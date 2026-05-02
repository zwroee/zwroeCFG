using System.Windows;
using Rebind.Services;

namespace Rebind
{
    public partial class App : Application
    {
        public static KeyMapperService? KeyMapper { get; private set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var configManager = new ConfigManager();
            var keyboardHook = new KeyboardHook();
            var vigemService = new ViGEmService();
            
            KeyMapper = new KeyMapperService(configManager, keyboardHook, vigemService);
        }
    }
}
