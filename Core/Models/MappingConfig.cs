using System;
using System.Text.Json.Serialization;

namespace Rebind.Core.Models
{
    public class MappingConfig
    {
        public string? ToggleShortcut { get; set; } = "Insert";
        public string? DPadUp { get; set; }
        public string? DPadDown { get; set; }
        public string? DPadLeft { get; set; }
        public string? DPadRight { get; set; }
        public string? Guide { get; set; }
        public string? LeftBumper { get; set; }
        public string? JoystickXPositive { get; set; }
        public string? JoystickXNegative { get; set; }
        public string? JoystickYPositive { get; set; }
        public string? JoystickYNegative { get; set; }
        
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MouseLeftMapping { get; set; }
        
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? MouseRightMapping { get; set; }

        public bool IsStrafeEnabled { get; set; } = false;
        public bool IsJumpSpamEnabled { get; set; } = true;
    }
}
