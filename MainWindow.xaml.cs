using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Rebind.Views;
using Rebind.Services;

namespace Rebind
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            
            MainFrame.Navigate(new MappingPageV2());

            // Subscribe to toggle events from the engine
            if (App.KeyMapper != null)
            {
                App.KeyMapper.OnToggleChanged += UpdateStatusUI;
            }
        }

        private void UpdateStatusUI(bool isEnabled)
        {
            // Update the dot in the title bar
            this.Dispatcher.Invoke(() => {
                StatusDot.Fill = new SolidColorBrush(isEnabled ? 
                    (Color)ColorConverter.ConvertFromString("#00CC66") : 
                    (Color)ColorConverter.ConvertFromString("#FF4B4B"));
            });
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                this.DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
    }
}