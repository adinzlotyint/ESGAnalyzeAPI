using ESGAnalyzeAPI.Models;
using Microsoft.Extensions.Logging;

namespace ESGAnalyzeAPI.Services {
    public interface IAnalyzerService {
        public ESGAnalysisResult Analyze(string text);
    }
    public class AnalyzeService : IAnalyzerService {
        private readonly IEnumerable<ICriterions> _analyzers;
        private readonly ILogger<AnalyzeService> _logger;

        public AnalyzeService(IEnumerable<ICriterions> analyzers, ILogger<AnalyzeService> logger) {
            _analyzers = analyzers.ToList();
            _logger = logger;
        }

        public ESGAnalysisResult Analyze(string reportText) {
            BaseRuleAnalyzer.SetLogger(_logger);
            var result = new ESGAnalysisResult();

            foreach (var analyzer in _analyzers) {
                analyzer.Evaluate(reportText, result);
            }

            return result;
        }
    }
}
