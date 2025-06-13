using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using HtmlAgilityPack;
using System.Linq;

class Program
{
    static async Task Main(string[] args)
    {
        string urlStat = "https://rguk.ru/applicant/statistika-prijoma-2024/";
        string urlEge = "https://rguk.ru/applicant/admission-rules/bakalavriat/exams/";
        string outputPath = "napravleniya2024_full.csv";

        Console.OutputEncoding = Encoding.UTF8;

        // 1. Парсим таблицу с предметами ЕГЭ (по коду направления)
        var codeToEge = await ParseEgeSubjects(urlEge);

        // 2. Парсим основную таблицу и объединяем с предметами ЕГЭ
        var statRows = await ParseStatistika(urlStat, codeToEge);

        // 3. Сохраняем результат
        File.WriteAllText(outputPath, statRows, Encoding.UTF8);
        Console.WriteLine($"Готово! Данные сохранены в {outputPath}");
    }

    static async Task<Dictionary<string, string>> ParseEgeSubjects(string url)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var html = await DownloadHtmlAsync(url);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var table = doc.DocumentNode.SelectSingleNode("//table");
        var rows = table.SelectNodes(".//tr");
        foreach (var row in rows.Skip(1))
        {
            var cells = row.SelectNodes(".//td");
            if (cells != null && cells.Count >= 5)
            {
                string code = Clean(cells[2].InnerText);            // 3-й столбец — код направления
                string codeNorm = NormalizeCode(code);
                string direction = Clean(cells[1].InnerText);       // 2-й столбец — название направления
                string directionNorm = Normalize(direction);
                string ege = Clean(cells[4].InnerText);             // 5-й столбец — предметы ЕГЭ

                var subjects = ege
                    .Split(new[] { "или", ",", ";" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                // Добавляем "Русский язык", если его нет
                if (!subjects.Any(s => s.Equals("Русский язык", StringComparison.OrdinalIgnoreCase)))
                    subjects.Add("Русский язык");

                // Проверяем ключевые слова в названии направления
                string directionLower = direction.ToLower();
                bool needMathProfile =
                    directionLower.Contains("математика") ||
                    directionLower.Contains("информатика") ||
                    directionLower.Contains("техн");

                if (needMathProfile)
                {
                    // Ищем предмет с ключевым словом "математика"
                    bool foundMath = false;
                    for (int i = 0; i < subjects.Count; i++)
                    {
                        if (subjects[i].ToLower().Contains("математика"))
                        {
                            subjects[i] = "Математика профиль";
                            foundMath = true;
                        }
                    }
                    // Если не было ни одного предмета с "математика" — добавляем "Математика профиль"
                    if (!foundMath)
                        subjects.Add("Математика профиль");
                }
                else
                {
                    // Если нет ни "Математика профиль", ни "Математика", ни "Математика база" — добавить "Математика база"
                    if (!subjects.Any(s =>
                        s.Equals("Математика профиль", StringComparison.OrdinalIgnoreCase) ||
                        s.Equals("Математика", StringComparison.OrdinalIgnoreCase) ||
                        s.Equals("Математика база", StringComparison.OrdinalIgnoreCase)))
                    {
                        subjects.Add("Математика база");
                    }
                }

                ege = string.Join(", ", subjects);

                if (!string.IsNullOrWhiteSpace(codeNorm) && !result.ContainsKey(codeNorm))
                    result[codeNorm] = ege;
                if (!string.IsNullOrWhiteSpace(directionNorm) && !result.ContainsKey(directionNorm))
                    result[directionNorm] = ege;
            }
        }
        return result;
    }

    static async Task<string> ParseStatistika(string url, Dictionary<string, string> codeToEge)
    {
        var html = await DownloadHtmlAsync(url);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var table = doc.DocumentNode.SelectSingleNode("//table");
        var rows = table.SelectNodes(".//tr");
        var sb = new StringBuilder();
        sb.AppendLine("код;направление;форма;места;балл_бюджет;балл_платно;стоимость;предметы_егэ");

        foreach (var row in rows.Skip(1))
        {
            var cells = row.SelectNodes(".//td");
            if (cells != null && cells.Count >= 9)
            {
                string code = Clean(cells[0].InnerText);         // 1-й столбец
                string codeNorm = NormalizeCode(code);
                string direction = Clean(cells[1].InnerText);    // 2-й столбец
                string directionNorm = Normalize(direction);
                string form = Clean(cells[3].InnerText);         // 4-й столбец
                string places = Clean(cells[4].InnerText);       // 5-й столбец
                string scoreBudget = Clean(cells[6].InnerText);  // 7-й столбец
                string scorePaid = Clean(cells[7].InnerText);    // 8-й столбец
                string cost = Clean(cells[8].InnerText);         // 9-й столбец

                // Находим предметы ЕГЭ по коду направления
                string ege = "";
                if (!string.IsNullOrWhiteSpace(codeNorm) && codeToEge.TryGetValue(codeNorm, out var subjByCode))
                {
                    ege = subjByCode;
                }
                else
                {
                    // Если не найдено по коду, ищем по названию направления (мягкое сравнение)
                    if (!string.IsNullOrWhiteSpace(directionNorm) && codeToEge.TryGetValue(directionNorm, out var subjByDir))
                    {
                        ege = subjByDir;
                    }
                    else
                    {
                        // Ещё более мягкое сравнение: ищем частичное совпадение
                        foreach (var kv in codeToEge)
                        {
                            if (kv.Key.Length > 3 && (kv.Key.Contains(directionNorm) || directionNorm.Contains(kv.Key)))
                            {
                                ege = kv.Value;
                                break;
                            }
                        }
                    }
                }

                sb.AppendLine($"{code};{direction};{form};{places};{scoreBudget};{scorePaid};{cost};{ege}");
            }
        }
        return sb.ToString();
    }

    static async Task<string> DownloadHtmlAsync(string url)
    {
        using (var client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; RgukParser/1.0)");
            try
            {
                return await client.GetStringAsync(url);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки: {ex.Message}");
                return null;
            }
        }
    }

    static string Clean(string input)
    {
        return System.Net.WebUtility.HtmlDecode(input)
            .Replace("\n", " ")
            .Replace("\r", " ")
            .Replace("\t", " ")
            .Replace("&nbsp;", " ")
            .Trim();
    }

    static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "";
        return code
            .Replace(" ", "")
            .Replace("\u00A0", "") // неразрывный пробел
            .Replace(".", "")
            .Trim()
            .ToLower();
    }

    static string Normalize(string s)
    {
        return s.ToLower()
            .Replace(" ", "")
            .Replace("\u00A0", "")
            .Replace("ё", "е")
            .Replace("-", "")
            .Replace("–", "")
            .Replace("—", "")
            .Replace(".", "")
            .Trim();
    }
}