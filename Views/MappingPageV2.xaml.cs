using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Rebind.Core.Models;
using Rebind.Services;

namespace Rebind.Views
{
    public partial class MappingPageV2 : Page
    {
        private ConfigManager _configManager;
        private MappingConfig _config;
        private Button? _activeBindButton;

        public MappingPageV2()
        {
            InitializeComponent();
            
            _configManager = new ConfigManager();
            _config = _configManager.LoadConfig();
            LoadConfigToUI();

            this.PreviewKeyDown += MappingPageV2_PreviewKeyDown;

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
            btnDPadRight.Content = _config.DPadRight;
            
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
            _config.DPadRight = btnDPadRight.Content?.ToString();
            
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
            if (_activeBindButton != null) _activeBindButton.Tag = null;

            _activeBindButton = sender as Button;
            if (_activeBindButton != null)
            {
                _activeBindButton.Tag = "Active";
                _activeBindButton.Content = "...";

                // Enable Binding Mode in the engine
                if (App.KeyMapper != null) App.KeyMapper.IsBindingMode = true;
                
                // Ensure focus
                Keyboard.Focus(this);
            }
        }

        private void MappingPageV2_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_activeBindButton != null)
            {
                e.Handled = true;

                string keyStr = e.Key.ToString();
                if (e.Key >= Key.D0 && e.Key <= Key.D9) keyStr = keyStr.Replace("D", "");
                if (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) keyStr = keyStr.Replace("NumPad", "Numpad ");
                if (e.Key == Key.Space) keyStr = "Space";
                
                _activeBindButton.Content = keyStr;
                _activeBindButton.Tag = null;
                _activeBindButton = null;

                // Disable Binding Mode in the engine
                if (App.KeyMapper != null) App.KeyMapper.IsBindingMode = false;

                SaveConfigFromUI();
            }
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
