using System;
using System.IO;
using System.Text.Json;
using Rebind.Core.Models;

namespace Rebind.Services
{
    public class ConfigManager
    {
        private const string ConfigFileName = "mappingConfig.json";
        private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

        public MappingConfig LoadConfig()
        {
            if (!File.Exists(ConfigFilePath))
            {
                var defaultConfig = new MappingConfig();
                SaveConfig(defaultConfig);
                return defaultConfig;
            }

            try
            {
                string json = File.ReadAllText(ConfigFilePath);
                return JsonSerializer.Deserialize<MappingConfig>(json) ?? new MappingConfig();
            }
            catch
            {
                return new MappingConfig();
            }
        }

        public void SaveConfig(MappingConfig config)
        {
            try
            {
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(ConfigFilePath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving config: {ex.Message}");
            }
        }
    }
}
