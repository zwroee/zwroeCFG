using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Rebind.Core.Models;
using Rebind.Helpers;
using Rebind.Services;

namespace Rebind.Views
{
    public partial class MappingPageV2 : Page
    {
        private ConfigManager _configManager;
        private MappingConfig _config;
        private Button? _activeBindButton;
        private string? _previousBindValue;

        public MappingPageV2()
        {
            InitializeComponent();
            
            // Reuse single shared instance from App to prevent diverged config state (Fix Bug #2)
            _configManager = App.ConfigManager ?? new ConfigManager();
            _config = _configManager.LoadConfig();
            LoadConfigToUI();

            this.PreviewKeyDown += MappingPageV2_PreviewKeyDown;
            this.PreviewMouseDown += MappingPageV2_PreviewMouseDown;

            if (App.KeyMapper != null)
            {
                App.KeyMapper.OnToggleChanged += UpdateStatusText;
            }
        }

        private void UpdateStatusText(bool isEnabled)
        {
            this.Dispatcher.Invoke(() => {
                StatusText.Text = isEnabled ? "ENABLED" : "DISABLED";
                StatusText.Foreground = new SolidColorBrush(isEnabled ? 
                    (Color)ColorConverter.ConvertFromString("#00CC66") : 
                    (Color)ColorConverter.ConvertFromString("#666666"));
            });
        }

        private void LoadConfigToUI()
        {
            btnForward.Content = _config.JoystickYPositive;
            btnBackward.Content = _config.JoystickYNegative;
            btnLeft.Content = _config.JoystickXNegative;
            btnRight.Content = _config.JoystickXPositive;
            btnJump.Content = _config.LeftBumper;
            btnToggle.Content = _config.ToggleShortcut;
            
            btnDPadUp.Content = _config.DPadUp;
            btnDPadDown.Content = _config.DPadDown;
            btnDPadRight.Content = _config.FastLootKey;
            btnInspect.Content = _config.InspectKey;
            
            togStrafe.IsChecked = _config.IsStrafeEnabled;
            togJump.IsChecked = _config.IsJumpSpamEnabled;
        }

        private void SaveConfigFromUI()
        {
            _config.JoystickYPositive = btnForward.Content?.ToString();
            _config.JoystickYNegative = btnBackward.Content?.ToString();
            _config.JoystickXNegative = btnLeft.Content?.ToString();
            _config.JoystickXPositive = btnRight.Content?.ToString();
            _config.LeftBumper = btnJump.Content?.ToString();
            _config.ToggleShortcut = btnToggle.Content?.ToString();
            
            _config.DPadUp = btnDPadUp.Content?.ToString();
            _config.DPadDown = btnDPadDown.Content?.ToString();
            _config.FastLootKey = btnDPadRight.Content?.ToString();
            _config.InspectKey = btnInspect.Content?.ToString();
            
            _config.IsStrafeEnabled = togStrafe.IsChecked ?? false;
            _config.IsJumpSpamEnabled = togJump.IsChecked ?? false;

            _configManager.SaveConfig(_config);
            App.KeyMapper?.ReloadConfig();
        }

        private void Setting_Changed(object sender, RoutedEventArgs e)
        {
            SaveConfigFromUI();
        }

        private void BindButton_Click(object sender, RoutedEventArgs e)
        {
            if (_activeBindButton != null) CancelBind();

            _activeBindButton = sender as Button;
            if (_activeBindButton != null)
            {
                _previousBindValue = _activeBindButton.Content?.ToString();
                _activeBindButton.Tag = "Active";
                _activeBindButton.Content = "...";

                // Enable Binding Mode in the engine
                if (App.KeyMapper != null) App.KeyMapper.IsBindingMode = true;
                
                // Ensure focus
                Keyboard.Focus(this);
            }
        }

        private void CancelBind()
        {
            if (_activeBindButton != null)
            {
                _activeBindButton.Content = _previousBindValue;
                _activeBindButton.Tag = null;
                _activeBindButton = null;
                _previousBindValue = null;
            }
            if (App.KeyMapper != null) App.KeyMapper.IsBindingMode = false;
        }

        private void MappingPageV2_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_activeBindButton != null)
            {
                e.Handled = true;

                Key targetKey = e.Key == Key.System ? e.SystemKey : e.Key;

                // Escape cancels binding mode (Fix Bug #1)
                if (targetKey == Key.Escape)
                {
                    CancelBind();
                    return;
                }

                string keyStr = KeyHelper.GetConfigKeyName(targetKey);
                CommitBind(keyStr);
            }
        }

        private void MappingPageV2_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_activeBindButton != null)
            {
                // Check if user clicked on something other than a bind button or current active button to cancel
                string? mouseStr = e.ChangedButton switch
                {
                    MouseButton.Middle   => "Mouse3",
                    MouseButton.XButton1 => "Mouse4",
                    MouseButton.XButton2 => "Mouse5",
                    _ => null
                };

                if (mouseStr != null)
                {
                    e.Handled = true;
                    CommitBind(mouseStr);
                }
                else if (e.OriginalSource is not Button b || b != _activeBindButton)
                {
                    // Clicked away with left/right click -> cancel binding mode (Fix Bug #1)
                    CancelBind();
                }
            }
        }

        private void CommitBind(string keyStr)
        {
            _activeBindButton!.Content = keyStr;
            _activeBindButton.Tag = null;
            _activeBindButton = null;
            _previousBindValue = null;

            if (App.KeyMapper != null) App.KeyMapper.IsBindingMode = false;

            SaveConfigFromUI();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveConfigFromUI();
            StatusText.Text = "CONFIG SAVED";
            var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (s, ev) => { 
                StatusText.Text = (App.KeyMapper?.IsEnabled ?? true) ? "ENABLED" : "DISABLED"; 
                timer.Stop(); 
            };
            timer.Start();
        }
    }
}
