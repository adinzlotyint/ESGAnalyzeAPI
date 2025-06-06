namespace ESGAnalyzeAPI.Models {
    public class ESGAnalysisResult {
        public double C1PolicyStrategyScore { get; set; }
        public double C2RisksAndChancesScore { get; set; }
        public double C3ClimateRiskScore { get; set; }
        public double C4EmissionsScopeScore { get; set; }
        public double C5EmissionsBoundaryScore { get; set; }
        public double C6CalculationStandardScore { get; set; }
        public double C7GwpSourcesScore { get; set; }
        public double C8EmissionsTrendScore { get; set; }
        public double C9IntensityIndicatorScore { get; set; }
        public double C10NumericConsistencyScore { get; set; }
        public double C11UnitCorrectnessScore { get; set; }
        public double C12KeywordPresenceScore { get; set; }
        public double GetTotalScore() {
            return
                C1PolicyStrategyScore +
                C2RisksAndChancesScore +
                C3ClimateRiskScore +
                C4EmissionsScopeScore +
                C5EmissionsBoundaryScore +
                C6CalculationStandardScore +
                C7GwpSourcesScore +
                C8EmissionsTrendScore +
                C9IntensityIndicatorScore +
                C10NumericConsistencyScore;
        }
    }
}
