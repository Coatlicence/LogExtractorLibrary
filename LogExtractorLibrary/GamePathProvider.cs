using System;
using System.IO;
using System.Text.Json;

namespace LogExtractorLibrary
{
    public static class GamePathProvider
    {
        private const string ConfigFileName = "config.json";
        private static readonly string _configFilePath;
        private static string _gameRootPath = "";

        static GamePathProvider()
        {
            // Определяем путь к папке %APPDATA%\LogExtractor
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string configDir = Path.Combine(appDataPath, "LogExtractor");
            _configFilePath = Path.Combine(configDir, ConfigFileName);

            // Создаём папку, если её нет
            Directory.CreateDirectory(configDir);

            // Загружаем настройки
            LoadConfig();
        }

        public static string GetGameRootPath()
        {
            return _gameRootPath;
        }

        public static string GetRecipesLogPath()
        {
            return Path.Combine(_gameRootPath, "logs", "kubejs", "server.log");
        }

        public static string GetIconsPath()
        {
            return Path.Combine(_gameRootPath, "icon-exports-x16");
        }

        public static void SetGameRootPath(string path)
        {
            _gameRootPath = path;
            SaveConfig();
        }

        private static void LoadConfig()
        {
            if (!File.Exists(_configFilePath))
            {
                // Если файла нет, создаём с дефолтным значением
                _gameRootPath = @"C:\Users\f0578\AppData\Roaming\.tlauncher\legacy\Minecraft\game";
                SaveConfig();
            }
            else
            {
                string json = File.ReadAllText(_configFilePath);
                var config = JsonSerializer.Deserialize<ConfigModel>(json);
                _gameRootPath = config?.GameRootPath ?? @"C:\Users\f0578\AppData\Roaming\.tlauncher\legacy\Minecraft\game";
            }
        }

        private static void SaveConfig()
        {
            var config = new ConfigModel { GameRootPath = _gameRootPath };
            string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configFilePath, json);
        }

        private class ConfigModel
        {
            public string GameRootPath { get; set; } = "";
        }
    }
}