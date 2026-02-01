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

        public RecipesExtractor(string logPath)
        {
            _logPath = logPath ?? throw new ArgumentNullException(nameof(logPath));
        }

        public override bool IsValid()
        {
            // TODO: Реализовать валидацию
            return true;
        }

        public override async Task<List<string>> ExtractAsync()
        {
            if (!File.Exists(_logPath))
            {
                throw new FileNotFoundException($"Файл не найден: {_logPath}", _logPath);
            }

            var recipes = new List<string>();

            string[] lines = await File.ReadAllLinesAsync(_logPath);
            Regex recipePattern = new Regex(@"recipe_id:\s*(?<recipeId>\S+)");

            foreach (string line in lines)
            {
                var match = recipePattern.Match(line);
                if (match.Success)
                {
                    string recipeId = match.Groups["recipeId"].Value;
                    recipes.Add(recipeId);
                }
            }

            Console.WriteLine($"Найдено {recipes.Count} рецептов.");

            return recipes;
        }
    }
}