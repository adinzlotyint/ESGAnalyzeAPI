using ESGAnalyzeAPI.Models;
using System.Text.RegularExpressions;

namespace ESGAnalyzeAPI.Services {
    public interface ICriterions {
        void Evaluate(string reportText, ESGAnalysisResult result);
    }

    public abstract class BaseRuleAnalyzer : ICriterions {

        public abstract void Evaluate(string reportText, ESGAnalysisResult result);

        protected double MatchFirstScore(string text, IEnumerable<(string Pattern, double Score)> patterns) {
            var opts = RegexOptions.IgnoreCase
                        | RegexOptions.Singleline
                        | RegexOptions.IgnorePatternWhitespace
                        | RegexOptions.CultureInvariant;

            foreach (var (pattern, score) in patterns) {
                if (Regex.IsMatch(text, pattern, opts))
                    return score;
            }
            return 0.0;
        }
    }

    public class C1PolicyStrategyAnalyzer : BaseRuleAnalyzer {

        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            var patterns = new List<(string Pattern, double Score)>
            {
            (
                // 1a) publicznie / publicly dostępny dokument
                @"(?is)\b(polityk[aię]|strategi[aię]|policy|strategy)\b
                    .{0,40}
                    \b(klimat\w*|zmian\W+klimatu|climate\W*change|decarboni[sz]ation|net\W*zero|neutraln\w*|carbon\W*neutral)\b
                    .*?
                    \b(publiczn\w*\s+dostęp\w*|publicly\s+available|available\s+online|publicly\s+disclosed|dostępna\s+online|udostępnion\w*)\b",
                1.0
            ),
            (
                // 1b) założenia/assumptions + cele/goals + działania/actions
                @"(?is)\b(polityk[aię]|strategi[aię]|policy|strategy)\b
                    (?=.*?\b(klimat\w*|zmian\W+klimatu|climate\W*change|decarboni[sz]ation|net\W*zero|neutraln\w*|carbon\W*neutral)\b)
                    (?=.*?\b(założen\w*|assumptions?)\b)
                    (?=.*?\b(cel(e|ów)|goals?|objectives?)\b)
                    (?=.*?\b(działan\w*|actions?|measures?)\b)
                    .{0,120}",
                1.0
            ),

            // ─────────────── 0,5 pkt ───────────────
            (
                // 0,5a) ogólne stwierdzenie posiadania dokumentu
                @"(?is)(posiad(a|amy|anie)\w*|wdrożon\w*|opracowan\w*|realizuj\w*|
                        have|has|maintain|maintains|implemented|adopted|prepared)
                    .{0,30}
                    (polityk[aię]|strategi[aię]|policy|strategy)
                    .{0,40}
                    (klimat\w*|zmian\W+klimatu|climate\W*change|decarboni[sz]ation|net\W*zero|neutraln\w*|carbon\W*neutral)",
                0.5
            ),
            (
                // 0,5b) klimat w strategii biznesowej / business strategy
                @"(?is)\b(strategi[aię]\s+biznesow\w*|business\s+strategy)\b
                    .{0,60}
                    (zagadnienia|kwestie|ryzyka|szanse|topics?|issues|risks?|opportunit(?:y|ies))
                    .{0,20}
                    (klimat\w*|zmian\W+klimatu|climate\W*change|decarboni[sz]ation|net\W*zero|neutraln\w*|carbon\W*neutral)",
                0.5
            )
        };

            result.C1PolicyStrategyScore = MatchFirstScore(reportText, patterns);
        }
    }

    public class C2RisksAndChancesAnalyzer : BaseRuleAnalyzer {

        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            var patterns = new List<(string Pattern, double Score)>
            {
            // ───────────────────────── 1,00 pkt ─────────────────────────
            (
                // 1a) Ryzyka + szanse + istotny wpływ na finanse/strategię
                @"(?isx)
                    \b(ryzyk\w*|risks?)\b
                    (?=.*?\b(szans\w*|możliwoś\w*|opportunit(?:y|ies))\b)
                    (?=.*?\b(klimat\w*|zmian\W+klimatu|climate\W*change|decarboni[sz]ation|net\W*zero|carbon\W*neutral)\b)
                    (?=.*?\b(istotn\w*|material|significant|kluczow\w*)\b)
                    (?=.*?\b(wpływ\w*|impact)\b)
                    (?=.*?\b(finansow\w*|wynik\w*\s+finans\w*|strategi\w*\s+biznes\w*|business\s+strategy)\b)
                    .{0,150}",
                1.0
            ),
            (
                // 1b) Informacja, że zidentyfikowane ryzyka i szanse NIE mają istotnego wpływu
                @"(?isx)
                    \b(ryzyk\w*|risks?)\b
                    (?=.*?\b(szans\w*|możliwoś\w*|opportunit(?:y|ies))\b)
                    (?=.*?\b(klimat\w*|zmian\W+klimatu|climate\W*change|decarboni[sz]ation|net\W*zero|carbon\W*neutral)\b)
                    (?=.*?\b(nie\s+mają|nie\s+ma|no)\b.*?\b(istotn\w*|material|significant)\b.*?\b(wpływ\w*|impact)\b)
                    .{0,150}",
                1.0
            ),

            // ───────────────────────── 0,67 pkt ─────────────────────────
            (
                // Ryzyka + szanse + sposoby zarządzania (manage / mitigation)
                @"(?isx)
                    \b(ryzyk\w*|risks?)\b
                    (?=.*?\b(szans\w*|możliwoś\w*|opportunit(?:y|ies))\b)
                    (?=.*?\b(klimat\w*|zmian\W+klimatu|climate\W*change|decarboni[sz]ation|net\W*zero|carbon\W*neutral)\b)
                    (?=.*?\b(zarządz|manage|management|mitigat\w*|łagodzen\w*|response|odpowied\w*|działan\w*)\b)
                    .{0,150}",
                0.67
            ),

            // ───────────────────────── 0,33 pkt ─────────────────────────
            (
                // Ryzyka LUB szanse związane z klimatem
                @"(?isx)
                    \b((ryzyk\w*|risks?)|(szans\w*|możliwoś\w*|opportunit(?:y|ies)))\b
                    (?=.*?\b(klimat\w*|zmian\W+klimatu|climate\W*change|decarboni[sz]ation|net\W*zero|carbon\W*neutral)\b)
                    .{0,120}",
                0.33
            )
        };

            result.C2RisksAndChancesScore = MatchFirstScore(reportText, patterns);
        }
    }

    public class C3ClimateRiskAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            var patterns = new List<(string Pattern, double Score)>
            {
            // ───────────────────────── 1,00 pkt ─────────────────────────
            (
                // 1a) Konkretny członek zarządu / RN wskazany jako odpowiedzialny za klimat
                @"(?isx)
                    (?!.*\b(cały|cała|all|whole)\s+(zarząd|board|rada\s+nadzorcza|supervisory\s+board)\b)
                    \b
                    (   członek\w*\s+(zarządu|rady\s+nadzorczej)
                    | member\s+of\s+the\s+(board\s+of\s+directors|supervisory\s+board)
                    )\b
                    .*?
                    \b(odpowiad\w*|nadzoruj\w*|zarządz\w*|responsible\s+for|oversee[s]?|manage[s]?)\b
                    .*?
                    \b(klimat\w*|zmian\W+klimatu|climate\W*change|climate)\b",
                1.0
            ),
            (
                // 1b) Zespół / komitet dot. klimatu z udziałem członka zarządu / RN
                @"(?isx)
                    \b(zespół|komitet|komisja|committee)\b
                    .*?
                    \b(klimat\w*|climate)\b
                    .*?
                    \b(w\s+skład|includes|consist[s]?\s+of)\b
                    .*?
                    (   członek\w*\s+(zarządu|rady\s+nadzorczej)
                    | member\s+of\s+the\s+(board|supervisory\s+board)
                    )",
                1.0
            ),

            // ───────────────────────── 0,50 pkt ─────────────────────────
            (
                // Menadżer / dyrektor (poziom poniżej zarządu) odpowiedzialny za klimat
                @"(?isx)
                    \b(menadżer\w*|manager|kierownik\w*|dyrektor\w*|director
                    |chief\s+sustainability\s+officer|head\s+of\s+sustainability|CSO)\b
                    .*?
                    \b(odpowiad\w*|nadzoruj\w*|zarządz\w*|responsible\s+for|oversee[s]?|manage[s]?)\b
                    .*?
                    \b(klimat\w*|zmian\W+klimatu|climate\W*change|climate)\b",
                0.5
            )
        };

            result.C3ClimateRiskScore = MatchFirstScore(reportText, patterns);
        }
    }

    public class C4EmissionsScopeAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            var patterns = new List<(string Pattern, double Score)>
            {
            // ───────────────────────── 1,00 pkt ─────────────────────────
            (
                /* Zakres 1 + 2 (oba VARIANTS) + 3 z WYMIENIONYMI kategoriami,
                    a dla zakresu 2 pokazane OBA podejścia: location-based i market-based            */
                @"(?isx)
                    (?=.*\b(scope|zakres)\s*1\b)
                    (?=.*\b(scope|zakres)\s*2\b)
                    (?=.*\b(location[-\s]?based)\b)
                    (?=.*\b(market[-\s]?based)\b)
                    (?=.*\b(scope|zakres)\s*3\b)
                    (?=.*\b(kategor\w*|category)\b)
                ",
                1.0
            ),

            // ───────────────────────── 0,75 pkt ─────────────────────────
            (
                /* Zakres 1 + 2 (JEDNA metoda, wprost lub nie) + 3 z kategoriami        */
                @"(?isx)
                    (?=.*\b(scope|zakres)\s*1\b)
                    (?=.*\b(scope|zakres)\s*2\b)
                    (?=.*\b(location[-\s]?based|market[-\s]?based)\b)
                    (?=.*\b(scope|zakres)\s*3\b)
                    (?=.*\b(kategor\w*|category)\b)
                ",
                0.75
            ),

            // ───────────────────────── 0,50 pkt ─────────────────────────
            (
                /* Zakres 1 + 2 DWIE metody, brak obowiązku S3                         */
                @"(?isx)
                    (?=.*\b(scope|zakres)\s*1\b)
                    (?=.*\b(scope|zakres)\s*2\b)
                    (?=.*\b(location[-\s]?based)\b)
                    (?=.*\b(market[-\s]?based)\b)
                ",
                0.5
            ),

            // ───────────────────────── 0,25 pkt ─────────────────────────
            (
                /* Zakres 1 + 2 JEDNA metoda LUB brak wskazania metody                  */
                @"(?isx)
                    (?=.*\b(scope|zakres)\s*1\b)
                    (?=.*\b(scope|zakres)\s*2\b)
                    (?: (?=.*\b(location[-\s]?based|market[-\s]?based)\b) | .* )
                ",
                0.25
            )
        };

            result.C4EmissionsScopeScore = MatchFirstScore(reportText, patterns);
        }
    }

    public class C5EmissionsBoundaryAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            var patterns = new List<(string Pattern, double Score)>
                {
                // ───────────────────────── 1,00 pkt ─────────────────────────
                (
                    /* 1a) Emisje obejmują CAŁĄ grupę kapitałową / wszystkie spółki zależne
                            LUB podano tylko podmioty istotne, ale wraz z wyjaśnionymi
                            kryteriami istotności                                              */
                    @"(?isx)
                        (?=.*\b(emisj\w*|emission[s]?)\b)
                        (?=
                            # wariant „pełne pokrycie”
                            (?: .*?\b(cał[aeiy]\w*\s+(GK|grup\w+\s+kapitałow\w*|group|consolidated\s+group))\b
                                | .*?\b(all\s+subsidiar(?:y|ies)|wszystk\w*\s+jednostk\w*\s+zależn\w*)\b
                                | .*?\b(kontrol\w*\s+(operacyjn\w*|finansow\w*)|operational\s+control|financial\s+control)\b
                            )
                            |   # ─────── lub ───────
                            # wariant „istotne podmioty” + kryteria materialności
                            (?: .*?\b(podmiot\w*|entity|subsidiar(?:y|ies)|unit[s]?)\b
                                .*?\b(istotn\w*|material(?:ity)?)\b
                                .*?\b(kryteri\w*|criteria|criterion|wyjaśni\w*|explain\w*)\b
                            )
                        )",
                    1.0
                ),

                // ───────────────────────── 0,67 pkt ─────────────────────────
                (
                    /* Emisje obejmują WYBRANE najważniejsze jednostki,
                        ale BRAK wyjaśnienia kryteriów wyboru                                      */
                    @"(?isx)
                        (?=.*\b(emisj\w*|emission[s]?)\b)
                        (?=.*\b(wybran\w*|selected|największ\w*|largest|major)\b)
                        (?=.*\b(jednostk\w*|subsidiar(?:y|ies)|unit[s]?|spółek|companies)\b)
                        (?!.*\b(kryteri\w*|criteria|criterion|wyjaśni\w*|explain\w*)\b)
                    ",
                    0.67
                ),

                // ───────────────────────── 0,33 pkt ─────────────────────────
                (
                    /* Emisje podane tylko dla części podmiotów
                        (np. same zależne lub sama jednostka dominująca)                           */
                    @"(?isx)
                        (?=.*\b(emisj\w*|emission[s]?)\b)
                        (?=.*
                            \b(jednostk\w*\s+zależn\w*|subsidiar(?:y|ies)
                                |spółk\w*\s+dominuj\w*|parent\s+company)\b
                        )",
                    0.33
                )
            };
            result.C5EmissionsBoundaryScore = MatchFirstScore(reportText, patterns);
        }
    }

    public class C6CalculationStandardAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            var patterns = new List<(string Pattern, double Score)>
            {
            // ─────────────────── 1,00 pkt ───────────────────
            (
                /* Emisje obliczone wg uznanego standardu (ISO 14064-1, GHG Protocol itp.) */
                @"(?isx)
                    (?=.*\b(emisj\w*|emission[s]?|GHG|gaz(?:ów)?\s+cieplarnianych
                            |greenhouse\s+gas(?:es)?)\b)                    # kontekst emisji
                    (?=.*\b(                                               # nazwa normy / standardu
                            ISO\s*14064(?:[--–]\d)?                         # np. ISO 14064-1, 14064-3
                        | GHG\s+Protocol
                        | Greenhouse\s+Gas\s+Protocol
                        | Corporate\s+Accounting\s+and\s+Reporting\s+Standard
                        | IPCC\s+Guidelines
                        | ISO\s*14064\s*Series
                    )\b)
                ",
                1.0
            )
            // 0 pkt – brak dopasowania
        };

            result.C6CalculationStandardScore = MatchFirstScore(reportText, patterns);
        }
    }

    public class C7GwpSourcesAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            var patterns = new List<(string Pattern, double Score)>
            {
            // ───────────────────────── 1,00 pkt ─────────────────────────
            (
                /* Raport podaje źródła (source / źródło / referencja) zarówno
                    A) wskaźników emisji (emission factors)   i
                    B) współczynników GWP (global-warming-potential).                         */
                @"(?isx)
                    # ——— A) emission factors + source ———
                    (?=.*\b(                           # look-ahead 1
                            wskaźnik\w*\s+emisji
                        | emission\s+factor[s]?
                        )\b
                        .*?\b(źródł\w*|source[s]?|referencj\w*|IPCC|DEFRA|EPA)\b
                    )
                    # ——— B) GWP factors + source ———
                    (?=.*\b(
                            współczynnik\w*\s+GWP
                        | GWP\s+factor[s]?
                        | global\s+warming\s+potential
                        )\b
                        .*?\b(źródł\w*|source[s]?|referencj\w*|IPCC|DEFRA|EPA)\b
                    )",
                1.0
            )
            // 0 pkt – brak dopasowania
        };

            result.C7GwpSourcesScore = MatchFirstScore(reportText, patterns);
        }
    }

    public class C8EmissionsTrendAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            var patterns = new List<(string Pattern, double Score)>
                {
            // ───────────────────────── 1,00 pkt ─────────────────────────
            (
                /* Emisje + co najmniej TRZY wystąpienia roku (np. 2022-2024)               */
                @"(?isx)
                    (?=.*\b(
                        emisj\w*
                        | emission[s]?
                        | GHG
                        | gaz(?:ów)?\s+cieplarnianych
                        | greenhouse\s+gas(?:es)?
                    )\b)
                    (?=.*\b(zmian\w*|trend\w*|spadk\w*|wzrost\w*|changes?|increase|decrease|trend[s]?)\b)
                    (?=(?: .*? \b20\d{2}\b ){3,})
                ",
                1.0
            ),

            // ───────────────────────── 0,50 pkt ─────────────────────────
            (
                /* Emisje + dokładnie DWIE wystąpienia roku (rok raportu + poprzedni)       */
                @"(?isx)
                    (?=.*\b(emisj\w*|emission[s]?|GHG|gaz(?:ów)?\s+cieplarnianych|greenhouse\s+gas(?:es)?)\b)
                    (?=.*\b(zmian\w*|trend\w*|spadk\w*|wzrost\w*|changes?|increase|decrease|trend[s]?)\b)
                    (?=(?: .*? \b20\d{2}\b ){2,})    # ≥ 2 lata (jeśli byłoby ≥3, przechwyci pattern 1,0)
                ",
                0.5
            )
            // 0 pkt – brak dopasowania do powyższych
        };

            result.C8EmissionsTrendScore = MatchFirstScore(reportText, patterns);
        }
    }

    public class C9IntensityIndicatorAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            var patterns = new List<(string Pattern, double Score)>
                {
            // ───────────────────────────── 1,00 pkt ─────────────────────────────
            (
                /* Emission-intensity KPI – przykłady spodziewanych form:
                        • „wskaźnik intensywności emisji: 0,45 t CO₂e/MWh”
                        • „GHG emission intensity – 15 kg CO₂e per tonne product”
                        • „carbon-intensity (Scope 1+2): 30 t CO₂e / €m revenue”          */
                @"(?isx)
                    (?=.*\b(                           # kontekst emisji
                            emisj\w*
                        | emission[s]?
                        | GHG
                        | carbon
                        | gaz(?:ów)?\s+cieplarnianych
                        )\b)
                    (?=.*\b(intensywno\w*|intensity)\b)   # słowo „intensywność / intensity”
                    (?=                                   # jednostkowa forma (tCO₂e per … / … per …)
                        .*? \b(?:t|kg)\s*CO2e?\b
                        .*? (?: [/]|per\b|na\b )         # separator „/”, „per”, „na”
                    )
                ",
                1.0
            )
            // 0 pkt – brak dopasowania
        };


            result.C9IntensityIndicatorScore = MatchFirstScore(reportText, patterns);
        }
    }

    public class C10NumericConsistencyAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            var patterns = new List<(string Pattern, double Score)>
                {
            // ───────────────────────────── 1,00 pkt ─────────────────────────────
            (
                /* ABSOLUTNE, skwantyfikowane cele redukcji + DZIAŁANIA                */
                @"(?isx)
                    (?=.*\b(emisj\w*|emission[s]?|GHG|carbon|greenhouse\s+gas)\b)
                    (?=.*\b(redukcj\w*|reduc(?:e|tion))\b)
                    (?=.*\b\d{1,3}\s*%\b|\b(?:t|kt|Mg)\s*CO2e?\b)
                    (?=(?: .*? \b20\d{2}\b ){2,})
                    (?=.*\b(działan\w*|plan\w*|action\s+plan|measures?|inicjatyw\w*
                            |project[s]?|program\w*|investment[s]?|inwestycj\w*)\b)
                ",
                1.0
            ),

            // ───────────────────────────── 0,67 pkt ─────────────────────────────
            (
                /* ABSOLUTNE, skwantyfikowane cele redukcji, ale BEZ działań            */
                @"(?isx)
                    (?=.*\b(emisj\w*|emission[s]?|GHG|carbon|greenhouse\s+gas)\b)
                    (?=.*\b(redukcj\w*|reduc(?:e|tion))\b)
                    (?=.*\b\d{1,3}\s*%\b|\b(?:t|kt|Mg)\s*CO2e?\b)
                    (?=(?: .*? \b20\d{2}\b ){2,})
                    (?!.*\b(działan\w*|plan\w*|action\s+plan|measures?|inicjatyw\w*
                            |project[s]?|program\w*|investment[s]?|inwestycj\w*)\b)
                ",
                0.67
            ),

            // ───────────────────────────── 0,50 pkt ─────────────────────────────
            (
                /* SKWANTYFIKOWANE cele INTENSYWNOŚCI emisji (bez absolutnych)          */
                @"(?isx)
                    (?=.*\b(emisj\w*|emission[s]?|GHG|carbon|greenhouse\s+gas)\b)
                    (?=.*\b(intensywno\w*|intensity)\b)
                    (?=.*\b(target|cel\w*|goal)\b)
                    (?=.*\b\d{1,3}\s*%\b|\b(?:t|kg|g)\s*CO2e?\s*(/|per|na)\b)
                    (?=.*\b20\d{2}\b)
                ",
                0.5
            ),

            // ───────────────────────────── 0,33 pkt ─────────────────────────────
            (
                /* DZIAŁANIA redukcji emisji, ale BRAK skwantyfikowanych celów          */
                @"(?isx)
                    (?=.*\b(emisj\w*|emission[s]?|GHG|carbon|greenhouse\s+gas)\b)
                    (?=.*\b(działan\w*|plan\w*|action\s+plan|measures?|inicjatyw\w*
                            |project[s]?|program\w*|investment[s]?|inwestycj\w*)\b)
                    (?!.*\b(redukcj\w*|reduc(?:e|tion))\b.*?\b\d{1,3}\s*%\b)
                    (?!.*\b(intensywno\w*|intensity)\b.*?\b\d{1,3}\s*%\b)
                ",
                0.33
            )
        };

            result.C10NumericConsistencyScore = MatchFirstScore(reportText, patterns);
        }
    }

    public class C11UnitCorrectnessAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            var patterns = new List<(string Pattern, double Score)>
            {
            // ───────────────────────── 1,00 pkt ─────────────────────────
            (
                /* Przykładowe poprawne zapisy:
                        • 120 000 t CO2e
                        • 0.45 kg CO₂e per kg product
                        • 1.2 Mt CO₂-eq
                        • Emissions expressed in CO2-equivalents                                    */
                @"(?isx)
                    # wariant 1: liczba + jednostka masy + CO2e/CO₂e/CO2-eq
                    (?:\d+[.,]?\d*\s*
                        (?:t|kt|Mt|kg|Mg|tonn?e?s?)
                        \s*
                        (?:CO2e|CO₂e|CO2eq|CO₂eq|CO2[-\s]?equivalent[s]?|CO₂[-\s]?equivalent[s]?)
                    )
                    |
                    # wariant 2: zapis intensywności (CO2e per …)
                    (?:CO2e|CO₂e|CO2eq|CO₂eq)
                        \s*/\s*
                        [A-Za-ząćęłńóśźż/\-]+
                ",
                1.0
            )
        };
            result.C11UnitCorrectnessScore = MatchFirstScore(reportText, patterns);
        }
    }

    public class C12KeywordPresenceAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            var patterns = new List<(string Pattern, double Score)>
            {
            // ───────────────────────── 1,00 pkt ─────────────────────────
            (
                /* Dowolne ze słów-kluczy:                                                   */
                @"(?isx)
                    ( dwutlenek\W+węgla
                    | gaz\W+cieplarnian\w*
                    | CO\s*2       | CO₂
                    | zmian\w*\W+klimat\w*
                    | carbon\W+dioxide
                    | greenhouse\W+gas(?:es)?
                    | climate\W+change
                    | \bGHG\b
                    )",
                1.0
            )
        };
            result.C12KeywordPresenceScore = MatchFirstScore(reportText, patterns);
        }
    }

}
