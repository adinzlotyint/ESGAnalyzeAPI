using ESGAnalyzeAPI.Models;
using System.Text.RegularExpressions;

namespace ESGAnalyzeAPI.Services {
    public interface ICriterions {
        void Evaluate(string reportText, ESGAnalysisResult result);
    }

    public abstract class BaseRuleAnalyzer : ICriterions {
        protected static ILogger Logger { get; private set; }

        public static void SetLogger(ILogger logger) {
            Logger = logger;
        }
        public abstract void Evaluate(string reportText, ESGAnalysisResult result);

        protected double MatchFirstScore(string text, IEnumerable<(string Pattern, double Score)> patterns) {
            var opts = RegexOptions.IgnoreCase
                        | RegexOptions.Singleline
                        | RegexOptions.IgnorePatternWhitespace
                        | RegexOptions.CultureInvariant;

            var analyzerName = this.GetType().Name;

            int patternIndex = 1;

            foreach (var (pattern, score) in patterns) {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                bool isMatch = Regex.IsMatch(text, pattern, opts);

                stopwatch.Stop();

                Logger?.LogInformation("{Analyzer} - Pattern {PatternIndex} took {ElapsedMilliseconds} ms",
                    analyzerName, patternIndex, stopwatch.ElapsedMilliseconds);

                if (isMatch) {
                    Logger?.LogInformation("{Analyzer} - Pattern {PatternIndex} MATCHED --> Score: {Score}",
                        analyzerName, patternIndex, score);
                    return score;
                } else {
                    Logger?.LogInformation("{Analyzer} - Pattern {PatternIndex} NO MATCH",
                        analyzerName, patternIndex);
                }

                patternIndex++;
            }

            return 0.0;
        }
    }

    public class C1PolicyStrategyAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            /*  Kryterium C1 – Polityka / strategia dotycząca klimatu
             *  ------------------------------------------------------
             *  1.0 – (A) Samodzielna polityka/strategia klimatyczna publicznie dostępna
             *      – (B) Pełny opis w raporcie (założenia + cele + działania)
             *  0.5 – (C) Deklaracja posiadania polityki/strategii klimatycznej
             *      – (D) Kwestie klimatu w strategii biznesowej
             *      – (E) Strategia CSR / ESG obejmująca klimat lub środowisko
             */

            // ---------- stałe ----------
            const string CLIMATE =
                @"(klimat\w*|klimatyczn\w*|zmian\W+klimatu|climate\W*change
              |decarboni[sz]ation|net\W*zero|neutraln\w*|carbon\W*neutral)";

            const string POLICY =
                @"(polityk[aię]|strategi(?:a|e|ę)|policy|strategy)";

            const string PUBLIC =
                @"(publiczn\w*\s+dostęp\w*|publicly\s+(available|disclosed)
              |available\s+online|dostępna\s+online|udostępnion\w*
              |opublikowan\w*|na\s+stronie\s+internetowej|website)";

            const string ELEMENTS =
                @"(założen\w*|assumption[s]?)";
            const string GOALS = @"(cel(?:e|ów)?|targets?|goals?|objectives?)";
            const string ACTIONS = @"(działan\w*|actions?|measures?|inicjatyw\w*|initiatives?)";

            // ---------- regexy ----------
            var patterns = new List<(string Pattern, double Score)>
            {
            // 1.0A ─ Samodzielna polityka klimatyczna publicznie dostępna
            (
                $@"(?isx)
                    \b{POLICY}\b
                    .{{0,120}}
                    \b{CLIMATE}\b
                    .*?
                    \b{PUBLIC}\b
                ",
                1.0
            ),

            // 1.0B ─ Pełny opis (założenia + cele + działania) w raporcie
            (
                $@"(?isx)
                    \b{POLICY}\b
                    (?=.{{0,5000}}\b{CLIMATE}\b)
                    (?=.{{0,5000}}\b{ELEMENTS}\b)
                    (?=.{{0,5000}}\b{GOALS}\b)
                    (?=.{{0,5000}}\b{ACTIONS}\b)
                ",
                1.0
            ),

            // 0.5C ─ Deklaracja posiadania polityki/strategii klimatycznej
            (
                $@"(?isx)
                    (posiad\w*|wdrożon\w*|opracowan\w*|realizuj\w*
                     |have|has|maintain|maintains|implemented|adopted|prepared)\b
                    .{{0,60}}
                    {POLICY}\b
                    .{{0,120}}
                    {CLIMATE}
                ",
                0.5
            ),

            // 0.5D ─ Kwestie klimatu w strategii biznesowej
            (
                $@"(?isx)
                    \b(strategi(?:a|e|ę)\s+biznesow\w*|business\s+strategy)\b
                    .{{0,200}}
                    (zagadnienia|kwestie|ryzyka|szanse|aspekt\w*|topics?|issues|risks?|opportunit(?:y|ies))
                    .{{0,60}}
                    {CLIMATE}
                ",
                0.5
            ),

            // 0.5E ─ Strategia CSR / ESG / zrównoważonego rozwoju z elementem klimatu/środowiska
            (
                $@"(?isx)
                    \b(strategi(?:a|e|ę)\s+(CSR|zrównoważonego\ rozwoju|ESG|sustainability))\b
                    (?=.{{0,800}}\b({CLIMATE}|środowisk\w*|environment\w*)\b)
                ",
                0.5
            )
        };

            result.C1PolicyStrategyScore = MatchFirstScore(reportText, patterns);
        }
    }

    public class C2RisksAndChancesAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            /*  Punktacja
             *  1.00A – Ryzyka + szanse + (istotny) wpływ na finanse/strategię
             *  1.00B – Ryzyka + szanse + stwierdzenie „brak znaczącego wpływu”
             *  0.67  – Ryzyka + szanse + opis zarządzania / monitorowania
             *  0.33  – Wzmianka o ryzyku LUB szansie klimatycznej
             */

            const string RISK = @"(ryzyk\w*|risk[s]?)";
            const string CHANCE = @"(szans\w*|możliwoś\w*|opportunit(?:y|ies))";
            const string CLIMATE = @"(klimat\w*|zmian\W+klimatu|climate\W*change
                                   |decarboni[sz]ation|net\W*zero|carbon\W*neutral)";
            const string SIGNIF = @"(istotn\w*|znacząc\w*|material|significant|kluczow\w*)";
            const string IMPACT = @"(wpływ\w*|impact)";
            const string FINSTRAT = @"(finansow\w*|wynik\w*\s+finans\w*
                                   |strategi\w*\s+biznes\w*|business\s+strategy)";
            const string MANAGE = @"(zarządz\w*|manage|management|mitigat\w*|łagodzen\w*
                                   |monitor\w*|response|odpowiedz\w*|działan\w*)";

            var patterns = new List<(string Pattern, double Score)>
            {
            // 1.00A – istotny / znaczący wpływ
            (
                $@"(?isx)
                    \b{RISK}\b
                    (?=.{{0,5000}}\b{CHANCE}\b)
                    (?=.{{0,5000}}\b{CLIMATE}\b)
                    (?=.{{0,5000}}\b{SIGNIF}\b)
                    (?=.{{0,5000}}\b{IMPACT}\b)
                    (?=.{{0,5000}}\b{FINSTRAT}\b)
                ",
                1.0
            ),

            // 1.00B – firma stwierdza brak istotnego wpływu
            (
                $@"(?isx)
                    \b{RISK}\b
                    (?=.{{0,5000}}\b{CHANCE}\b)
                    (?=.{{0,5000}}\b{CLIMATE}\b)
                    (?=.{{0,5000}}\b(nie\s+mają|nie\s+ma|nie\s+będą|no|not)\b
                         .*?\b{SIGNIF}\b (?:.*?\b{IMPACT}\b)? )
                ",
                1.0
            ),

            // 0.67 – opis zarządzania / monitorowania
            (
                $@"(?isx)
                    \b{RISK}\b
                    (?=.{{0,5000}}\b{CHANCE}\b)
                    (?=.{{0,5000}}\b{CLIMATE}\b)
                    (?=.{{0,5000}}\b{MANAGE}\b)
                ",
                0.67
            ),

            // 0.33 – sama wzmianka o ryzykuLUBszansie klimatycznej
            (
                $@"(?isx)
                    \b({RISK}|{CHANCE})\b
                    (?=.{{0,5000}}\b{CLIMATE}\b)
                ",
                0.33
            )
        };

            result.C2RisksAndChancesScore = MatchFirstScore(reportText, patterns);
        }
    }

    public class C3ClimateRiskAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            /*  Punktacja
             *  1.00A – KONKRETNY członek Zarządu / RN odpowiedzialny za klimat
             *  1.00B – Komitet/komisja klimatyczna z udziałem członka Zarządu / RN
             *  0.50  – Menedżer (CSO/ESG) odpowiedzialny za klimat
             *  0.50  – Kolektywny nadzór całego Zarządu / RN
             */

            const string CLIMATE = @"(klimat\w*|zmian\W+klimatu|climate\W*change|climate)";
            const string RESPS = @"(odpowiad\w*|nadzoruj\w*|zarządz\w*|koordynuj\w*
                                 |responsible\s+for|oversee[s]?|manage[s]?|lead[s]?)";

            const string BOARD_MEMBER =
                @"( członek\w*\s+(zarządu|rady\s+nadzorczej|rady\s+dyrektorów)
             | member\s+of\s+the\s+(board\s+of\s+directors|supervisory\s+board) )";

            const string BOARD_COLLECTIVE =
                @"(zarząd|rada\s+nadzorcza|rada\s+dyrektorów
               |board\ of\ directors|supervisory\s+board)";

            const string NOT_WHOLE =
                @"(?!.*?\b(cały|cała|all|whole)\s+" + BOARD_COLLECTIVE + @"\b)";

            const string COMMITTEE =
                @"(zespół|komitet|komisja|committee)";

            const string MANAGER_ROLE =
                @"(menadżer\w*|manager|kierownik\w*|dyrektor\w*|director
              |pełnomocnik\w*|officer
              |chief\s+sustainability\s+officer
              |head\s+of\s+sustainability
              |CSO)";

            var patterns = new List<(string Pattern, double Score)>
            {
            // 1.00A – konkretny członek Zarządu / RN
            (
                $@"(?isx){NOT_WHOLE}
                    \b{BOARD_MEMBER}\b
                    .*? \b{RESPS}\b
                    .*? \b{CLIMATE}\b
                ",
                1.0
            ),

            // 1.00B – komitet klimatyczny
            (
                $@"(?isx)
                    \b{COMMITTEE}\b
                    .*? \b{CLIMATE}\b
                    .*? \b(w\s+skład|includes|consist[s]?\s+of)\b
                    .*? {BOARD_MEMBER}
                ",
                1.0
            ),

            // 0.50 – manager / CSO odpowiedzialny za klimat
            (
                $@"(?isx)
                    \b{MANAGER_ROLE}\b
                    .*? \b{RESPS}\b
                    .*? \b{CLIMATE}\b
                ",
                0.5
            ),

            // 0.50 – kolektywny nadzór Zarządu / RN
            (
                $@"(?isx)
                    \b{BOARD_COLLECTIVE}\b
                    .*? \b(nadzoruj\w*|monitoruj\w*|koordynuj\w*|oversee|monitor|coordinate[s]?)\b
                    .*? \b{CLIMATE}\b
                ",
                0.5
            )
        };

            result.C3ClimateRiskScore = MatchFirstScore(reportText, patterns);
        }
    }


    public class C4EmissionsScopeAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            /*  LOGIKA PUNKTACJI
             *  1.00  – Scope 1 + 2 (obie metody) + 3  z wyszczególnieniem kategorii/obszarów
             *  0.75  – Scope 1 + 2 + 3  (bez L-B / M-B, ale z kategoriami)
             *  0.75  – Scope 1 + 2 + 3  zaznaczony jako „częściowy”  **lub** 1+2 (≥1 metoda) + 3 + kategorie
             *  0.50  – Scope 1 + 2 + 3  (bez kategorii, bez metod)  -- lub -- 1+2 (obie metody)
             *  0.25  – Scope 1 + 2  (≥1 metoda opcjonalnie)
             */

            const string scope1 = @"\b(scope|zakres|zakresie|scope\s*I)\s*1\b";
            const string scope2 = @"\b(scope|zakres|zakresie|scope\s*II?)\s*2\b";
            const string scope3 = @"\b(scope|zakres|zakresie|scope\s*III?)\s*3\b";

            var patterns = new List<(string Pattern, double Score)>
            {
            // 1.00 – pełne ujawnienie 1+2 (L-B & M-B) + 3 z kategoriami
            (
                $@"(?isx)
                    (?=.{{0,5000}}{scope1})
                    (?=.{{0,5000}}{scope2})
                    (?=.{{0,5000}}\blocation[-\s]?based\b)
                    (?=.{{0,5000}}\bmarket[-\s]?based\b)
                    (?=.{{0,5000}}{scope3})
                    (?=.{{0,5000}}\b(kategor\w*|obszar\w*|category|area)\b)
                ",
                1.0
            ),

            // 0.90 – 1+2+3 + kategorie, ale brak rozróżnienia L-B/M-B
            (
                $@"(?isx)
                    (?=.{{0,5000}}{scope1})
                    (?=.{{0,5000}}{scope2})
                    (?=.{{0,5000}}{scope3})
                    (?=.{{0,5000}}\b(kategor\w*|obszar\w*|category|area)\b)
                ",
                0.75
            ),

            // 0.75 – 1+2 (≥1 z metod) + 3 + kategorie  **lub** „częściowy” Scope 3
            (
                $@"(?isx)
                    (?=.{{0,5000}}{scope1})
                    (?=.{{0,5000}}{scope2})
                    (?=.{{0,5000}}\b(location[-\s]?based|market[-\s]?based)\b)?
                    (?=.{{0,5000}}{scope3})
                    (?=.{{0,5000}}\b(kategor\w*|obszar\w*|category|area)\b)
                ",
                0.75
            ),
            (
                $@"(?isx)
                    (?=.{{0,5000}}{scope1})
                    (?=.{{0,5000}}{scope2})
                    (?=.{{0,5000}}{scope3})
                    (?=.{{0,5000}}\b(częściow\w*|partial)\b)
                ",
                0.75
            ),

            // 0.50 – 1+2+3 (bez kategorii)  **lub** 1+2 z obiema metodami
            (
                $@"(?isx)
                    (?=.{{0,5000}}{scope1})
                    (?=.{{0,5000}}{scope2})
                    (?=.{{0,5000}}{scope3})
                ",
                0.50
            ),
            (
                $@"(?isx)
                    (?=.{{0,5000}}{scope1})
                    (?=.{{0,5000}}{scope2})
                    (?=.{{0,5000}}\blocation[-\s]?based\b)
                    (?=.{{0,5000}}\bmarket[-\s]?based\b)
                ",
                0.50
            ),

            // 0.25 – minimalnie Scope 1 + 2 (metody opcjonalne)
            (
                $@"(?isx)
                    (?=.{{0,5000}}{scope1})
                    (?=.{{0,5000}}{scope2})
                ",
                0.25
            )
        };

            result.C4EmissionsScopeScore = MatchFirstScore(reportText, patterns);
        }
    }


    public class C5EmissionsBoundaryAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            /*  LOGIKA PUNKTACJI
             *  1.00 – dane o emisjach obejmują CAŁĄ grupę            ──► „cała GK”, „all subsidiaries”, kontrola oper./finans.
             *       – lub: wybrane podmioty + podane kryteria ISTOTNOŚCI (materiality test)
             *  0.67 – wybrane / największe podmioty, ALE bez kryteriów doboru
             *  0.50 – Scope 3 obliczony tylko dla wybranych kategorii / obszarów (boundary rzeczowa)
             *  0.33 – wzmianka, że dane dotyczą jednostek zależnych lub samej spółki dominującej, brak szczegółów
             */

            const string EMISSIONS = @"\b(emisj\w*|emission[s]?)\b";
            const string FULL_GROUP =
                @"\b(cał[aeiy]\w*\s+(GK|grup\w+\s+kapitałow\w*|grup\w*|consolidated\s+group)
              |all\s+subsidiar(?:y|ies)
              |wszystk\w*\s+jednostk\w*\s+zależn\w*
              |pełn\w*\s+zakres
              |entire\s+(group|organisation|organization))\b";

            const string CONTROL =
                @"\b(kontrol\w*\s+(operacyjn\w*|finansow\w*|equity)
              |operational\s+control
              |financial\s+control
              |equity\s+share)\b";

            const string MATERIALITY =
                @"\b(istotn\w*|material(?:ity)?|znacząc\w*)\b";
            const string CRITERIA =
                @"\b(kryteri\w*|criteria|criterion|wyjaśni\w*|explain\w*)\b";

            const string SUBSIDIARY =
                @"\b(jednostk\w*|subsidiar(?:y|ies)|unit[s]?|spółek|companies)\b";

            const string SCOPE3 =
                @"\b(scope|zakres|scope\s*III?)\s*3\b";

            var patterns = new List<(string Pattern, double Score)>
            {
            // 1.00 ─ pełne pokrycie całej grupy LUB wybrane + kryteria materialności
            (
                $@"(?isx)
                    (?=.{{0,5000}}{EMISSIONS})
                    (?=
                        .{{0,5000}}
                        ( {FULL_GROUP} | {CONTROL} )
                    )
                ",
                1.0
            ),
            (
                $@"(?isx)
                    (?=.{{0,5000}}{EMISSIONS})
                    (?=.{{0,5000}}\b(wybran\w*|selected|największ\w*|largest|major)\b)
                    (?=.{{0,5000}}{SUBSIDIARY})
                    (?=.{{0,5000}}{MATERIALITY})
                    (?=.{{0,5000}}{CRITERIA})
                ",
                1.0        // „wybrane podmioty” + kryteria istotności = maksymalna ocena
            ),

            // 0.67 ─ wybrane / największe spółki, brak uzasadnienia
            (
                $@"(?isx)
                    (?=.{{0,5000}}{EMISSIONS})
                    (?=.{{0,5000}}\b(wybran\w*|selected|największ\w*|largest|major)\b)
                    (?=.{{0,5000}}{SUBSIDIARY})
                    (?!.*?{CRITERIA})
                ",
                0.67
            ),

            // 0.50 ─ Scope 3 obliczony tylko dla wybranych kategorii/obszarów
            (
                $@"(?isx)
                    (?=.{{0,600}}{SCOPE3})
                    (?=.{{0,600}}\b(wybran\w*|selected)\b)
                    (?=.{{0,600}}\b(kategor\w*|obszar\w*|category|area)\b)
                ",
                0.50
            ),

            // 0.33 ─ wzmianka, że dane dotyczą jednostek zależnych / spółki dominującej
            (
                $@"(?isx)
                    (?=.{{0,5000}}{EMISSIONS})
                    (?=.{{0,5000}}
                        \b(jednostk\w*\s+zależn\w*|subsidiar(?:y|ies)
                          |spółk\w*\s+dominuj\w*|parent\s+company)\b
                    )
                ",
                0.33
            )
        };

            result.C5EmissionsBoundaryScore = MatchFirstScore(reportText, patterns);
        }
    }

    public class C6CalculationStandardAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            /*  1 pkt – firma wprost wskazuje UZNANY STANDARD lub NORMĘ,
             *          na podstawie której wyliczono emisje GHG.
             *
             *  Ujęte standardy (PL/EN):
             *  · ISO 14064-1/-2/-3, ISO 14064 Series, ISO 14065, ISO 14067
             *  · GHG Protocol (Corporate / Product), WRI/WBCSD
             *  · Corporate Accounting & Reporting Standard
             *  · IPCC Guidelines (ang. i „wytyczne IPCC” po polsku)
             *  · PCAF Standard (dla emisji finansowanych)
             *  · PAS 2050
             */

            const string EMISSIONS =
                @"\b(emisj\w*|emission[s]?|GHG|gaz(?:ów)?\s+cieplarnianych
               |greenhouse\s+gas(?:es)?)\b";

            const string STANDARDS =
                @"\b(
                  ISO\s*1406[4-7](?:[-–-]\d)?
                | ISO\s*14065
                | ISO\s*14067
                | GHG\s+Protocol
                | Greenhouse\s+Gas\s+Protocol
                | Corporate\s+Accounting\s+and\s+Reporting\s+Standard
                | WRI\s*/\s*WBCSD
                | (World\s+Resources|WRI)\s+(Institute|Instytut)
                | PCAF\s+Standard
                | PAS\s*2050
                | IPCC\s+Guidelines
                | wytyczn\w*\s+IPCC
              )\b";

            var patterns = new List<(string Pattern, double Score)>
            {
            (
                $@"(?isx)
                    (?=.{{0,5000}}{EMISSIONS})
                    (?=.{{0,5000}}{STANDARDS})
                ",
                1.0
            )
        };

            result.C6CalculationStandardScore = MatchFirstScore(reportText, patterns);
        }
    }

    public class C7GwpSourcesAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            /*  Punktacja
             *  1.0  – ŹRÓDŁO dla (a) współczynników emisji  **i**  (b) współczynników GWP
             *  0.0  – brak podanych źródeł
             *
             *  Akceptowane bazy / instytucje (PL/EN):
             *  · IPCC, DEFRA, EPA, IEA, EEA, BEIS
             *  · GHG Protocol, WRI/WBCSD
             *  · KOBiZE (PL), GEMIS, ecoinvent
             */

            const string SOURCES =
                @"\b(
                  źródł\w*|source[s]?|referencj\w*
                | IPCC | DEFRA | EPA | IEA | EEA | BEIS
                | GHG\s*Protocol | Greenhouse\s+Gas\s+Protocol
                | WRI | WBCSD | KOBIZE | GEMIS | ecoinvent
            )\b";

            const string EF =
                @"\b(
                  wskaźnik\w*\s+emisji
                | współczynnik\w*\s+emisji
                | emission\s+factor[s]?
                | emisji\s+czynnik\w*
            )\b";

            const string GWP =
                @"\b(
                  współczynnik\w*\s+GWP
                | GWP\s+factor[s]?
                | global\s+warming\s+potential
                | potencjał\w*\s+globalneg\w*\s+ociepleni\w*
            )\b";

            var patterns = new List<(string Pattern, double Score)>
            {
            // 1.0 – źródło dla EF **i** GWP
            (
                $@"(?isx)
                    (?=.{{0,5000}}{EF} .*? {SOURCES})
                    (?=.{{0,5000}}{GWP} .*? {SOURCES})
                ",
                1.0
            ),
        };

            result.C7GwpSourcesScore = MatchFirstScore(reportText, patterns);
        }
    }

    public class C8EmissionsTrendAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            /*  Kryterium C8 – Zmiany wielkości emisji GHG
             *  1.0  – co najmniej TRZY różne lata  + słowo-klucz opisujące trend (wzrost/spadek itd.)
             *  0.5  – dokładnie DWA lata           + słowo-klucz trendu
             *
             *  Lista słów obejmuje polskie i angielskie formy
             *  (w tym czasowniki oraz skróty „r/r”, „YoY”, „rok-do-roku”).
             */

            const string EMISSIONS =
                @"\b(
                  emisj\w*
                | emission[s]?
                | GHG
                | gaz(?:ów|y)?\s+cieplarnianych
                | greenhouse\s+gas(?:es)?
            )\b";

            const string TREND =
                @"\b(
                  zmian\w*   | trend\w*
                | spadk\w*  | spad(?:ł|ła|ły)
                | wzrost\w* | wzrós[łł]|wzrosł\w*
                | zmniejsz\w* | obniż\w*
                | zwiększ\w* | wyższ\w* | niższ\w*
                | redukcj\w* | redukow\w*
                | increase | decrease | rise | drop | decline\w*
                | higher | lower
                | year[-\s]?on[-\s]?year | YoY
                | r\/r | rok[-\s]?do[-\s]?roku
            )\b";

            const string THREE_YEARS = @"(?: .*? \b20\d{2}\b ){3,}";
            const string TWO_YEARS = @"(?: .*? \b20\d{2}\b ){2,}";

            var patterns = new List<(string Pattern, double Score)>
            {
            // 1.0  – ≥ 3 lata + słowo trendu
            (
                $@"(?isx)
                    (?=.{{0,5000}}{EMISSIONS})
                    (?=.{{0,5000}}{TREND})
                    (?=.{{0,5000}}{THREE_YEARS})
                ",
                1.0
            ),

            // 0.5 – ≥ 2 lata + słowo trendu
            (
                $@"(?isx)
                    (?=.{{0,5000}}{EMISSIONS})
                    (?=.{{0,5000}}{TREND})
                    (?=.{{0,5000}}{TWO_YEARS})
                ",
                0.5
            )
        };

            result.C8EmissionsTrendScore = MatchFirstScore(reportText, patterns);
        }
    }

    public class C9IntensityIndicatorAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            /*  Kryterium C9 – wskaźniki INTENSYWNOŚCI emisji GHG
             *  1.0  – w raporcie występuje poprawnie zapisany wskaźnik:
             *          ▸ liczba + jednostka (g / kg / t / kt / Mt / Mg) + CO2e/CO₂e/CO2eq
             *          ▸ separator „/”, „per”, „na” (PL)  – np. 33 kg CO2e na produkt
             *          ▸ w bezpośrednim kontekście emisji; słowo „intensywność” jest mile widziane,
             *            ale NIE obowiązkowe.
             */

            const string EMISSIONS =
                @"\b( emisj\w* | emission[s]? | GHG | carbon
               | gaz(?:ów|y)?\s+cieplarnianych )\b";

            const string UNIT_CO2E =
                @"\b(?:g|kg|Mg|t|kt|Mt|tony?)\s*CO2(?:e|eq)?|CO₂(?:e|eq)?\b";

            const string SEPARATOR =
                @"(?: / | per\b | na\b | na\s+każd\w*\b | /y )";

            // 1.0 – zawiera słowo „intensywność/intensity” + poprawny wzór
            const string INTENSITY_WORD = @"\b(intensywno\w*|intensity)\b";

            var patterns = new List<(string Pattern, double Score)>
            {
            // 1.0 – wskaźnik z wyraźnym słowem „intensywność”
            (
                $@"(?isx)
                    (?=.{{0,5000}}{EMISSIONS})
                    (?=.{{0,5000}}{INTENSITY_WORD})
                    (?=.*?{UNIT_CO2E}.*?{SEPARATOR})
                ",
                1.0
            ),

            // 1.0 – wzór jednostkowy bez słowa „intensywność”
            (
                $@"(?isx)
                    (?=.{{0,5000}}{EMISSIONS})
                    (?=.*?{UNIT_CO2E}.*?{SEPARATOR})
                ",
                1.0
            )
        };

            result.C9IntensityIndicatorScore = MatchFirstScore(reportText, patterns);
        }
    }

    public class C10NumericConsistencyAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            /*  Kryterium C10 – Cele i plany redukcji emisji
             *  ------------------------------------------------
             *  1.00 – SKWANTYFIKOWANY cel ABSOLUTNY (%, t/kt/Mt CO2e …)
             *         + min. 2 lata (bazowy i docelowy)
             *         + DZIAŁANIA / PLAN
             *  0.67 – jak wyżej, ale brak działań / planu
             *  0.50 – SKWANTYFIKOWANY cel INTENSYWNOŚCI  + rok docelowy
             *  0.33 – Deklaracja działań bez liczb
             *  0.00 – Nic z powyższych
             */

            // ---------- stałe ----------
            const string EMISSIONS =
                @"\b(emisj\w*|emission[s]?|GHG|carbon
               |gaz(?:ów|y)?\s+cieplarnianych
               |greenhouse\s+gas(?:es)?)\b";

            const string REDUCTION =
                @"\b(redukcj\w*|ograniczen\w*|zmniejszen\w*|obniż\w*
               |reduc(?:e|tion)|cut[s]?|lower|decrease|mitigat\w*)\b";

            const string ACTIONS =
                @"\b(działan\w*|plan\w*|roadmap|action\s+plan|strategy
               |measures?|inicjatyw\w*|projects?|program\w*|investment[s]?
               |inwestycj\w*|pathway|ścieżk\w*)\b";

            const string TARGET_WORD =
                @"\b(target|cel\w*|goal|objective)\b";

            const string ABS_VALUE =
                @"\b\d{1,3}(?:[\.,]\d{1,3})?\s*(?:t|kt|Mt|Mg)\s*CO2(?:e|eq)?\b";

            const string PERCENT = @"\b\d{1,3}\s*%\b";

            const string INTENSITY_VALUE =
                @"\b\d+(?:[\.,]\d+)?\s*(?:g|kg|Mg|t)\s*CO2(?:e|eq)?
              \s*(?:/|per\b|na\b)\s*\w+";

            const string YEARS_TWO = @"(?: .*?\b20\d{2}\b ){2,}";   // ≥2 różne lata

            // ---------- wzorce ----------
            var patterns = new List<(string Pattern, double Score)>
            {
            // 1.00 – absolutny cel + plan
            (
                $@"(?isx)
                    (?=.{{0,6000}}{EMISSIONS})
                    (?=.{{0,6000}}{YEARS_TWO})

                    # jedno zdanie: słowo CEL, słowo REDUKCJI, liczba %, tCO2e …
                    (?=
                        [^.{{0,200}}]
                        {TARGET_WORD} .*? {REDUCTION} .*? ({PERCENT}|{ABS_VALUE})
                        [^.]*\.
                    )

                    # działania / plan w promieniu 6000 znaków
                    (?=.{{0,6000}}{ACTIONS})
                ",
                1.0
            ),

            // 0.67 – absolutny cel bez działań
            (
                $@"(?isx)
                    (?=.{{0,6000}}{EMISSIONS})
                    (?=.{{0,6000}}{YEARS_TWO})
                    (?=
                        [^.{{0,200}}]
                        {TARGET_WORD} .*? {REDUCTION} .*? ({PERCENT}|{ABS_VALUE})
                        [^.]*\.
                    )
                    (?!.*?{ACTIONS})
                ",
                0.67
            ),

            // 0.50 – cel intensywności (liczba + jednostka) + rok docelowy
            (
                $@"(?isx)
                    (?=.{{0,6000}}{EMISSIONS})
                    (?=.{{0,6000}}\b20\d{{2}}\b)               # rok docelowy
                    (?=
                        [^.{{0,200}}]
                        {TARGET_WORD} .*? ({INTENSITY_VALUE}|{PERCENT})
                        [^.]*\.
                    )
                ",
                0.5
            ),

            // 0.33 – same działania/inwestycje, brak liczb
            (
                $@"(?isx)
                    (?=.{{0,6000}}{EMISSIONS})
                    (?=.{{0,6000}}{ACTIONS})
                    (?!.*?({PERCENT}|{ABS_VALUE}|{INTENSITY_VALUE}))
                ",
                0.33
            )
        };

            result.C10NumericConsistencyScore = MatchFirstScore(reportText, patterns);
        }
    }

    public class C11UnitCorrectnessAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            /*  Kryterium C11 – Poprawna jednostka: CO2e / CO₂e / CO2eq  (ekwiwalent CO2)
             *  Wymagamy co najmniej jednego poprawnego zapisu:
             *     ▸ liczba  +  jednostka masy  +  CO2e / CO₂e / CO2eq / CO₂eq / CO2-equivalent
             *     ▸ lub zapis wskaźnika (CO2e / jednostka działalności)
             *
             *  Akceptowane jednostki masy: g, kg, Mg, t, kt, Mt, „ton”, „tony”, „tonnes”.
             */

            const string NUMBER =
                @"\d{1,3}(?:[\u00A0\u2007\u202F\s'.]\d{3})*(?:[.,]\d+)?";   // 1 234,56  | 1 234  | 1234.5

            const string MASS_UNIT =
                @"(?:g|kg|Mg|t|kt|Mt|Gg|Tg|tonn?e?s?|tony)";

            const string CO2E =
                @"(?:CO2e|CO₂e|CO2eq|CO₂eq|CO2[\u2011-–\- ]?equivalent(?:s)?
              |CO₂[\u2011-–\- ]?equivalent(?:s)?)";

            const string INDICATOR =
                @"(?: / | per\b | na\b | na\s+każd\w*\b | /y)";

            var patterns = new List<(string Pattern, double Score)>
            {
            // 1.0 – poprawna liczba + jednostka masy + CO2e
            (
                $@"(?isx)
                    {NUMBER}\s*{MASS_UNIT}\s*{CO2E}
                    |
                    {CO2E}\s*{INDICATOR}\s*[A-Za-ząćęłńóśźż0-9/\-]+
                ",
                1.0
            )
        };

            result.C11UnitCorrectnessScore = MatchFirstScore(reportText, patterns);
        }
    }

    public class C12KeywordPresenceAnalyzer : BaseRuleAnalyzer {
        public override void Evaluate(string reportText, ESGAnalysisResult result) {
            /*  Kryterium C12 – obecność SŁÓW KLUCZOWYCH klimatyczno-emisyjnych
             *  (wystarczy jedno trafienie, punktacja binarna 1 / 0).
             *
             *  Rozszerzyliśmy listę o:
             *  · „CO2e / CO₂e / CO2eq / CO₂eq”         – bardzo częsty skrót w raportach
             *  · „globalne ocieplenie / global warming”
             *  · „ślad węglowy / carbon footprint”
             *  · warianty liczby mnogiej „gazy cieplarniane”
             */

            var patterns = new List<(string Pattern, double Score)>
            {
            (
                @"(?isx)\b(
                       dwutlenek\W+w[ęe]gla
                     | gaz(?:y|ów)?\W+cieplarnian\w*
                     | CO\s*2
                     | CO₂
                     | CO2e | CO₂e | CO2eq | CO₂eq
                     | zmian\w*\W+klimat\w*
                     | globaln\w*\W+ocieplen\w*
                     | ślad\W+węglow\w*
                     | carbon\W+dioxide
                     | carbon\W+footprint
                     | greenhouse\W+gas(?:es)?
                     | climate\W+change
                     | global\W+warming
                     | \bGHG\b
                   )\b",
                1.0
            )
        };

            result.C12KeywordPresenceScore = MatchFirstScore(reportText, patterns);
        }
    }

}
