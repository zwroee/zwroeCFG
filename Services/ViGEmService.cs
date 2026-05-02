using System;
using System.Threading;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

namespace Rebind.Services
{
    public class ViGEmService : IDisposable
    {
        private ViGEmClient? _client;
        private IXbox360Controller? _controller;

        public ViGEmService()
        {
            try
            {
                // Simple, old-school initialization
                _client = new ViGEmClient();
                _controller = _client.CreateXbox360Controller();
                _controller.Connect();
            }
            catch (Exception ex)
            {
                // We will fall back to a "Silent" mode if it fails, 
                // so the GUI still opens, but we'll log the real error.
                Console.WriteLine($"VIGEM ERROR: {ex.Message}");
            }
        }

        public void SetButton(Xbox360Button button, bool pressed)
        {
            try { _controller?.SetButtonState(button, pressed); } catch { }
        }

        public void SetAxis(Xbox360Axis axis, short value)
        {
            try { _controller?.SetAxisValue(axis, value); } catch { }
        }

        public void Dispose()
        {
            try { _controller?.Disconnect(); } catch { }
            try { _client?.Dispose(); } catch { }
        }
    }
}
