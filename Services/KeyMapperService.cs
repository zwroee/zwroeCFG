using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Rebind.Core.Models;
using Rebind.Helpers;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using System.IO;

namespace Rebind.Services
{
    public static class KeyLogger
    {
        private static string _logPath = Path.Combine(AppContext.BaseDirectory, "key_log.txt");
        private static object _lock = new object();
        
        public static void Log(string message)
        {
            try 
            { 
                lock (_lock) 
                { 
                    File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n"); 
                } 
            } 
            catch { }
        }
    }

    /// <summary>
    /// The core engine of the remapper.
    /// Handles listening to physical keystrokes, executing the high-precision macro loop,
    /// and routing simulated inputs to the virtual controller via ViGEm.
    /// </summary>
    public class KeyMapperService : IDisposable
    {
        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint period);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

        private const int KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_SCANCODE = 0x0008;
        private const byte VK_E = 0x45;
        private const byte SCAN_E = 0x12;
        private const byte SCAN_SPACE = 0x39;

        // In-game movement keybind scan codes
        // Forward=I, Backward=K, Left=J, Right=L, Jump=Y
        private const byte SCAN_I = 0x17; // In-game Forward
        private const byte SCAN_K = 0x25; // In-game Backward
        private const byte SCAN_J = 0x24; // In-game Left
        private const byte SCAN_L = 0x26; // In-game Right
        private const byte SCAN_Y = 0x15; // In-game Jump

        private readonly KeyboardHook _keyboardHook;
        private readonly ViGEmService _vigemService;
        private readonly ConfigManager _configManager;

        private MappingConfig? _config;
        private bool _isEnabled = true;

        /// <summary>
        /// Indicates if the remapper engine is currently active.
        /// </summary>
        public bool IsEnabled => _isEnabled;

        /// <summary>
        /// Set to true by the UI to pause input blocking, allowing the user to bind new keys.
        /// </summary>
        public bool IsBindingMode { get; set; } = false;

        private int _toggleKeyVk;
        private int _strafeKeyVk;
        private int _jumpKeyVk;
        private int _moveLeftVk;
        private int _moveRightVk;
        private int _backVk;

        private HashSet<int> _blockedKeys = new HashSet<int>();
        private Dictionary<int, Action<bool>> _keyPressActions;

        private readonly Thread _macroThread;
        private bool _isRunning = true;
        private bool _isStrafeKeyPressed = false;
        private bool _isBackKeyPressed = false;
        private bool _isJumpKeyPressed = false;

        private short _currentJoyX = 0;
        private short _currentJoyY = 0;

        private int _macroCounter = 0;
        private bool _jumpState = false;
        private bool _lootState = false;
        private bool _isLootKeyPressed = false;

        private readonly object _stackLock = new object();
        private List<int> _horizontalStack = new List<int>();
        private List<int> _verticalStack = new List<int>();

        private bool _wasTapStrafeActive = false;

        private short _lastLoggedJoyX = 0;
        private short _lastLoggedJoyY = 0;

        private Dictionary<byte, bool> _scanCodeStates = new Dictionary<byte, bool>();

        public event Action<bool>? OnToggleChanged;

        /// <summary>
        /// Initializes the KeyMapperService and starts the high-precision background macro loop.
        /// </summary>
        public KeyMapperService(ConfigManager configManager, KeyboardHook keyboardHook, ViGEmService vigemService)
        {
            _configManager = configManager;
            _keyboardHook = keyboardHook;
            _vigemService = vigemService;
            _keyPressActions = new Dictionary<int, Action<bool>>();

            timeBeginPeriod(1);

            _macroThread = new Thread(MacroLoop) { IsBackground = true, Priority = ThreadPriority.Highest };
            _macroThread.Start();

            ReloadConfig();

            _keyboardHook.KeyEvent += HandleKeyEvent;
        }

        /// <summary>
        /// Reloads the mapping configuration from the disk and rebuilds the virtual keycode cache.
        /// Can be called at runtime to apply new settings without restarting the engine.
        /// </summary>
        public void ReloadConfig()
        {
            ClearVirtualInputs();
            _config = _configManager.LoadConfig();

            _toggleKeyVk = KeyHelper.GetVirtualKeyCode(_config.ToggleShortcut ?? "Insert");
            _strafeKeyVk = KeyHelper.GetVirtualKeyCode(_config.JoystickYPositive ?? "W");
            _jumpKeyVk = KeyHelper.GetVirtualKeyCode(_config.LeftBumper ?? "Space");
            _moveLeftVk = KeyHelper.GetVirtualKeyCode(_config.JoystickXNegative ?? "A");
            _moveRightVk = KeyHelper.GetVirtualKeyCode(_config.JoystickXPositive ?? "D");
            _backVk = KeyHelper.GetVirtualKeyCode(_config.JoystickYNegative ?? "S");

            _blockedKeys.Clear();
            _blockedKeys.Add(_toggleKeyVk);
            _blockedKeys.Add(_strafeKeyVk);
            _blockedKeys.Add(_jumpKeyVk);
            _blockedKeys.Add(_moveLeftVk);
            _blockedKeys.Add(_moveRightVk);
            _blockedKeys.Add(_backVk);

            BuildMappingCache();
        }

        private void BuildMappingCache()
        {
            if (_config == null) return;
            _keyPressActions.Clear();

            AddMapping(_config.DPadUp, isDown => _vigemService.SetButton(Xbox360Button.Up, isDown));
            AddMapping(_config.DPadDown, isDown => _vigemService.SetButton(Xbox360Button.Down, isDown));
            AddMapping(_config.DPadLeft, isDown => _vigemService.SetButton(Xbox360Button.Left, isDown));
            AddMapping(_config.DPadRight, isDown => _vigemService.SetButton(Xbox360Button.Right, isDown));
            AddMapping(_config.Guide, isDown => _vigemService.SetButton(Xbox360Button.Guide, isDown));
            AddMapping(_config.FastLootKey, isDown => _isLootKeyPressed = isDown);
        }

        private void AddMapping(string? keyString, Action<bool> action)
        {
            if (keyString == null) return;
            int vkCode = KeyHelper.GetVirtualKeyCode(keyString);
            if (vkCode != -1)
            {
                if (_keyPressActions.ContainsKey(vkCode)) _keyPressActions[vkCode] += action;
                else _keyPressActions[vkCode] = action;

                _blockedKeys.Add(vkCode);
            }
        }

        /// <summary>
        /// The primary intercept handler. Decides whether to block a physical key press
        /// and trigger the corresponding controller logic (SnapTap, Jump, etc.).
        /// </summary>
        private bool HandleKeyEvent(int vkCode, bool isDown)
        {
            if (IsBindingMode) return false;

            if (vkCode == _toggleKeyVk && isDown)
            {
                KeyLogger.Log($"PHYSICAL: {(isDown ? "DOWN" : "UP")} - VK: {vkCode}");
                _isEnabled = !_isEnabled;
                if (!_isEnabled) ClearVirtualInputs();

                OnToggleChanged?.Invoke(_isEnabled);
                return true; // Block Insert key
            }

            if (!_isEnabled || _config == null) return false;

            // SnapTap (A/D)
            if (vkCode == _moveLeftVk || vkCode == _moveRightVk)
            {
                KeyLogger.Log($"PHYSICAL: {(isDown ? "DOWN" : "UP")} - VK: {vkCode}");
                UpdateSnapTap(vkCode, isDown);
                return true; // Block physical A/D
            }

            // Jump (Space)
            if (vkCode == _jumpKeyVk)
            {
                KeyLogger.Log($"PHYSICAL: {(isDown ? "DOWN" : "UP")} - VK: {vkCode}");
                _isJumpKeyPressed = isDown;
                if (_config.IsJumpSpamEnabled)
                {
                    if (!isDown) 
                    { 
                        _jumpState = false; 
                        _vigemService.SetButton(Xbox360Button.LeftShoulder, false); 
                        SendScanCode(SCAN_SPACE, false); 
                    }
                }
                else
                {
                    _vigemService.SetButton(Xbox360Button.LeftShoulder, isDown);
                    SendScanCode(SCAN_SPACE, isDown);
                }

                UpdateMovementOutput();
                return true; // Block physical Space
            }

            // Forward (W)
            if (vkCode == _strafeKeyVk)
            {
                KeyLogger.Log($"PHYSICAL: {(isDown ? "DOWN" : "UP")} - VK: {vkCode}");
                _isStrafeKeyPressed = isDown;
                UpdateSnapTap(vkCode, isDown);
                return true; // Block physical W
            }

            // Backward (S)
            if (vkCode == _backVk)
            {
                KeyLogger.Log($"PHYSICAL: {(isDown ? "DOWN" : "UP")} - VK: {vkCode}");
                _isBackKeyPressed = isDown;
                UpdateSnapTap(vkCode, isDown);
                return true; // Block physical S
            }

            if (_keyPressActions.TryGetValue(vkCode, out var action))
            {
                action.Invoke(isDown);
                return true; // Block other mapped keys
            }

            return false;
        }

        /// <summary>
        /// Implements SnapTap (SOCD) logic. Resolves overlapping Left/Right inputs
        /// by strictly prioritizing the most recently pressed key, ensuring no momentum loss.
        /// </summary>
        private void UpdateSnapTap(int vkCode, bool isDown)
        {
            lock (_stackLock)
            {
                if (vkCode == _moveLeftVk || vkCode == _moveRightVk)
                {
                    if (isDown) { if (!_horizontalStack.Contains(vkCode)) _horizontalStack.Add(vkCode); }
                    else { _horizontalStack.Remove(vkCode); }
                }
                else if (vkCode == _strafeKeyVk || vkCode == _backVk)
                {
                    if (isDown) { if (!_verticalStack.Contains(vkCode)) _verticalStack.Add(vkCode); }
                    else { _verticalStack.Remove(vkCode); }
                }
            }

            UpdateMovementOutput();
        }

        private bool IsMacroActive()
        {
            // Only active when Space is held AND at least one movement key is pressed
            return _config?.IsStrafeEnabled == true && _isJumpKeyPressed
                && (_isStrafeKeyPressed || _isBackKeyPressed || _horizontalStack.Count > 0);
        }

        private void UpdateMovementOutput()
        {
            bool ew = false, es = false, ea = false, ed = false;

            lock (_stackLock)
            {
                if (_verticalStack.Count > 0)
                {
                    int lastY = _verticalStack[_verticalStack.Count - 1];
                    if (lastY == _strafeKeyVk) ew = true;
                    else if (lastY == _backVk) es = true;
                }

                if (_horizontalStack.Count > 0)
                {
                    int lastX = _horizontalStack[_horizontalStack.Count - 1];
                    if (lastX == _moveLeftVk) ea = true;
                    else if (lastX == _moveRightVk) ed = true;
                }
            }

            _currentJoyY = (short)(ew ? 32767 : (es ? -32768 : 0));
            _currentJoyX = (short)(ed ? 32767 : (ea ? -32768 : 0));

            if (_currentJoyX != _lastLoggedJoyX || _currentJoyY != _lastLoggedJoyY)
            {
                _lastLoggedJoyX = _currentJoyX;
                _lastLoggedJoyY = _currentJoyY;
                KeyLogger.Log($"VIRTUAL JOYSTICK: X={_currentJoyX}, Y={_currentJoyY}");
            }

            // EXCLUSIVELY use Virtual Joystick for continuous movement!
            // This prevents "sticky keys" and "sliding" by eliminating overlapping keyboard inputs.
            _vigemService.SetAxis(Xbox360Axis.LeftThumbX, _currentJoyX);
            _vigemService.SetAxis(Xbox360Axis.LeftThumbY, _currentJoyY);
        }

        private void SendScanCode(byte scanCode, bool isDown)
        {
            if (!_scanCodeStates.ContainsKey(scanCode)) _scanCodeStates[scanCode] = false;
            
            // Skip redundant hardware calls
            if (_scanCodeStates[scanCode] == isDown) return;
            
            _scanCodeStates[scanCode] = isDown;
            KeyLogger.Log($"VIRTUAL KEYBOARD: {(isDown ? "DOWN" : "UP")} - SCAN: 0x{scanCode:X2}");

            uint flag = KEYEVENTF_SCANCODE | (isDown ? 0 : (uint)KEYEVENTF_KEYUP);
            keybd_event(0, scanCode, flag, KeyboardHook.SYNTHETIC_MARKER);
        }

        private void ClearVirtualInputs()
        {
            _isStrafeKeyPressed = false;
            _isBackKeyPressed = false;
            _isJumpKeyPressed = false;
            _isLootKeyPressed = false;
            _jumpState = false;
            _lootState = false;
            _horizontalStack.Clear();
            _verticalStack.Clear();

            _currentJoyX = 0;
            _currentJoyY = 0;

            // Release all virtual in-game keys
            SendScanCode(SCAN_I, false);
            SendScanCode(SCAN_K, false);
            SendScanCode(SCAN_J, false);
            SendScanCode(SCAN_L, false);
            SendScanCode(SCAN_Y, false);
            SendScanCode(SCAN_SPACE, false);

            _vigemService.SetAxis(Xbox360Axis.LeftThumbX, 0);
            _vigemService.SetAxis(Xbox360Axis.LeftThumbY, 0);
            _vigemService.SetButton(Xbox360Button.LeftShoulder, false);
            _vigemService.SetButton(Xbox360Button.Up, false);
            _vigemService.SetButton(Xbox360Button.Down, false);
            _vigemService.SetButton(Xbox360Button.Left, false);
            _vigemService.SetButton(Xbox360Button.Right, false);
            _vigemService.SetButton(Xbox360Button.Guide, false);
            keybd_event(VK_E, SCAN_E, KEYEVENTF_KEYUP, 0);
        }

        /// <summary>
        /// A high-priority background loop running at 3ms intervals.
        /// Executes tap-strafe pulsing, optional auto jump, and fast-loot helpers.
        /// </summary>
        private void MacroLoop()
        {
            while (_isRunning)
            {
                if (_isEnabled && _config != null)
                {
                    _macroCounter++;

                    // ── Tap Strafe Engine ──────────────────────────────────────────
                    // Rules (Space must be held):
                    //   Space + W  → spam Y + I
                    //   Space + S  → spam Y + K
                    //   Space + A  → spam Y + J
                    //   Space + D  → spam Y + L
                    // Pulse: 9ms ON (3 ticks) / 3ms OFF (1 tick) = 75% duty cycle
                    if (IsMacroActive())
                    {
                        _wasTapStrafeActive = true;

                        // Thread-safe stack snapshot
                        bool holdW, holdS, holdA, holdD;
                        lock (_stackLock)
                        {
                            holdW = _verticalStack.Count > 0 && _verticalStack[_verticalStack.Count - 1] == _strafeKeyVk;
                            holdS = _verticalStack.Count > 0 && _verticalStack[_verticalStack.Count - 1] == _backVk;
                            holdA = _horizontalStack.Count > 0 && _horizontalStack[_horizontalStack.Count - 1] == _moveLeftVk;
                            holdD = _horizontalStack.Count > 0 && _horizontalStack[_horizontalStack.Count - 1] == _moveRightVk;
                        }

                        // Log state every 100 ticks (~300ms) for debugging
                        if (_macroCounter % 100 == 0)
                            KeyLogger.Log($"STRAFE STATE: W={holdW} S={holdS} A={holdA} D={holdD} | vStack={_verticalStack.Count} hStack={_horizontalStack.Count}");

                        // The virtual joystick remains ACTIVE based on normal movement input.
                        // We do NOT zero it out. The smooth base momentum comes from the controller,
                        // and the sharp directional lurches come from the spammed keyboard scan codes on top.

                        // Replicating the EXACT duty cycle from your old code:
                        // 12ms ON (4 ticks) / 3ms OFF (1 tick)
                        bool tapOn = (_macroCounter % 5 != 0);

                        if (tapOn)
                        {
                            SendScanCode(SCAN_Y, true);
                            SendScanCode(SCAN_I, holdW);
                            SendScanCode(SCAN_K, holdS);
                            SendScanCode(SCAN_J, holdA);
                            SendScanCode(SCAN_L, holdD);
                        }
                        else
                        {
                            SendScanCode(SCAN_Y, false);
                            SendScanCode(SCAN_I, false);
                            SendScanCode(SCAN_K, false);
                            SendScanCode(SCAN_J, false);
                            SendScanCode(SCAN_L, false);
                        }
                    }
                    else
                    {
                        // Clean up when Space released or no direction held
                        if (_wasTapStrafeActive)
                        {
                            _wasTapStrafeActive = false;
                            SendScanCode(SCAN_Y, false);
                            SendScanCode(SCAN_I, false);
                            SendScanCode(SCAN_K, false);
                            SendScanCode(SCAN_J, false);
                            SendScanCode(SCAN_L, false);
                        }

                        if (_macroCounter % 5 == 0)
                        {
                            // Normal Jump Spam (when not Tap Strafing)
                            if (_config.IsJumpSpamEnabled && _isJumpKeyPressed)
                            {
                                _jumpState = !_jumpState;
                                _vigemService.SetButton(Xbox360Button.LeftShoulder, _jumpState);
                                SendScanCode(SCAN_SPACE, _jumpState);
                            }
                        }
                    }

                    // FAST LOOT SPAM (Keyboard E)
                    // Every 5 steps (15ms toggle = 30ms cycle = 33Hz)
                    if (_macroCounter % 5 == 0)
                    {
                        if (_isLootKeyPressed)
                        {
                            _lootState = !_lootState;
                            if (_lootState) keybd_event(VK_E, SCAN_E, 0, 0);
                            else keybd_event(VK_E, SCAN_E, KEYEVENTF_KEYUP, 0);
                        }
                        else if (_lootState)
                        {
                            _lootState = false;
                            keybd_event(VK_E, SCAN_E, KEYEVENTF_KEYUP, 0);
                        }
                    }
                }

                Thread.Sleep(3); 
            }
        }

        public void Dispose()
        {
            _keyboardHook.KeyEvent -= HandleKeyEvent;
            ClearVirtualInputs();
            _isRunning = false;
            timeEndPeriod(1);
            _macroThread.Join(500);
            _keyboardHook.Dispose();
            _vigemService.Dispose();
        }
    }
}
