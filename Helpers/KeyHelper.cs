using System;
using System.Windows.Input;

namespace Rebind.Helpers
{
    public static class KeyHelper
    {
        public static int GetVirtualKeyCode(string keyString)
        {
            if (string.IsNullOrWhiteSpace(keyString))
                return -1;

            if (Enum.TryParse<Key>(keyString, true, out Key key))
            {
                return KeyInterop.VirtualKeyFromKey(key);
            }
            
            // Handle some common aliases if Enum.TryParse fails
            if (keyString.Equals("Space", StringComparison.OrdinalIgnoreCase))
                return KeyInterop.VirtualKeyFromKey(Key.Space);

            return -1;
        }
    }
}
