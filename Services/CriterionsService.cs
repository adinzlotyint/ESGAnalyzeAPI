using System.Reflection;
using System.Text.RegularExpressions;
using ESGAnalyzeAPI.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ESGAnalyzeAPI.Services {
    public interface ICriterions {
        void Evaluate(string reportText, ESGAnalysisResult result);
    }

    public class CriterionConfig {
        required public string Id { get; set; }
        required public string Name { get; set; }
        required public List<string> IncludePatterns { get; set; }
        public List<string> ExcludePatterns { get; set; } = new();
        public string SectionHeader { get; set; } = "";
        required public Dictionary<string, double> ScoreMapping { get; set; }
    }

    public class ConfigRoot {
        required public List<CriterionConfig> Criteria { get; set; }
    }

    public class ConfigLoader {
        
        public List<CriterionConfig> LoadFromFile(string filePath) {
            
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Brak pliku z kryteriami: {filePath}");

            var yaml = File.ReadAllText(filePath);
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
            var root = deserializer.Deserialize<ConfigRoot>(yaml);
            return root?.Criteria ?? new List<CriterionConfig>();
        }
    }

    public class YamlRuleAnalyzer : ICriterions {
        private readonly List<CriterionConfig> _configs;
        private readonly ILogger<AnalyzeService> _logger;
        private readonly Dictionary<string, PropertyInfo> _propertyCache = new();
        private readonly Dictionary<string, List<Regex>> _compiledIncludes = new();
        private readonly Dictionary<string, List<Regex>> _compiledExcludes = new();

        public YamlRuleAnalyzer(ILogger<AnalyzeService> logger) {
            _logger = logger;
            var yamlPath = Path.Combine(AppContext.BaseDirectory, "Services\\criteria.yaml");
            _configs = new ConfigLoader().LoadFromFile(yamlPath);

            foreach (var cfg in _configs) {
                var key = cfg.Id + cfg.Name;

                _compiledIncludes[key] = cfg.IncludePatterns?
                    .Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline))
                    .ToList() ?? new List<Regex>();

                _compiledExcludes[key] = cfg.ExcludePatterns?
                    .Select(p => new Regex(p, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline))
                    .ToList() ?? new List<Regex>();
            }

            var props = typeof(ESGAnalysisResult).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var cfg in _configs) {
                var prop = props.FirstOrDefault(p =>
                    p.Name.StartsWith(cfg.Id, StringComparison.OrdinalIgnoreCase) &&
                    p.Name.EndsWith("Score", StringComparison.OrdinalIgnoreCase) &&
                    p.PropertyType == typeof(double));
                if (prop != null)
                    _propertyCache[cfg.Id] = prop;
                else
                    _logger.LogWarning("Nie znaleziono właściwości dla {Id}", cfg.Id);
            }
        }

        public void Evaluate(string reportText, ESGAnalysisResult result) {
            var groupedById = _configs
                .GroupBy(c => c.Id)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var criterionId in groupedById.Keys) {
                var variants = groupedById[criterionId];

                double? bestScore = null;

                foreach (var cfg in variants) {
                    string key = cfg.Id + cfg.Name;

                    if (!_compiledIncludes.TryGetValue(key, out var includes))
                        includes = new List<Regex>();

                    if (!_compiledExcludes.TryGetValue(key, out var excludes))
                        excludes = new List<Regex>();

                    if (excludes.Any(rx => rx.IsMatch(reportText)))
                        continue;

                    if (includes.All(rx => rx.IsMatch(reportText))) {
                        double score = cfg.ScoreMapping?.GetValueOrDefault("default", 1.0) ?? 1.0;
                        bestScore = Math.Max(bestScore ?? 0.0, score);
                    }
                }
                if (bestScore.HasValue) {
                    SetScore(result, criterionId, bestScore.Value);
                }
            }
        }
        private void SetScore(ESGAnalysisResult result, string id, double score) {
            if (_propertyCache.TryGetValue(id, out var prop)) {
                prop.SetValue(result, score);
            }
        }
    }
}
