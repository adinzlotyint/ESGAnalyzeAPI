using ESGAnalyzeAPI.Models;

namespace ESGAnalyzeAPI.Services {
    public interface IAnalyzerService {
        public ESGAnalysisResult Analyze(string text);
    }
    public class AnalyzeService : IAnalyzerService {
        private readonly IEnumerable<ICriterions> _analyzers;

        public AnalyzeService(IEnumerable<ICriterions> analyzers) {
            _analyzers = analyzers.ToList();
        }

        public ESGAnalysisResult Analyze(string reportText) {
            var result = new ESGAnalysisResult();

            foreach (var analyzer in _analyzers) {
                analyzer.Evaluate(reportText, result);
            }

            return result;
        }
    }
}
