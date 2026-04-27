using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
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

            // Путь к файлу metadata.json (предполагается, что он рядом с папкой иконок)
            string metadataPath = Path.Combine(Path.GetDirectoryName(_iconsPath) ?? "", "icon-exports-metadata.json");

            if (!File.Exists(metadataPath))
            {
                throw new FileNotFoundException($"Файл metadata.json не найден: {metadataPath}");
            }

            // Читаем и парсим metadata
            string metadataJson = await File.ReadAllTextAsync(metadataPath);
            var metadataDict = new Dictionary<string, MetadataEntry>();

            using (JsonDocument doc = JsonDocument.Parse(metadataJson))
            {
                JsonElement root = doc.RootElement;
                if (root.TryGetProperty("meta", out JsonElement metaArray))
                {
                    foreach (JsonElement entryElement in metaArray.EnumerateArray())
                    {
                        var entry = new MetadataEntry
                        {
                            Id = entryElement.GetProperty("id").GetString()!,
                            LocalName = entryElement.GetProperty("local_name").GetString()!,
                            ModName = entryElement.GetProperty("mod_name").GetString()!,
                            Type = entryElement.GetProperty("type").GetString()!,
                        };

                        // Извлекаем теги, если они есть
                        if (entryElement.TryGetProperty("tags", out JsonElement tagsElement))
                        {
                            foreach (JsonElement tagElement in tagsElement.EnumerateArray())
                            {
                                entry.Tags.Add(tagElement.GetString()!);
                            }
                        }

                        metadataDict[entry.Id] = entry;
                    }
                }
            }

            string[] files = Directory.GetFiles(_iconsPath, "*.png", SearchOption.AllDirectories);
            Regex nbtRegex = new Regex(@"\{.*\}", RegexOptions.Compiled); // Для проверки NBT тегов
            Regex flowingRegex = new Regex(@"^fluid__.*__flowing_", RegexOptions.Compiled); // flowing жидкости

            foreach (string file in files)
            {
                string fileName = Path.GetFileNameWithoutExtension(file);

                // Пропускаем файлы с NBT тегами
                if (nbtRegex.IsMatch(fileName))
                {
                    continue;
                }

                // Попробуем извлечь ID из имени файла в формате IconExporter
                // Например: modid__itemid__.png или fluid__modid__fluidid__.png
                string extractedId = ExtractIdFromFileName(fileName);

                if (string.IsNullOrEmpty(extractedId))
                {
                    continue; // Не удалось извлечь ID, пропускаем
                }

                // Пропускаем flowing жидкости на этапе определения типа
                if (fileName.StartsWith("fluid__") && flowingRegex.IsMatch(fileName))
                {
                    continue;
                }

                // Проверяем, есть ли данные в metadata для этого ID
                if (!metadataDict.TryGetValue(extractedId, out MetadataEntry? metadataEntry))
                {
                    Console.WriteLine($"Предупреждение: Нет метаданных для ID {extractedId}, файл {file}");
                    // Продолжаем, но без метаданных
                    metadataEntry = new MetadataEntry
                    {
                        Id = extractedId,
                        LocalName = extractedId, // Используем ID как название, если нет метаданных
                        ModName = "Unknown", // Или извлекаем из ID: extractedId.Split(':')[0]
                        Type = fileName.StartsWith("fluid__") ? "fluid" : "item",
                        Tags = new List<string>()
                    };
                }

                byte[] imageData = await File.ReadAllBytesAsync(file);

                var minecraftItem = new MinecraftItem
                {
                    Id = metadataEntry.Id,
                    LocalizedName = metadataEntry.LocalName,
                    ModName = metadataEntry.ModName,
                    Type = metadataEntry.Type,
                    Tags = metadataEntry.Tags,
                    ImageData = imageData
                };

                // Определяем тип на основе метаданных или имени файла
                if (metadataEntry.Type.Equals("fluid", StringComparison.OrdinalIgnoreCase) ||
                    fileName.StartsWith("fluid__")) // Резервное определение по имени файла
                {
                    // Дополнительная проверка: исключить flowing из итогового списка
                    if (!flowingRegex.IsMatch(fileName))
                    {
                        fluids.Add(minecraftItem);
                    }
                }
                else
                {
                    items.Add(minecraftItem);
                }
            }

            Console.WriteLine($"Найдено {items.Count} предметов и {fluids.Count} жидкостей.");

            return new ItemsAndFluidsData { Items = items, Fluids = fluids };
        }

        /// <summary>
        /// Извлекает ID предмета из имени PNG-файла в формате IconExporter.
        /// </summary>
        /// <param name="fileName">Имя файла без расширения.</param>
        /// <returns>ID в формате modid:itemid или modid:fluidid.</returns>
        private static string ExtractIdFromFileName(string fileName)
        {
            // Регулярное выражение для IconExporter: modid__name__.png
            // Или fluid__modid__name__.png
            // Должно захватывать всё после первого "__" до ".png" (или до "__" перед NBT)
            string pattern = @"^(?:fluid__)?([^_]+)__([^_.]+(?:_[^_.]+)*)(?:\.png)?$"; // Лучше уточнить под конкретный формат

            // Или проще, через Split:
            string[] parts = fileName.Split(new[] { "__" }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return "";

            if (parts[0] == "fluid")
            {
                if (parts.Length >= 3)
                {
                    // fluid__modid__name__metadata
                    string modId = parts[1];
                    string namePart = parts[2]; // Берём только следующую часть, нужно учитывать, что может быть больше
                                                // Для начала, если metadata нет, то: namePart = parts[2]
                                                // Если metadata есть (например, metadata = parts[3]), то его игнорируем
                                                // Примеры: fluid__tfmg__crude_oil.png -> tfmg:crude_oil
                                                // fluid__tfmg__flowing_crude_oil.png -> tfmg:crude_oil (но filtered out by other logic)
                                                // Более точно: string itemName = string.Join("_", parts.Skip(2).TakeWhile(p => !p.StartsWith("{")));
                    int nameEndIdx = Array.FindIndex(parts, 2, p => p.StartsWith("{")); // Ищем начало NBT
                    if (nameEndIdx == -1) nameEndIdx = parts.Length; // Если NBT нет, до конца
                    string itemName = string.Join("_", parts.Skip(2).Take(nameEndIdx - 2));
                    return $"{modId}:{itemName}";
                }
                return "";
            }
            else
            {
                // parts[0] = modid, остальные - имя предмета
                // Пример: minecraft__polished_basalt.png -> parts = ["minecraft", "polished", "basalt"]
                // Нужно: "minecraft:polished_basalt"
                // Опять же, учитываем NBT
                int nameEndIdx = Array.FindIndex(parts, 1, p => p.StartsWith("{")); // Ищем начало NBT
                if (nameEndIdx == -1) nameEndIdx = parts.Length; // Если NBT нет, до конца
                string itemName = string.Join("_", parts.Skip(1).Take(nameEndIdx - 1));
                string modId = parts[0];
                return $"{modId}:{itemName}";
            }
        }
    }

    public class MinecraftItem
    {
        public string Id { get; set; } = "";
        public string LocalizedName { get; set; } = "";
        public string ModName { get; set; } = "";
        public string Type { get; set; } = ""; // "item" или "fluid"
        public List<string> Tags { get; set; } = new();
        public byte[] ImageData { get; set; } = Array.Empty<byte>();
    }

    // Внутренний класс для хранения данных из metadata.json
    internal class MetadataEntry
    {
        public string Id { get; set; } = "";
        public string LocalName { get; set; } = "";
        public string ModName { get; set; } = "";
        public string Type { get; set; } = "";
        public List<string> Tags { get; set; } = new();
    }
}