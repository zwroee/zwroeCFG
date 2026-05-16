using System;
using System.Windows.Input;

namespace Rebind.Helpers
{
    /// <summary>
    /// Utility class for translating string key names into Windows virtual key codes.
    /// </summary>
    public static class KeyHelper
    {
        /// <summary>
        /// Converts a string representation of a key (e.g., "Space", "W", "Insert")
        /// into its corresponding Windows Virtual Key Code integer.
        /// Returns -1 if the conversion fails.
        /// </summary>
        public static int GetVirtualKeyCode(string keyString)
        {
            if (string.IsNullOrWhiteSpace(keyString))
                return -1;

            string normalizedKey = NormalizeKeyString(keyString);

            if (Enum.TryParse<Key>(normalizedKey, true, out Key key))
            {
                return KeyInterop.VirtualKeyFromKey(key);
            }
            
            // Handle some common aliases if Enum.TryParse fails
            if (normalizedKey.Equals("Esc", StringComparison.OrdinalIgnoreCase))
                return KeyInterop.VirtualKeyFromKey(Key.Escape);

            if (normalizedKey.Equals("Spacebar", StringComparison.OrdinalIgnoreCase))
                return KeyInterop.VirtualKeyFromKey(Key.Space);

            return -1;
        }

        public static string GetConfigKeyName(Key key)
        {
            return key switch
            {
                Key.Space => "Space",
                _ => key.ToString()
            };
        }

        private static string NormalizeKeyString(string keyString)
        {
            string trimmed = keyString.Trim();

            if (trimmed.Length == 1 && char.IsDigit(trimmed[0]))
                return $"D{trimmed}";

            string compact = trimmed.Replace(" ", string.Empty);
            if (compact.StartsWith("Numpad", StringComparison.OrdinalIgnoreCase))
            {
                string suffix = compact.Substring("Numpad".Length);
                if (suffix.Length == 1 && char.IsDigit(suffix[0]))
                    return $"NumPad{suffix}";
            }

            return trimmed;
        }
    }
}
