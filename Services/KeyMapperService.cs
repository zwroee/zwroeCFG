using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Rebind.Core.Models;
using Rebind.Helpers;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace Rebind.Services
{
    public class KeyMapperService : IDisposable
    {
        [DllImport("winmm.dll")]
        private static extern uint timeBeginPeriod(uint period);
        [DllImport("winmm.dll")]
        private static extern uint timeEndPeriod(uint period);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);

        private const int KEYEVENTF_KEYUP = 0x0002;
        private const byte VK_N = 0x4E;
        private const byte VK_E = 0x45;
        private const byte SCAN_E = 0x12;
        private const byte SCAN_N = 0x31;

        private readonly KeyboardHook _keyboardHook;
        private readonly ViGEmService _vigemService;
        private readonly ConfigManager _configManager;
        
        private MappingConfig? _config;
        private bool _isEnabled = true;
        public bool IsEnabled => _isEnabled;
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
        private bool _isJumpKeyPressed = false;

        private int _macroCounter = 0;
        private bool _jumpState = false;
        private bool _lootState = false;
        private bool _isLootKeyPressed = false;

        private List<int> _horizontalStack = new List<int>();

        public event Action<bool>? OnToggleChanged;

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

        public void ReloadConfig()
        {
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
        }

        private void AddMapping(string? keyString, Action<bool> action)
        {
            if (keyString == null) return;
            int vkCode = KeyHelper.GetVirtualKeyCode(keyString);
            if (vkCode != -1)
            {
                if (keyString == _config.DPadRight) // Fast Loot
                {
                    _keyPressActions[vkCode] = isDown => _isLootKeyPressed = isDown;
                }
                else
                {
                    if (_keyPressActions.ContainsKey(vkCode)) _keyPressActions[vkCode] += action;
                    else _keyPressActions[vkCode] = action;
                }
                _blockedKeys.Add(vkCode);
            }
        }

        private bool HandleKeyEvent(int vkCode, bool isDown)
        {
            if (IsBindingMode) return false;

            if (vkCode == _toggleKeyVk && isDown)
            {
                _isEnabled = !_isEnabled;
                OnToggleChanged?.Invoke(_isEnabled);
                return true; // Block Insert key
            }

            if (!_isEnabled || _config == null) return false;

            // SnapTap (A/D)
            if (vkCode == _moveLeftVk || vkCode == _moveRightVk)
            {
                UpdateSnapTap(vkCode, isDown);
                return true; // Block physical A/D
            }

            // Jump (Space)
            if (vkCode == _jumpKeyVk)
            {
                _isJumpKeyPressed = isDown;
                if (_config.IsJumpSpamEnabled)
                {
                    if (!isDown) { _jumpState = false; _vigemService.SetButton(Xbox360Button.LeftShoulder, false); }
                }
                else
                {
                    _vigemService.SetButton(Xbox360Button.LeftShoulder, isDown);
                }
                return true; // Block physical Space
            }

            // Forward (W)
            if (vkCode == _strafeKeyVk)
            {
                _isStrafeKeyPressed = isDown;
                if (!_config.IsStrafeEnabled || !_isJumpKeyPressed || _horizontalStack.Count == 0)
                {
                    _vigemService.SetAxis(Xbox360Axis.LeftThumbY, (short)(isDown ? 32767 : 0));
                }
                return true; // Block physical W
            }

            // Backward (S)
            if (vkCode == _backVk)
            {
                _vigemService.SetAxis(Xbox360Axis.LeftThumbY, (short)(isDown ? -32768 : 0));
                return true; // Block physical S
            }

            if (_keyPressActions.TryGetValue(vkCode, out var action))
            {
                action.Invoke(isDown);
                return true; // Block other mapped keys
            }

            return false;
        }

        private void UpdateSnapTap(int vkCode, bool isDown)
        {
            if (isDown)
            {
                if (!_horizontalStack.Contains(vkCode)) _horizontalStack.Add(vkCode);
            }
            else
            {
                _horizontalStack.Remove(vkCode);
            }

            if (_horizontalStack.Count == 0)
            {
                _vigemService.SetAxis(Xbox360Axis.LeftThumbX, 0);
                if (_isStrafeKeyPressed) _vigemService.SetAxis(Xbox360Axis.LeftThumbY, 32767);
            }
            else
            {
                int lastKey = _horizontalStack[_horizontalStack.Count - 1];
                if (lastKey == _moveLeftVk) _vigemService.SetAxis(Xbox360Axis.LeftThumbX, -32768);
                else if (lastKey == _moveRightVk) _vigemService.SetAxis(Xbox360Axis.LeftThumbX, 32767);
            }
        }

        private void MacroLoop()
        {
            while (_isRunning)
            {
                if (_isEnabled && _config != null)
                {
                    _macroCounter++;

                    // Tap Strafe (Sharp Spike)
                    if (_config.IsStrafeEnabled && _isStrafeKeyPressed && _isJumpKeyPressed && _horizontalStack.Count > 0)
                    {
                        int cycleStep = _macroCounter % 5;
                        short forwardValue = (short)(cycleStep == 0 ? 0 : 32767);
                        _vigemService.SetAxis(Xbox360Axis.LeftThumbY, forwardValue);
                    }

                    // Jump Spam
                    if (_macroCounter % 5 == 0)
                    {
                        if (_config.IsJumpSpamEnabled && _isJumpKeyPressed)
                        {
                            _jumpState = !_jumpState;
                            _vigemService.SetButton(Xbox360Button.LeftShoulder, _jumpState);
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
            _isRunning = false;
            timeEndPeriod(1);
            _macroThread.Join(500);
        }
    }
}
