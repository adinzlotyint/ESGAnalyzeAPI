using ESGAnalyzeAPI.Models;
using System.Text.RegularExpressions;

namespace ESGAnalyzeAPI.Services {
    public interface ICriterions {
        void Evaluate(string reportText, ESGAnalysisResult result);
    }

    public abstract class BaseRuleAnalyzer : ICriterions {
        protected static ILogger Logger { get; private set; }
        private readonly (Regex Rx, double Score)[] _compiled;
        private readonly string[]? _mustContain;

        protected BaseRuleAnalyzer((string pattern, double score)[] rawPatterns,params string[] mustContain) {
            var opts = RegexOptions.IgnoreCase
                     | RegexOptions.Singleline
                     | RegexOptions.IgnorePatternWhitespace
                     | RegexOptions.CultureInvariant
                     | RegexOptions.Compiled; // ⇦ key for perf

            _compiled = rawPatterns.Select(p => (new Regex(p.pattern, opts), p.score))
                                    .ToArray();

            _mustContain = mustContain?.Length == 0 ? null : mustContain;
        }
        public static void SetLogger(ILogger logger) => Logger = logger;
        public abstract void Evaluate(string reportText, ESGAnalysisResult result);

        protected double MatchFirstScore(string text) {
            // ---------- super‑cheap gate ----------
            if (_mustContain is not null && !_mustContain.Any(t =>
                    text.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)) {
                return 0.0;
            }

            // ---------- regex loop ----------
            int idx = 1;
            foreach (var (rx, score) in _compiled) {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                bool ok = rx.IsMatch(text);
                sw.Stop();

                Logger?.LogInformation("{Analyzer} ‑ pattern #{Idx}  → {Hit}  ({Ms} ms)",
                    GetType().Name, idx, ok ? "HIT" : "miss", sw.ElapsedMilliseconds);

                if (ok) return score;
                idx++;
            }
            return 0.0;
        }
    }

    public sealed class C1PolicyStrategyAnalyzer : BaseRuleAnalyzer {
        private static readonly (string, double)[] _patterns =
        {
            // 1.0 – samodzielna polityka, publicznie dostępna
            ("(?isx)\\b(polityk[aię]|strategi(?:a|e|ę)|policy|strategy)\\b"
           + ".{0,120}\\b(klimat\\w*|klimatyczn\\w*|zmian\\W+klimatu|climate\\W*change|net\\W*zero)\\b"
           + ".*?\\b(publiczn\\w*|publicly\\s+available|available\\s+online|website)\\b", 1.0),

            // 0.5 – deklaracja posiadania
            ("(?isx)(posiad\\w*|have|implemented)\\b.{0,60}(polityk[aię]|strategi[aeę]|policy|strategy)"
           + ".{0,120}(klimat\\w*|climate)", 0.5)
        };

        public C1PolicyStrategyAnalyzer() : base(_patterns, "klimat", "climate", "policy") { }

        public override void Evaluate(string txt, ESGAnalysisResult res) =>
            res.C1PolicyStrategyScore = MatchFirstScore(txt);
    }

    public sealed class C2RisksAndChancesAnalyzer : BaseRuleAnalyzer {
        private static readonly string RISK = "(ryzyk\\w*|risk[s]?)";
        private static readonly string CHANCE = "(szans\\w*|możliwoś\\w*|opportunit(?:y|ies))";
        private static readonly string CLIMATE = "(klimat\\w*|climate\\W*change|net\\W*zero|decarboni[sz]ation)";
        private static readonly string SIGNIF = "(istotn\\w*|znacząc\\w*|material|significant)";
        private static readonly string IMPACT = "(wpływ\\w*|impact)";
        private static readonly string FIN = "(finansow\\w*|wynik\\w*\\s+finans\\w*|business\\s+strategy)";
        private static readonly string MANAGE = "(zarządz\\w*|manage|mitigat\\w*|monitor\\w*|działan\\w*)";

        private static readonly (string, double)[] _p =
        {
            // 1.0 – risks+chances+significant impact
            ($"(?isx)\\b{RISK}\\b(?=.{{0,4000}}\\b{CHANCE}\\b)(?=.{{0,4000}}\\b{CLIMATE}\\b)(?=.{{0,4000}}\\b{SIGNIF}\\b)(?=.{{0,4000}}\\b{IMPACT}\\b)(?=.{{0,4000}}\\b{FIN}\\b)", 1.0),
            // 1.0 – explicitly *no* significant impact
            ($"(?isx)\\b{RISK}\\b(?=.{{0,4000}}\\b{CHANCE}\\b)(?=.{{0,4000}}\\b{CLIMATE}\\b)(?=.{{0,4000}}\\b(nie\\s+mają|nie\\s+ma|nie\\s+będą|no|not)\\b.*?\\b{SIGNIF}\\b)", 1.0),
            // 0.67 – management actions described
            ($"(?isx)\\b{RISK}\\b(?=.{{0,4000}}\\b{CHANCE}\\b)(?=.{{0,4000}}\\b{CLIMATE}\\b)(?=.{{0,4000}}\\b{MANAGE}\\b)", 0.67),
            // 0.33 – mention of either risk or chance
            ($"(?isx)\\b({RISK}|{CHANCE})\\b(?=.{{0,4000}}\\b{CLIMATE}\\b)", 0.33)
        };

        public C2RisksAndChancesAnalyzer() : base(_p, "risk", "ryzyk", "szans", "klimat") { }
        public override void Evaluate(string t, ESGAnalysisResult r) => r.C2RisksAndChancesScore = MatchFirstScore(t);
    }

    public sealed class C3ClimateRiskAnalyzer : BaseRuleAnalyzer {
        private const string CLIMATE = "(klimat\\w*|climate\\W*change|climate)";
        private const string RESPS = "(odpowiad\\w*|nadzoruj\\w*|zarządz\\w*|koordynuj\\w*|responsible\\s+for|oversee[s]?|manage[s]?|lead[s]?)";
        private const string BOARD_MEMBER = "(członek\\w*\\s+(zarządu|rady\\s+nadzorczej|rady\\s+dyrektorów)|member\\s+of\\s+the\\s+(board\\s+of\\s+directors|supervisory\\s+board))";
        private const string BOARD = "(zarząd|rada\\s+nadzorcza|rada\\s+dyrektorów|board\\s+of\\s+directors|supervisory\\s+board)";
        private const string NOT_WHOLE = "(?!.*?\\b(cały|cała|all|whole)\\s+" + BOARD + "\\b)";
        private const string COMMITTEE = "(zespół|komitet|komisja|committee)";
        private const string MANAGER = "(menadżer\\w*|manager|kierownik\\w*|dyrektor\\w*|director|pełnomocnik\\w*|officer|chief\\s+sustainability\\s+officer|head\\s+of\\s+sustainability|CSO)";

        private static readonly (string, double)[] _p =
        {
            // 1.0 – named board member
            ($"(?isx){NOT_WHOLE}\\b{BOARD_MEMBER}\\b.*?\\b{RESPS}\\b.*?\\b{CLIMATE}\\b", 1.0),
            // 1.0 – climate committee with board member
            ($"(?isx)\\b{COMMITTEE}\\b.*?\\b{CLIMATE}\\b.*?\\b(w\\s+skład|includes|consist[s]?\\s+of)\\b.*?{BOARD_MEMBER}", 1.0),
            // 0.5 – dedicated manager / CSO
            ($"(?isx)\\b{MANAGER}\\b.*?\\b{RESPS}\\b.*?\\b{CLIMATE}\\b", 0.5),
            // 0.5 – collective oversight
            ($"(?isx)\\b{BOARD}\\b.*?\\b(nadzoruj\\w*|monitoruj\\w*|koordynuj\\w*|oversee|monitor)\\b.*?\\b{CLIMATE}\\b", 0.5)
        };

        public C3ClimateRiskAnalyzer() : base(_p, "klimat", "climate", "zarząd", "board") { }
        public override void Evaluate(string t, ESGAnalysisResult r) => r.C3ClimateRiskScore = MatchFirstScore(t);
    }
    public sealed class C4EmissionsScopeAnalyzer : BaseRuleAnalyzer {
        private const string S1 = "\\b(scope|zakres|zakresie|scope\\s*I)\\s*1\\b";
        private const string S2 = "\\b(scope|zakres|zakresie|scope\\s*II?)\\s*2\\b";
        private const string S3 = "\\b(scope|zakres|zakresie|scope\\s*III?)\\s*3\\b";

        private static readonly (string, double)[] _p =
        {
            ($"(?isx)(?=.{{0,6000}}{S1})(?=.{{0,6000}}{S2})(?=.{{0,6000}}\\blocation[-\\s]?based\\b)(?=.{{0,6000}}\\bmarket[-\\s]?based\\b)(?=.{{0,6000}}{S3})(?=.{{0,6000}}\\b(kategor\\w*|obszar\\w*|category|area)\\b)", 1.0),
            ($"(?isx)(?=.{{0,6000}}{S1})(?=.{{0,6000}}{S2})(?=.{{0,6000}}{S3})(?=.{{0,6000}}\\b(kategor\\w*|obszar\\w*|category|area)\\b)", 0.75),
            ($"(?isx)(?=.{{0,6000}}{S1})(?=.{{0,6000}}{S2})(?=.{{0,6000}}{S3})(?=.{{0,6000}}\\b(częściow\\w*|partial)\\b)", 0.75),
            ($"(?isx)(?=.{{0,6000}}{S1})(?=.{{0,6000}}{S2})(?=.{{0,6000}}{S3})", 0.50),
            ($"(?isx)(?=.{{0,6000}}{S1})(?=.{{0,6000}}{S2})(?=.{{0,6000}}\\blocation[-\\s]?based\\b)(?=.{{0,6000}}\\bmarket[-\\s]?based\\b)", 0.50),
            ($"(?isx)(?=.{{0,6000}}{S1})(?=.{{0,6000}}{S2})", 0.25)
        };
        public C4EmissionsScopeAnalyzer() : base(_p, "scope", "zakres", "location-based", "market-based") { }
        public override void Evaluate(string t, ESGAnalysisResult r) => r.C4EmissionsScopeScore = MatchFirstScore(t);
    }

    public sealed class C5EmissionsBoundaryAnalyzer : BaseRuleAnalyzer {
        private const string E = "\\b(emisj\\w*|emission[s]?)\\b";
        private const string FULL = "\\b(cał[aeiy]\\w*\\s+(GK|grup\\w+\\s+kapitałow\\w*|grup\\w*|consolidated\\s+group)|all\\s+subsidiar(?:y|ies)|wszystk\\w*\\s+jednostk\\w*\\s+zależn\\w*|entire\\s+(group|organisation|organization))\\b";
        private const string CONTROL = "\\b(kontrol\\w*\\s+(operacyjn\\w*|finansow\\w*|equity)|operational\\s+control|financial\\s+control|equity\\s+share)\\b";
        private const string MAT = "\\b(istotn\\w*|material(?:ity)?|znacząc\\w*)\\b";
        private const string CRIT = "\\b(kryteri\\w*|criteria|criterion|wyjaśni\\w*|explain\\w*)\\b";
        private const string SEL = "\\b(wybran\\w*|selected|największ\\w*|largest|major)\\b";
        private const string SUB = "\\b(jednostk\\w*|subsidiar(?:y|ies)|unit[s]?|spółek|companies)\\b";
        private const string S3 = "\\b(scope|zakres|scope\\s*III?)\\s*3\\b";

        private static readonly (string, double)[] _p =
        {
            ($"(?isx)(?=.{{0,6000}}{E})(?=.{{0,6000}}({FULL}|{CONTROL}))", 1.0),
            ($"(?isx)(?=.{{0,6000}}{E})(?=.{{0,6000}}{SEL})(?=.{{0,6000}}{SUB})(?=.{{0,6000}}{MAT})(?=.{{0,6000}}{CRIT})", 1.0),
            ($"(?isx)(?=.{{0,6000}}{E})(?=.{{0,6000}}{SEL})(?=.{{0,6000}}{SUB})(?!.*?{CRIT})", 0.67),
            ($"(?isx)(?=.{{0,800}}{S3})(?=.{{0,800}}{SEL})(?=.{{0,800}}\\b(kategor\\w*|obszar\\w*|category|area)\\b)", 0.50),
            ($"(?isx)(?=.{{0,6000}}{E})(?=.{{0,6000}}\\b(jednostk\\w*\\s+zależn\\w*|subsidiar(?:y|ies)|spółk\\w*\\s+dominuj\\w*|parent\\s+company)\\b)", 0.33)
        };
        public C5EmissionsBoundaryAnalyzer() : base(_p, "emisj", "emission") { }
        public override void Evaluate(string t, ESGAnalysisResult r) => r.C5EmissionsBoundaryScore = MatchFirstScore(t);
    }

    public sealed class C6CalculationStandardAnalyzer : BaseRuleAnalyzer {
        private const string E = "\\b(emisj\\w*|emission[s]?|GHG|gaz)";
        private const string STD = "\\b(ISO\\s*1406[4-7]|ISO\\s*1406[57]|GHG\\s+Protocol|Greenhouse\\s+Gas\\s+Protocol|Corporate\\s+Accounting\\s+and\\s+Reporting\\s+Standard|WRI|WBCSD|PCAF|PAS\\s*2050|IPCC\\s+Guidelines|wytyczn\\w*\\s+IPCC)\\b";
        private static readonly (string, double)[] _p =
        {
            ($"(?isx)(?=.{{0,4000}}{E})(?=.{{0,4000}}{STD})", 1.0)
        };
        public C6CalculationStandardAnalyzer() : base(_p, "GHG", "ISO", "Protocol", "IPCC") { }
        public override void Evaluate(string t, ESGAnalysisResult r) => r.C6CalculationStandardScore = MatchFirstScore(t);
    }

    public sealed class C7GwpSourcesAnalyzer : BaseRuleAnalyzer {
        private const string SRC = "\\b(źródł\\w*|source[s]?|IPCC|DEFRA|EPA|IEA|EEA|BEIS|KOBIZE|GEMIS|ecoinvent|GHG\\s*Protocol)\\b";
        private const string EF = "\\b(wskaźnik\\w*\\s+emisji|współczynnik\\w*\\s+emisji|emission\\s+factor[s]?)\\b";
        private const string GWP = "\\b(współczynnik\\w*\\s+GWP|GWP\\s+factor[s]?|global\\s+warming\\s+potential)\\b";
        private static readonly (string, double)[] _p =
        {
            ($"(?isx)(?=.{{0,4000}}{EF}.*?{SRC})(?=.{{0,4000}}{GWP}.*?{SRC})", 1.0)
        };
        public C7GwpSourcesAnalyzer() : base(_p, "GWP", "emission", "factor") { }
        public override void Evaluate(string t, ESGAnalysisResult r) => r.C7GwpSourcesScore = MatchFirstScore(t);
    }


    public sealed class C8EmissionsTrendAnalyzer : BaseRuleAnalyzer {
        private const string E = "\\b(emisj\\w*|emission[s]?|GHG|gaz)";
        private const string TR = "\\b(zmian\\w*|trend\\w*|spadk\\w*|wzrost\\w*|increase|decrease|YoY|r/r)\\b";
        private const string Y3 = "(?: .*? \\b20\\d{2}\\b ){3,}";
        private const string Y2 = "(?: .*? \\b20\\d{2}\\b ){2,}";
        private static readonly (string, double)[] _p =
        {
            ($"(?isx)(?=.{{0,5000}}{E})(?=.{{0,5000}}{TR})(?=.{{0,5000}}{Y3})", 1.0),
            ($"(?isx)(?=.{{0,5000}}{E})(?=.{{0,5000}}{TR})(?=.{{0,5000}}{Y2})", 0.5)
        };
        public C8EmissionsTrendAnalyzer() : base(_p, "emisj", "emission", "trend", "zmian") { }
        public override void Evaluate(string t, ESGAnalysisResult r) => r.C8EmissionsTrendScore = MatchFirstScore(t);
    }

    public sealed class C9IntensityIndicatorAnalyzer : BaseRuleAnalyzer {
        private const string E = "\\b(emisj|emission|GHG|carbon)";
        private const string UNIT = "(?:g|kg|Mg|t|kt|Mt|tony?)\\s*CO2(?:e|eq)?|CO₂(?:e|eq)?";
        private const string SEP = "(?: / | per\\b | na\\b | /y )";
        private const string WORD = "\\b(intensywno\\w*|intensity)\\b";
        private static readonly (string, double)[] _p =
        {
            ($"(?isx)(?=.{{0,5000}}{E})(?=.{{0,5000}}{WORD})(?=.*?{UNIT}.*?{SEP})", 1.0),
            ($"(?isx)(?=.{{0,5000}}{E})(?=.*?{UNIT}.*?{SEP})", 1.0)
        };
        public C9IntensityIndicatorAnalyzer() : base(_p, "CO2e", "intensywno", "intensity") { }
        public override void Evaluate(string t, ESGAnalysisResult r) => r.C9IntensityIndicatorScore = MatchFirstScore(t);
    }

    public sealed class C10NumericConsistencyAnalyzer : BaseRuleAnalyzer {
        private const string EM = @"\b(emisj|emission|GHG|carbon|śl[aą]d|footprint|scope)\b";
        private const string RED = @"\b(redukcj|ogranicz|zmniejsz|dekarboniz|cut|decrease|lower)\w*\b";
        private const string ACT = "\\b(działan|plan|roadmap|action|measure|program|investment|inwestycj)";
        private const string TAR = "\\b(target|cel|goal|objective)";
        private const string ABS = "\\b\\d{1,3}(?:[.,]\\d{1,3})?\\s*(?:t|kt|Mt|Mg)\\s*CO2(?:e|eq)?\\b";
        private const string PCT = "\\b\\d{1,3}\\s*%\\b";
        private const string INT = "\\b\\d+(?:[.,]\\d+)?\\s*(?:g|kg|t|Mg)\\s*CO2(?:e|eq)?\\s*(?:/|per\\b|na\\b)";
        private const string YEARS = @"(?s).*?\b20\d{2}\b.*?\b20\d{2}\b";
        private const string SEG = @"(?s:.{0,300})";

        private static readonly (string, double)[] _p =
        {
            ($@"(?isx)(?=.{{0,6000}}{EM})(?=.{{0,6000}}{YEARS})(?:{SEG}){TAR}.*?{RED}.*?(?:{PCT}|{ABS}).*?\.(?=.{{0,6000}}{ACT})", 1.0),
            ($@"(?isx)(?=.{{0,6000}}{EM})(?=.{{0,6000}}{YEARS})(?:{SEG}){TAR}.*?{RED}.*?(?:{PCT}|{ABS}).*?\.(?!.*?{ACT})", 0.67),
            ($@"(?isx)(?=.{{0,6000}}{EM})(?=.{{0,6000}}\\b20\\d{{2}}\\b)(?:{SEG}){TAR}.*?(?:{INT}|{PCT}).*?\.", 0.5),
            ($@"(?isx)(?=.{{0,6000}}{RED})(?=.{{0,6000}}{ACT})(?!.*?(?:{PCT}|{ABS}|{INT}))", 0.33)
        };
        public C10NumericConsistencyAnalyzer() : base(_p, "cel", "target", "redukc", "plan") { }
        public override void Evaluate(string t, ESGAnalysisResult r) => r.C10NumericConsistencyScore = MatchFirstScore(t);
    }

    public sealed class C11UnitCorrectnessAnalyzer : BaseRuleAnalyzer {
        private const string NUM = "\\d{1,3}(?:[\u00A0\u2007\u202F\\s'.]\\d{3})*(?:[.,]\\d+)?";
        private const string MASS = "(?:g|kg|Mg|t|kt|Mt|Gg|Tg|tonn?e?s?|tony)";
        private const string CO2E = @"(?:CO2e|CO₂e|CO2eq|CO₂eq|CO2[\u2011–\- ]?equivalent(?:s)?|CO₂[\u2011–\- ]?equivalent(?:s)?)";
        private const string IND = "(?: / | per\\b | na\\b | /y )";
        private static readonly (string, double)[] _p =
        {
            ($"(?isx){NUM}\\s*{MASS}\\s*{CO2E}|{CO2E}\\s*{IND}\\s*\\w+", 1.0)
        };
        public C11UnitCorrectnessAnalyzer() : base(_p, "CO2e", "CO2eq") { }
        public override void Evaluate(string t, ESGAnalysisResult r) => r.C11UnitCorrectnessScore = MatchFirstScore(t);
    }

    public sealed class C12KeywordPresenceAnalyzer : BaseRuleAnalyzer {
        private static readonly (string, double)[] _p =
        {
            ("(?isx)\\b(dwutlenek\\W+w[ęe]gla|gaz(?:y|ów)?\\W+cieplarnian\\w*|CO\\s*2|CO₂|CO2e|CO₂e|CO2eq|CO₂eq|zmian\\w*\\W+klimat\\w*|globaln\\w*\\W+ocieplen\\w*|ślad\\W+węglow\\w*|carbon\\W+dioxide|carbon\\W+footprint|greenhouse\\W+gas(?:es)?|climate\\W+change|global\\W+warming|\\bGHG\\b)\\b", 1.0)
        };
        public C12KeywordPresenceAnalyzer() : base(_p) { }
        public override void Evaluate(string t, ESGAnalysisResult r) => r.C12KeywordPresenceScore = MatchFirstScore(t);
    }
}
