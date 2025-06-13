using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.RegularExpressions;

namespace RgukDirectionsApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DirectionsController : ControllerBase
    {
        private static readonly string CsvPath = Path.Combine("Data", "napravleniya2024_full.csv");
        private static List<Direction> _directions;
        private static bool _loaded = false;

        public DirectionsController()
        {
            if (!_loaded)
            {
                _directions = LoadDirections();
                _loaded = true;
            }
        }

        [HttpGet]
        public IActionResult GetAll()
        {
            return Ok(_directions);
        }

        [HttpPost("filter")]
        public IActionResult Filter([FromBody] FilterRequest request)
        {
            // Поддержка фильтрации по нескольким ключевым словам (разделитель — запятая или пробел)
            List<string> areaKeywords = null;
            if (!string.IsNullOrWhiteSpace(request.Area))
            {
                areaKeywords = request.Area
                    .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim().ToLower())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }

            var filtered = _directions
                .Where(d =>
                    (request.Form == null || d.Form.Contains(request.Form, StringComparison.OrdinalIgnoreCase)) &&
                    (
                        areaKeywords == null ||
                        areaKeywords.Any(kw => d.Name.ToLower().Contains(kw))
                    ) &&
                    request.Subjects.All(s =>
                        d.Subjects.Any(ds => ds.Equals(s.Subject, StringComparison.OrdinalIgnoreCase))
                    )
                )
                .Select(d =>
                {
                    int userSum = request.Subjects.Sum(s => s.Score ?? 0);
                    bool passBudget = d.ScoreBudget != null && userSum >= d.ScoreBudget;
                    bool passPaid = d.ScorePaid != null && userSum >= d.ScorePaid;

                    string passType = null;
                    if (request.Subjects.Any(s => s.Score != null))
                    {
                        if (passBudget) passType = "бюджет";
                        else if (passPaid) passType = "платное";
                    }

                    return new
                    {
                        d.Code,
                        d.Name,
                        d.Form,
                        d.Places,
                        d.ScoreBudget,
                        d.ScorePaid,
                        d.Cost,
                        d.Subjects,
                        PassType = passType
                    };
                })
                .Where(d =>
                    !request.Subjects.Any(s => s.Score != null) || d.PassType != null
                )
                .ToList();

            return Ok(filtered);
        }

        private List<Direction> LoadDirections()
        {
            var list = new List<Direction>();
            if (!System.IO.File.Exists(CsvPath)) return list;
            var lines = System.IO.File.ReadAllLines(CsvPath, Encoding.UTF8);
            foreach (var line in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split(';');
                if (parts.Length < 8) continue;

                var subjects = parts[7]
                    .Split(new[] { "или", ",", ";" }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Trim())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();

                list.Add(new Direction
                {
                    Code = parts[0].Trim(),
                    Name = parts[1].Trim(),
                    Form = parts[2].Trim(),
                    Places = int.TryParse(parts[3].Trim(), out var p) ? p : (int?)null,
                    ScoreBudget = int.TryParse(parts[4].Trim(), out var sb) ? sb : (int?)null,
                    ScorePaid = int.TryParse(parts[5].Trim(), out var sp) ? sp : (int?)null,
                    Cost = int.TryParse(parts[6].Replace(" ", "").Trim(), out var c) ? c : (int?)null,
                    Subjects = subjects
                });
            }
            return list;
        }

        public class Direction
        {
            public string Code { get; set; }
            public string Name { get; set; }
            public string Form { get; set; }
            public int? Places { get; set; }
            public int? ScoreBudget { get; set; }
            public int? ScorePaid { get; set; }
            public int? Cost { get; set; }
            public List<string> Subjects { get; set; }
        }

        public class SubjectScore
        {
            public string Subject { get; set; }
            public int? Score { get; set; }
        }

        public class FilterRequest
        {
            public List<SubjectScore> Subjects { get; set; } = new();
            public string? Area { get; set; }
            public string? Form { get; set; }
        }
    }
} 