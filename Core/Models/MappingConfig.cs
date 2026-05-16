using System;
using System.Text.Json.Serialization;

namespace Rebind.Core.Models
{
    /// <summary>
    /// Represents the user's keybind configuration.
    /// Serialized to and from mappingConfig.json.
    /// </summary>
    public class MappingConfig
    {
        /// <summary>Shortcut key used to toggle the entire remapper engine on and off.</summary>
        public string? ToggleShortcut { get; set; } = "Insert";

        /// <summary>Keyboard key mapped to the controller's DPad Up (Mantle Jump).</summary>
        public string? DPadUp { get; set; } = "X";
        /// <summary>Keyboard key mapped to the controller's DPad Down (Superglide).</summary>
        public string? DPadDown { get; set; } = "V";
        public string? DPadLeft { get; set; }
        /// <summary>Keyboard key mapped to the controller's DPad Right.</summary>
        public string? DPadRight { get; set; }
        public string? Guide { get; set; } = "G";
        /// <summary>Keyboard key mapped to the controller's Left Bumper (Jump).</summary>
        public string? LeftBumper { get; set; } = "Space";

        // Directional movement mappings
        public string? JoystickXPositive { get; set; } = "D"; // Right
        public string? JoystickXNegative { get; set; } = "A"; // Left
        public string? JoystickYPositive { get; set; } = "W"; // Forward
        public string? JoystickYNegative { get; set; } = "S"; // Backward

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MouseLeftMapping { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MouseRightMapping { get; set; }

        /// <summary>Keyboard key used for the fast-loot E spam helper.</summary>
        public string? FastLootKey { get; set; } = "B";

        /// <summary>Enables or disables the Tap Strafe (Lurch) macro engine.</summary>
        public bool IsStrafeEnabled { get; set; } = false;

        /// <summary>Enables or disables the optional continuous jump pulse.</summary>
        public bool IsJumpSpamEnabled { get; set; } = false;
    }
}
