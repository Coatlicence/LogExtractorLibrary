using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LogExtractorLibrary
{
    public class ItemsAndFluidsData
    {
        public List<MinecraftItem> Items { get; set; } = new();
        public List<MinecraftItem> Fluids { get; set; } = new();
    }

    public class ItemFluidWithIconExtractor : BaseExtractor<ItemsAndFluidsData>
    {
        private readonly string _iconsPath;

        public ItemFluidWithIconExtractor()
        {
            _iconsPath = GamePathProvider.GetIconsPath();
        }

        public override bool IsValid()
        {
            return Directory.Exists(_iconsPath);
        }

        public override async Task<ItemsAndFluidsData> ExtractAsync()
        {
            var items = new List<MinecraftItem>();
            var fluids = new List<MinecraftItem>();

            string[] files = Directory.GetFiles(_iconsPath, "*.png", SearchOption.TopDirectoryOnly);
            Regex nbtRegex = new Regex(@"\{.*\}"); // Для проверки NBT тегов

            foreach (string file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);

                // Пропускаем файлы с NBT тегами
                if (nbtRegex.IsMatch(fileName))
                {
                    continue;
                }

                // Проверяем, является ли это жидкостью
                if (fileName.StartsWith("fluid__"))
                {
                    string[] parts = fileName.Substring(7).Split("__", 2, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2) continue;

                    string modId = parts[0];
                    string fluidId = parts[1];
                    string fullId = $"{modId}:{fluidId}";

                    byte[] imageData = await File.ReadAllBytesAsync(file);

                    fluids.Add(new MinecraftItem
                    {
                        Id = fullId,
                        ImageData = imageData
                    });
                }
                else
                {
                    // Обычный предмет
                    string[] parts = fileName.Split("__", 2, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length != 2) continue;

                    string modId = parts[0];
                    string itemId = parts[1];
                    string fullId = $"{modId}:{itemId}";

                    byte[] imageData = await File.ReadAllBytesAsync(file);

                    items.Add(new MinecraftItem
                    {
                        Id = fullId,
                        ImageData = imageData
                    });
                }
            }

            Console.WriteLine($"Найдено {items.Count} предметов и {fluids.Count} жидкостей.");

            return new ItemsAndFluidsData { Items = items, Fluids = fluids };
        }
    }

    public class MinecraftItem
    {
        public string Id { get; set; } = "";
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
    }
}