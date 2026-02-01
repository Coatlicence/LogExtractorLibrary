using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LogExtractorLibrary
{
    public class RecipesExtractor : BaseExtractor<List<string>>
    {
        private readonly string _logPath;


        public RecipesExtractor()
        {
            _logPath = GamePathProvider.GetRecipesLogPath();
        }


        public override bool IsValid()
        {
            string rootPath = GamePathProvider.GetGameRootPath();
            Console.WriteLine("Корень игры: " + rootPath);

            string logPath = Path.Combine(rootPath, "logs", "kubejs", "server.log");
            Console.WriteLine("Путь к логу: " + logPath);

            string scriptPath = Path.Combine(rootPath, "kubejs", "server_scripts", "log_all_recipes.js");
            Console.WriteLine("Путь к скрипту: " + scriptPath);

            bool logExists = File.Exists(logPath);
            bool scriptExists = File.Exists(scriptPath);

            Console.WriteLine("Лог существует: " + logExists);
            Console.WriteLine("Скрипт существует: " + scriptExists);

            if (!logExists) return false;
            if (!scriptExists) return false;

            return true;
        }

        public override async Task<List<string>> ExtractAsync()
        {
            if (!File.Exists(_logPath))
            {
                throw new FileNotFoundException($"Файл не найден: {_logPath}");
            }

            var recipes = new List<string>();
            string[] lines = await File.ReadAllLinesAsync(_logPath);
            Regex recipePattern = new(@"recipe_id:\s*(?<recipeId>\S+)");

            foreach (string line in lines)
            {
                var match = recipePattern.Match(line);
                if (match.Success)
                {
                    string recipeId = match.Groups["recipeId"].Value;
                    recipes.Add(recipeId);
                }
            }

            //Console.WriteLine($"Найдено {recipes.Count} рецептов.");

            return recipes;
        }
    }
}