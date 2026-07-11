using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CodePlanner.Core
{
    /// <summary>Impact of the question on the project – controls question planning.</summary>
    public enum Impact
    {
        High,
        Medium
    }

    public class ProjectTemplate
    {
        [JsonPropertyName("klic")]
        public string Key { get; set; } = "";

        [JsonPropertyName("nazev")]
        public string Name { get; set; } = "";

        [JsonPropertyName("otazky")]
        public List<TemplateQuestion> Questions { get; set; } = new List<TemplateQuestion>();
    }

    public class TemplateQuestion
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        [JsonPropertyName("napoveda")]
        public string HelpText { get; set; } = "";

        [JsonPropertyName("vychoziPredpoklad")]
        public string DefaultAssumption { get; set; } = "";

        [JsonPropertyName("moznosti")]
        public List<string> Options { get; set; } = new List<string>();
    }

    public static class TemplateService
    {
        public static List<ProjectTemplate> CustomTemplates { get; private set; } = new List<ProjectTemplate>();

        public static void LoadCustomTemplates()
        {
            CustomTemplates = new List<ProjectTemplate>();
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sablony.json");
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var data = JsonSerializer.Deserialize<Dictionary<string, List<ProjectTemplate>>>(json, opt);
                    if (data != null && data.TryGetValue("sablony", out var list))
                    {
                        CustomTemplates = list;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading custom templates: {ex.Message}");
            }
        }
    }

    /// <summary>Project Type / Question Template.</summary>
    public enum ProjectType
    {
        General,
        Game,
        Registry,
        Tool
    }

    /// <summary>One guided question. High-impact questions go first.</summary>
    public class Question
    {
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
        public string HelpText { get; set; } = "";
        public Impact Impact { get; set; }
        public string Section { get; set; } = "";
        public string DefaultAssumption { get; set; } = "";
        public List<string> Options { get; set; } = new List<string>();

        // Dicts for template texts
        [JsonIgnore]
        public Dictionary<ProjectType, string> Texts { get; set; } = new Dictionary<ProjectType, string>();
        [JsonIgnore]
        public Dictionary<ProjectType, string> Helps { get; set; } = new Dictionary<ProjectType, string>();
        [JsonIgnore]
        public Dictionary<ProjectType, string> Assumptions { get; set; } = new Dictionary<ProjectType, string>();

        public List<string> GetOptions(string typeKey)
        {
            var template = TemplateService.CustomTemplates.FirstOrDefault(s => string.Equals(s.Key, typeKey, StringComparison.OrdinalIgnoreCase));
            if (template != null)
            {
                var ot = template.Questions.FirstOrDefault(o => string.Equals(o.Id, Id, StringComparison.OrdinalIgnoreCase));
                if (ot != null && ot.Options != null && ot.Options.Count > 0) return ot.Options;
            }

            if (Options != null && Options.Count > 0) return Options;

            return GetDefaultOptionsForId(Id);
        }

        private static List<string> GetDefaultOptionsForId(string id)
        {
            switch (id)
            {
                case "tech-platforma":
                    return new List<string> { "Web (React + Node.js)", "Mobilní (React Native)", "Desktop (Windows Forms)" };
                case "tech-offline":
                    return new List<string> { "Plně offline (lokální ukládání)", "Primárně online (s REST API)", "Hybridní (offline-first s cloud synchronizací)" };
                case "data-obsah":
                    return new List<string> { "SQLite databáze v souboru", "PostgreSQL na cloudovém serveru", "Lokální JSON konfigurační soubor" };
                case "data-export":
                    return new List<string> { "Export do CSV a Excelu", "Kompletní JSON záloha", "Žádný export (pouze v aplikaci)" };
                case "rizika-reseni":
                    return new List<string> { "Automatické denní zálohy na pozadí", "Jednoduché chybové hlášení uživateli", "Omezení velikosti nahrávaných dat" };
                case "akceptace":
                    return new List<string> { "Prochází všechny automatické testy", "Uživatel dokáže úspěšně dokončit celý scénář", "Aplikace splňuje výkonnostní limity (odezva pod 100ms)" };
                default:
                    return new List<string>();
            }
        }

        public string GetText(string typeKey)
        {
            if (Enum.TryParse<ProjectType>(typeKey, true, out var enumType))
            {
                if (enumType == ProjectType.General) return Text;
                return Texts.TryGetValue(enumType, out var val) && !string.IsNullOrWhiteSpace(val) ? val : Text;
            }

            var template = TemplateService.CustomTemplates.FirstOrDefault(s => string.Equals(s.Key, typeKey, StringComparison.OrdinalIgnoreCase));
            if (template != null)
            {
                var ot = template.Questions.FirstOrDefault(o => string.Equals(o.Id, Id, StringComparison.OrdinalIgnoreCase));
                if (ot != null && !string.IsNullOrWhiteSpace(ot.Text)) return ot.Text;
            }

            return Text;
        }

        public string GetHelpText(string typeKey)
        {
            if (Enum.TryParse<ProjectType>(typeKey, true, out var enumType))
            {
                if (enumType == ProjectType.General) return HelpText;
                return Helps.TryGetValue(enumType, out var val) && !string.IsNullOrWhiteSpace(val) ? val : HelpText;
            }

            var template = TemplateService.CustomTemplates.FirstOrDefault(s => string.Equals(s.Key, typeKey, StringComparison.OrdinalIgnoreCase));
            if (template != null)
            {
                var ot = template.Questions.FirstOrDefault(o => string.Equals(o.Id, Id, StringComparison.OrdinalIgnoreCase));
                if (ot != null && !string.IsNullOrWhiteSpace(ot.HelpText)) return ot.HelpText;
            }

            return HelpText;
        }

        public string GetDefaultAssumption(string typeKey)
        {
            if (Enum.TryParse<ProjectType>(typeKey, true, out var enumType))
            {
                if (enumType == ProjectType.General) return DefaultAssumption;
                return Assumptions.TryGetValue(enumType, out var val) && !string.IsNullOrWhiteSpace(val) ? val : DefaultAssumption;
            }

            var template = TemplateService.CustomTemplates.FirstOrDefault(s => string.Equals(s.Key, typeKey, StringComparison.OrdinalIgnoreCase));
            if (template != null)
            {
                var ot = template.Questions.FirstOrDefault(o => string.Equals(o.Id, Id, StringComparison.OrdinalIgnoreCase));
                if (ot != null && !string.IsNullOrWhiteSpace(ot.DefaultAssumption)) return ot.DefaultAssumption;
            }

            return DefaultAssumption;
        }

        public string GetText(ProjectType typ) => GetText(typ.ToString());
        public string GetHelpText(ProjectType typ) => GetHelpText(typ.ToString());
        public string GetDefaultAssumption(ProjectType typ) => GetDefaultAssumption(typ.ToString());
    }

    /// <summary>User response or marked assumption.</summary>
    public class Answer
    {
        public string QuestionId { get; set; } = "";
        public string Text { get; set; } = "";
        public bool IsAssumption { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>Entry in the decision log.</summary>
    public class DecisionLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Action { get; set; } = "";
        public string Detail { get; set; } = "";
    }

    public class UserStory
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public List<string> Criteria { get; set; } = new List<string>();
        public string Priority { get; set; } = "Střední";
    }

    public class ChatMessage
    {
        public string Role { get; set; } = "user"; // "user" / "model"
        public string Text { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }

    public class ProjectMetrics
    {
        public string TimeEstimateMin { get; set; } = "";
        public string TimeEstimateMax { get; set; } = "";
        public string Complexity { get; set; } = "";
        public string TeamComposition { get; set; } = "";
        public string RecommendedBudget { get; set; } = "";
        public string TechnicalAnalysis { get; set; } = "";
        public List<string> MetricRisks { get; set; } = new List<string>();
        public DateTime CalculationTimestamp { get; set; }
    }

    /// <summary>One consistency warning/finding.</summary>
    public class AiFinding
    {
        public string Severity { get; set; } = "Varovani"; // "Rozpor" / "Varovani"
        public string Title { get; set; } = "";
        public string Detail { get; set; } = "";

        public static AiFinding FromFinding(ConsistencyFinding nalez) => new AiFinding
        {
            Severity = nalez == null ? "Varovani" : nalez.Severity.ToString(),
            Title = nalez != null ? (nalez.Title ?? "") : "",
            Detail = nalez != null ? (nalez.Detail ?? "") : ""
        };
    }

    /// <summary>Whole project / specification model.</summary>
    public class ProjectSpecification
    {
        public string Name { get; set; } = "";
        public string Idea { get; set; } = "";
        public ProjectType ProjectType { get; set; } = ProjectType.General;

        private string _projectTypeKey = null;
        public string ProjectTypeKey
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_projectTypeKey))
                {
                    return ProjectType.ToString();
                }
                return _projectTypeKey;
            }
            set => _projectTypeKey = value;
        }

        public string ReferenceText { get; set; } = null;
        public string ReferenceName { get; set; } = null;
        public string MockupBase64 { get; set; } = null;
        public string MockupName { get; set; } = null;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public int Version { get; set; } = 1;
        public List<Answer> Answers { get; set; } = new List<Answer>();
        public List<DecisionLogEntry> ChangeLog { get; set; } = new List<DecisionLogEntry>();
        public List<Question> Questions { get; set; } = new List<Question>();
        public List<UserStory> UserStories { get; set; } = new List<UserStory>();
        public List<ChatMessage> ChatHistory { get; set; } = new List<ChatMessage>();
        public ProjectMetrics Metrics { get; set; } = new ProjectMetrics();

        public DateTime? StoriesGenerationTimestamp { get; set; }
        public List<AiFinding> AiFindings { get; set; } = new List<AiFinding>();
        public DateTime? AiCheckTimestamp { get; set; }
    }

    /// <summary>Standard list of guided questions.</summary>
    public static class StandardQuestions
    {
        public static readonly IReadOnlyList<Question> All = new List<Question>
        {
            new Question { Id = "cil-problem", Section = "Cíl a uživatelé", Impact = Impact.High,
                Text = "Jaký problém má aplikace vyřešit a jaký přínos od ní čekáš?",
                HelpText = "Určuje smysl celé specifikace – všechno ostatní se od toho odvíjí.",
                DefaultAssumption = "Přínos je popsán jen v původním nápadu; bude upřesněn po první ukázce.",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Jaký zážitek má hra přinést a jaký je cíl hráče?" },
                    { ProjectType.Registry, "Jaké objekty se budou evidovat a jaký přínos má jejich přehled přinést?" },
                    { ProjectType.Tool, "Jaký úkol nebo operaci má nástroj automatizovat a co je cílem?" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "U her je hlavním přínosem zábava, hratelnost, odreagování nebo skóre." },
                    { ProjectType.Registry, "U evidencí jde o přehlednost, rychlé vyhledávání a spolehlivé ukládání." },
                    { ProjectType.Tool, "U nástrojů jde o úsporu času a eliminaci lidských chyb při rutinní práci." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Hra pro zábavu, cíl hráče je dosáhnout co nejvyššího skóre." },
                    { ProjectType.Registry, "Evidence a přehledné ukládání specifických záznamů s možností filtrování." },
                    { ProjectType.Tool, "Jednoúčelový nástroj pro automatizaci specifického úkonu a úsporu času." }
                }
            },

            new Question { Id = "cil-uzivatele", Section = "Cíl a uživatelé", Impact = Impact.High,
                Text = "Kdo bude aplikaci používat? (role, zkušenost s počítačem, kolik lidí)",
                HelpText = "Jiné UX pro recepční, jiné pro vývojáře. Ovlivňuje složitost rozhraní.",
                DefaultAssumption = "Jediný uživatel – autor nápadu.",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Pro jaké hráče je hra určena? (věk, občasný hráč, hardcore, lokální multiplayer?)" },
                    { ProjectType.Registry, "Kdo bude data spravovat a kdo je bude jen prohlížet? (role, oprávnění)" },
                    { ProjectType.Tool, "Kdo je typickým uživatelem nástroje a jaké má technické znalosti?" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Ovlivňuje obtížnost, ovládání a přítomnost hry pro více hráčů." },
                    { ProjectType.Registry, "Určuje, zda potřebujeme různé uživatelské účty a úroveň přístupu." },
                    { ProjectType.Tool, "Nástroje často spouští vývojáři přes CLI, nebo uživatelé v jednoduchém GUI." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Pro běžné občasné hráče všech věkových kategorií, jednoduché ovládání." },
                    { ProjectType.Registry, "Administrátor vkládá a mění záznamy, běžný uživatel pouze čte." },
                    { ProjectType.Tool, "Technicky zdatný uživatel, který rozumí spouštěnému úkolu." }
                }
            },

            new Question { Id = "rozsah-funkce", Section = "Rozsah", Impact = Impact.High,
                Text = "Jaké jsou 3 nejdůležitější funkce, bez kterých aplikace nemá smysl?",
                HelpText = "Tzv. MVP (Minimum Viable Product). Zbytek věcí se odloží na později.",
                DefaultAssumption = "MVP obsahuje základní zobrazení a editaci hlavního objektu.",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Jaké jsou hlavní herní mechaniky? (např. pohyb, střílení, sbírání bodů)" },
                    { ProjectType.Registry, "Jaké 3 hlavní operace musíme se záznamy umět? (např. import, filtry, tisk)" },
                    { ProjectType.Tool, "Jaké jsou klíčové kroky zpracování? (např. načtení vstupu, transformace, zápis)" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Základní smyčka hry (gameplay loop), kterou hráč dělá 90 % času." },
                    { ProjectType.Registry, "Například rychlé Fulltext vyhledávání, export do Excelu a editace polí." },
                    { ProjectType.Tool, "Popiš sekvenci kroků, jak nástroj zpracuje vstup na požadovaný výstup." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Pohyb postavy, interakce s překážkami a počítadlo skóre." },
                    { ProjectType.Registry, "Vytvoření záznamu, vyhledávání podle názvu a export do CSV." },
                    { ProjectType.Tool, "Načtení konfiguračního souboru, provedení analýzy a výpis chyb." }
                }
            },

            new Question { Id = "rozsah-nongoals", Section = "Rozsah", Impact = Impact.Medium,
                Text = "Co aplikace v první verzi rozhodně DĚLAT NEBUDE? (tzv. Non-Goals)",
                HelpText = "Důležité pro vymezení hranic – např. žádná mobilní aplikace, žádný cloud, žádné role.",
                DefaultAssumption = "První verze neřeší přihlašování uživatelů, synchronizaci ani design na míru.",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Co ve hře v první verzi určitě nebude? (multiplayer, 3D grafika, žebříčky online...)" },
                    { ProjectType.Registry, "Jaké funkce odložíme na druhou verzi? (historie změn, hromadné úpravy, API...)" },
                    { ProjectType.Tool, "Co nástroj nebude řešit? (automatické spouštění, GUI rozhraní, složité formáty...)" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Udržujte rozsah při zemi. Zlaté pravidlo: dělejte nejdřív lokální singleplayer." },
                    { ProjectType.Registry, "U evidencí se často odkládá pokročilý reporting a automatické notifikace." },
                    { ProjectType.Tool, "CLI nástroje obvykle v první verzi neřeší grafické klikátko ani integraci do OS." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Bez online multiplayeru, bez mikrotransakcí, pouze lokální uložení skóre." },
                    { ProjectType.Registry, "Bez napojení na externí fakturační systémy, bez hromadných úprav dat." },
                    { ProjectType.Tool, "Bez plánovače úloh (cronu) a bez webového API pro integraci." }
                }
            },

            new Question { Id = "ux-styl", Section = "UX", Impact = Impact.Medium,
                Text = "Jaký vzhled a styl rozhraní preferuješ? (minimalistické, tmavý režim, retro...)",
                HelpText = "Určuje designový systém a estetické nároky na aplikaci.",
                DefaultAssumption = "Čistý, moderní vzhled s podporou tmavého režimu (dark mode).",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Jaký grafický styl a pohled kamera využívá? (2D z boku, retro pixel art, 3D...)" },
                    { ProjectType.Registry, "Je důležitější hustota informací (tabulky), nebo vzdušný design (karty)?" },
                    { ProjectType.Tool, "Preferuješ čistě textové CLI (příkazová řádka), nebo jednoduché GUI formuláře?" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Estetika hry zásadně ovlivňuje náročnost na grafické assety." },
                    { ProjectType.Registry, "Administrační systémy vyžadují spíše přehledné a husté tabulky pro rychlou práci." },
                    { ProjectType.Tool, "CLI rozhraní je rychlejší na vývoj a oblíbené u programátorů." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Jednoduchá 2D grafika, pohled shora, čisté barvy." },
                    { ProjectType.Registry, "Tabulkové zobrazení s fixním záhlavím, zaměření na čitelnost a kontrast." },
                    { ProjectType.Tool, "Jednoduché okno se vstupními poli a jedním spouštěcím tlačítkem." }
                }
            },

            new Question { Id = "tech-platforma", Section = "Technika", Impact = Impact.High,
                Text = "Na jakých zařízeních a platformách má aplikace běžet? (web, Windows, mobil...)",
                HelpText = "Rozhodující pro volbu technologií (C#, React, Swift...).",
                DefaultAssumption = "Desktopová aplikace pro Windows 10/11.",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Kde se bude hra hrát? (v prohlížeči, na mobilu, na PC/Steam, konzole?)" },
                    { ProjectType.Registry, "Jak budou uživatelé k evidenci přistupovat? (z vnitřní sítě, z internetu, z mobilu)" },
                    { ProjectType.Tool, "Kde se bude nástroj spouštět? (lokálně na vývojářském PC, na serveru v Linuxu...)" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Prohlížeč (HTML5/WASM) je nejsnadnější pro rychlé sdílení s hráči." },
                    { ProjectType.Registry, "Webová aplikace v prohlížeči je dnes standardem pro většinu evidencí." },
                    { ProjectType.Tool, "Nástroje v .NET nebo Pythonu lze snadno kompilovat pro Windows i Linux." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Webová hra běžící v moderním prohlížeči bez instalace." },
                    { ProjectType.Registry, "Webová aplikace přístupná přes standardní prohlížeč z vnitřní sítě." },
                    { ProjectType.Tool, "Lokální konzolová aplikace (CLI) pro Windows i Linux." }
                }
            },

            new Question { Id = "tech-offline", Section = "Technika", Impact = Impact.High,
                Text = "Musí aplikace fungovat plně offline, nebo se počítá se stálým internetem?",
                HelpText = "Offline vyžaduje lokální databázi (SQLite), online využívá cloud API.",
                DefaultAssumption = "Plně online aplikace vyžadující připojení k internetu.",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Ukládá se stav hry a skóre lokálně na zařízení, nebo na cloudový server?" },
                    { ProjectType.Registry, "Lze data upravovat bez připojení (offline synchronizace), nebo jen online?" },
                    { ProjectType.Tool, "Bude nástroj volat nějaké externí API, nebo pracuje čistě s lokálními soubory?" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Lokální ukládání (LocalStorage) je nejjednodušší, cloud umožňuje žebříčky." },
                    { ProjectType.Registry, "Offline-first aplikace s pozdější synchronizací jsou technicky velmi náročné." },
                    { ProjectType.Tool, "Pokud nástroj zpracovává citlivá data, offline provoz zvyšuje bezpečnost." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Lokální ukládání postupu na daném zařízení (IndexedDB / LocalStorage)." },
                    { ProjectType.Registry, "Vyžaduje stálé připojení k internetu a centrální databázi." },
                    { ProjectType.Tool, "Čistě lokální zpracování souborů bez jakéhokoliv síťového provozu." }
                }
            },

            new Question { Id = "data-obsah", Section = "Data", Impact = Impact.High,
                Text = "Jaká data budeme ukládat a jak dlouho je potřebujeme uchovat?",
                HelpText = "Určuje nároky na datové úložiště, databáze a bezpečnost dat.",
                DefaultAssumption = "Data se ukládají trvale do lokální databáze (např. SQLite).",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Ukládá se rozehraná pozice (save game), nastavení a nejvyšší skóre?" },
                    { ProjectType.Registry, "Jaké konkrétní informace o objektech ukládáme a jak velká data to budou?" },
                    { ProjectType.Tool, "Ukládá nástroj nějakou historii zpracování, nebo funguje bezstavově?" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Bezstavové hry (arkády) ukládají jen skóre, RPG vyžadují robustní serializaci." },
                    { ProjectType.Registry, "Evidenční karty obsahují texty, čísla, data založení a případně přílohy." },
                    { ProjectType.Tool, "Bezstavový nástroj (input->output) je řádově jednodušší a bezpečnější." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Ukládá se pouze tabulka s 10 nejlepšími výsledky (jméno a skóre)." },
                    { ProjectType.Registry, "Ukládají se strukturovaná textová a číselná data s historií změn." },
                    { ProjectType.Tool, "Bezstavový nástroj – nastavení se načítá z parametrů příkazové řádky." }
                }
            },

            new Question { Id = "data-export", Section = "Data", Impact = Impact.Medium,
                Text = "Vyžaduješ export dat pro jiné systémy? (např. do Excelu, CSV, PDF)",
                HelpText = "Klíčové pro reporty a přenositelnost dat. Často se na to zapomíná.",
                DefaultAssumption = "Není vyžadován žádný automatický export dat do externích formátů.",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Bude možné skóre nebo replay sdílet na sociální sítě či exportovat obrázek?" },
                    { ProjectType.Registry, "Potřebují manažeři export tabulek do Excelu (XLSX) nebo PDF pro tisk?" },
                    { ProjectType.Tool, "V jakém formátu nástroj vrací výsledky? (JSON na konzoli, HTML report, log...)" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Generování screenshotu s výsledkem hry je skvělý marketingový nástroj." },
                    { ProjectType.Registry, "Export do CSV/XLSX je téměř vždy nutností pro další analýzu dat." },
                    { ProjectType.Tool, "Strojově čitelný výstup (JSON/XML) umožňuje napojit nástroj do CI/CD pipeline." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Sdílení výsledku formou vygenerovaného obrázku se statistikou." },
                    { ProjectType.Registry, "Možnost exportovat aktuálně vyfiltrovanou tabulku do CSV (pro Excel)." },
                    { ProjectType.Tool, "Nástroj zapisuje výsledky do standardního textového logu a JSON souboru." }
                }
            },

            new Question { Id = "rizika-reseni", Section = "Rizika", Impact = Impact.Medium,
                Text = "Co je největší riziko projektu a jak ho vyřešíme v první verzi?",
                HelpText = "Např. výpadek internetu, ztráta dat, pomalé reakce, složité ovládání.",
                DefaultAssumption = "Největším rizikem je ztráta dat; vyřešíme ji automatickým lokálním ukládáním.",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Co by mohlo hráče nejvíc otrávit? (špatné ovládání, ztráta savu, stereotyp...)" },
                    { ProjectType.Registry, "Co se stane, když systém vypadne? (dostupnost, zálohování, obnova dat)" },
                    { ProjectType.Tool, "Co když nástroj dostane neplatný nebo příliš velký vstup? (chybové stavy)" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Špatné ovládání na mobilu spolehlivě zabije i skvělou hru." },
                    { ProjectType.Registry, "U firemních evidencí je klíčová rychlost obnovy ze zálohy v případě havárie." },
                    { ProjectType.Tool, "Nástroj by měl vždy provést validaci vstupu a vypsat srozumitelnou chybu." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Ztráta rozehrané hry; vyřešíme automatickým ukládáním při každé změně scény." },
                    { ProjectType.Registry, "Ztráta dat; vyřešíme denním automatickým zálohováním databáze na cloud." },
                    { ProjectType.Tool, "Pád aplikace při chybě ve vstupu; vyřešíme důsledným ošetřením výjimek a validací." }
                }
            },

            new Question { Id = "akceptace", Section = "Akceptace", Impact = Impact.Medium,
                Text = "Jak poznáme, že je aplikace hotová a funguje správně? (akceptační kritéria)",
                HelpText = "Definuje úspěšné dokončení – např. 'projde scénář nákupu', 'načte 1000 položek pod 2s'.",
                DefaultAssumption = "Projekt je hotový, pokud splňuje všechny body specifikace a projde ručním testem.",
                Texts = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Kdy je hra připravená k vydání? (projití celého levelu bez chyb, stabilní FPS)" },
                    { ProjectType.Registry, "Jaké konkrétní testy musí systém splnit před nasazením do ostrého provozu?" },
                    { ProjectType.Tool, "Jak ověříme správnost výstupu? (referenční vstupy a porovnání s očekávaným výsledkem)" }
                },
                Helps = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Obvykle se testuje průchod hry od startu do konce a měří se plynulost (např. 60 FPS)." },
                    { ProjectType.Registry, "Například úspěšné vložení, vyhledání, úprava a smazání testovacího záznamu." },
                    { ProjectType.Tool, "Automatické testy porovnávající vygenerovaný soubor s referenčním vzorem." }
                },
                Assumptions = new Dictionary<ProjectType, string>
                {
                    { ProjectType.Game, "Hráč může projít úvodní tutoriál a první 3 herní úrovně bez pádu aplikace." },
                    { ProjectType.Registry, "Projmutí kompletního scénáře: registrace uživatele, vytvoření záznamu, export." },
                    { ProjectType.Tool, "Nástroj správně zpracuje všech 5 přiložených testovacích scénářů." }
                }
            }
        };

        public static Question Under(ProjectSpecification p, string id)
            => All.FirstOrDefault(o => o.Id == id);
    }

    public static class SpecificationService
    {
        // Pevné pořadí sekcí pro dokumentaci a formuláře (kap. 7)
        public static readonly IReadOnlyList<string> SectionOrder = new List<string>
        {
            "Cíl a uživatelé",
            "Rozsah",
            "UX",
            "Data",
            "Technika",
            "Akceptace",
            "Rizika"
        };

        public static List<string> GetAllSections(ProjectSpecification p)
        {
            var projectSections = GetProjectQuestions(p).Select(o => o.Section).Distinct();
            var list = new List<string>(SectionOrder);
            foreach (var s in projectSections)
            {
                if (!string.IsNullOrWhiteSpace(s) && !list.Contains(s))
                {
                    list.Add(s);
                }
            }
            return list;
        }

        private static readonly JsonSerializerOptions JsonOpt = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new JsonStringEnumConverter() }
        };

        // ---------- state changes ----------

        public static string GetProjectTypeName(ProjectType typ) => GetProjectTypeName(typ.ToString());

        public static string GetProjectTypeName(string typeKey)
        {
            if (Enum.TryParse<ProjectType>(typeKey, true, out var enumTyp))
            {
                return enumTyp switch
                {
                    ProjectType.Game => "Hra (Game)",
                    ProjectType.Registry => "Evidence / Registr",
                    ProjectType.Tool => "Nástroj / Utilita",
                    _ => "Obecná aplikace"
                };
            }

            var template = TemplateService.CustomTemplates.FirstOrDefault(s => string.Equals(s.Key, typeKey, StringComparison.OrdinalIgnoreCase));
            if (template != null) return template.Name;

            return typeKey;
        }

        public static void ChangeProjectType(ProjectSpecification p, ProjectType newType) => ChangeProjectType(p, newType.ToString());

        public static void ChangeProjectType(ProjectSpecification p, string newTypeKey)
        {
            if (p.ProjectTypeKey == newTypeKey) return;
            var oldTypeKey = p.ProjectTypeKey;
            p.ProjectTypeKey = newTypeKey;

            if (Enum.TryParse<ProjectType>(newTypeKey, true, out var enumTyp))
            {
                p.ProjectType = enumTyp;
            }
            else
            {
                p.ProjectType = ProjectType.General;
            }

            // Update all automatic assumptions
            foreach (var ot in GetProjectQuestions(p))
            {
                var odp = GetAnswerFor(p, ot.Id);
                if (odp != null && odp.IsAssumption)
                {
                    odp.Text = ot.GetDefaultAssumption(newTypeKey);
                    odp.Timestamp = DateTime.Now;
                }
            }

            LogChange(p, "Typ projektu", $"Změna typu projektu z {GetProjectTypeName(oldTypeKey)} na {GetProjectTypeName(newTypeKey)}.");
        }

        public static void SetIdea(ProjectSpecification p, string idea)
        {
            if ((p.Idea ?? "") == (idea ?? "")) return;
            p.Idea = idea ?? "";
            LogChange(p, "Nápad", "Upraven text původního nápadu.");
        }

        public static void AnswerQuestion(ProjectSpecification p, string questionId, string text)
        {
            var ot = GetQuestionById(p, questionId);
            if (ot == null || string.IsNullOrWhiteSpace(text)) return;

            var stara = p.Answers.FirstOrDefault(o => o.QuestionId == questionId);
            if (stara != null) p.Answers.Remove(stara);

            string novyText = text.Trim();
            p.Answers.Add(new Answer { QuestionId = questionId, Text = novyText, IsAssumption = false, Timestamp = DateTime.Now });

            string detail;
            if (stara != null && stara.Text != novyText)
            {
                detail = ot.GetText(p.ProjectTypeKey) + " → bylo: '" + ShortenText(stara.Text, 120) + "' → je: '" + ShortenText(novyText, 120) + "'";
            }
            else
            {
                detail = ot.GetText(p.ProjectTypeKey) + " → " + ShortenText(novyText, 120);
            }
            LogChange(p, stara == null ? "Odpověď" : "Změna odpovědi", detail);
        }

        public static void UseAssumption(ProjectSpecification p, string questionId)
        {
            var ot = GetQuestionById(p, questionId);
            if (ot == null) return;

            var stara = p.Answers.FirstOrDefault(o => o.QuestionId == questionId);
            if (stara != null) p.Answers.Remove(stara);

            p.Answers.Add(new Answer { QuestionId = questionId, Text = ot.GetDefaultAssumption(p.ProjectTypeKey), IsAssumption = true, Timestamp = DateTime.Now });
            LogChange(p, "Předpoklad", ot.GetText(p.ProjectTypeKey) + " → [PŘEDPOKLAD] " + ot.GetDefaultAssumption(p.ProjectTypeKey));
        }

        public static void LogChange(ProjectSpecification p, string action, string detail)
        {
            p.Version++;
            p.UpdatedAt = DateTime.Now;
            p.ChangeLog.Add(new DecisionLogEntry { Timestamp = DateTime.Now, Action = action, Detail = detail });
        }

        // ---------- status queries ----------

        public static Answer GetAnswerFor(ProjectSpecification p, string questionId)
            => p.Answers.FirstOrDefault(o => o.QuestionId == questionId);

        public static Question GetNextUnansweredQuestion(ProjectSpecification p)
            => GetProjectQuestions(p).FirstOrDefault(ot => GetAnswerFor(p, ot.Id) == null);

        public static int GetAnsweredCount(ProjectSpecification p)
            => GetProjectQuestions(p).Count(ot => { var o = GetAnswerFor(p, ot.Id); return o != null && !o.IsAssumption; });

        public static int GetAssumptionsCount(ProjectSpecification p)
            => GetProjectQuestions(p).Count(ot => { var o = GetAnswerFor(p, ot.Id); return o != null && o.IsAssumption; });

        public static List<Question> GetOpenQuestions(ProjectSpecification p)
            => GetProjectQuestions(p).Where(ot => GetAnswerFor(p, ot.Id) == null).ToList();

        public static bool AreMetricsOutdated(ProjectSpecification p)
            => p != null && p.Metrics != null && p.Metrics.CalculationTimestamp != default && p.Metrics.CalculationTimestamp < p.UpdatedAt;

        public static bool AreStoriesOutdated(ProjectSpecification p)
            => p != null && p.UserStories != null && p.UserStories.Count > 0
               && p.StoriesGenerationTimestamp.HasValue && p.StoriesGenerationTimestamp.Value < p.UpdatedAt;

        public const string OutdatedMetricsNote = "⚠ Odhad byl vygenerován pro starší verzi specifikace – doporučujeme přepočítat.";
        public const string OutdatedStoriesNote = "⚠ User stories byly vygenerovány pro starší verzi specifikace – doporučujeme je vygenerovat znovu.";

        // ---------- rendering ----------

        private static string FormatDate(DateTime d) => d.ToString("d'.' M'.' yyyy H':'mm");

        private static string ShortenText(string s, int max)
        {
            s = (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }

        public static string RenderMarkdown(ProjectSpecification p)
        {
            var sb = new StringBuilder();
            string name = string.IsNullOrWhiteSpace(p.Name) ? "(nepojmenovaný projekt)" : p.Name.Trim();

            sb.AppendLine("# Specifikace: " + name);
            sb.AppendLine("*Typ projektu: " + GetProjectTypeName(p.ProjectTypeKey) + "*");
            sb.AppendLine("*Verze specifikace " + p.Version + " · aktualizováno " + FormatDate(p.UpdatedAt) + "*");
            sb.AppendLine("*Vytvořeno nástrojem CodePlanner*");
            sb.AppendLine();

            sb.AppendLine("## Původní nápad");
            if (string.IsNullOrWhiteSpace(p.Idea))
                sb.AppendLine("> (zatím nezadán – napiš nebo nadiktuj svůj nápad)");
            else
                foreach (var radek in p.Idea.Trim().Split('\n'))
                    sb.AppendLine("> " + radek.TrimEnd());
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(p.ReferenceText))
            {
                sb.AppendLine("## Referenční podklady (" + (p.ReferenceName ?? "příloha") + ")");
                sb.AppendLine("```text");
                sb.AppendLine(p.ReferenceText.Trim());
                sb.AppendLine("```");
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(p.MockupName))
            {
                sb.AppendLine("## Vizuální nákres rozhraní (Mockup)");
                sb.AppendLine("- Přiložen vizuální návrh: **" + p.MockupName + "** (obrázek odesílán jako vizuální kontext do Gemini)");
                sb.AppendLine();
            }

            foreach (var sekce in GetAllSections(p))
            {
                sb.AppendLine("## " + sekce);
                var otazkySekce = GetProjectQuestions(p).Where(o => o.Section == sekce).ToList();
                bool neco = false;

                foreach (var ot in otazkySekce)
                {
                    var odp = GetAnswerFor(p, ot.Id);
                    if (odp == null) continue;
                    neco = true;
                    string znacka = odp.IsAssumption ? " **[PŘEDPOKLAD]**" : "";
                    sb.AppendLine("- **" + ot.GetText(p.ProjectTypeKey) + "**" + znacka);
                    foreach (var radek in odp.Text.Trim().Split('\n'))
                        sb.AppendLine("  " + radek.TrimEnd());
                }

                if (!neco) sb.AppendLine("- *(zatím bez rozhodnutí)*");
                sb.AppendLine();
            }

            var otevrene = GetOpenQuestions(p);
            sb.AppendLine("## Otevřené otázky");
            if (otevrene.Count == 0)
                sb.AppendLine("- *(žádné – všechny otázky jsou vyřešené)*");
            else
                foreach (var ot in otevrene)
                    sb.AppendLine("- [" + (ot.Impact == Impact.High ? "vysoký dopad" : "střední dopad") + "] " + ot.GetText(p.ProjectTypeKey));
            sb.AppendLine();

            var nalezy = ConsistencyChecker.Check(p);
            if (nalezy.Count > 0)
            {
                sb.AppendLine("## Kontrola konzistence");
                foreach (var n in nalezy)
                    sb.AppendLine("- " + (n.Severity == Severity.Conflict ? "❗ **ROZPOR: " : "⚠️ **Varování: ") + n.Title + "** – " + n.Detail);
                sb.AppendLine();
            }

            sb.AppendLine("## Souhrn stavu");
            sb.AppendLine("- Zodpovězeno: " + GetAnsweredCount(p) + " / " + GetProjectQuestions(p).Count());
            sb.AppendLine("- Označené předpoklady: " + GetAssumptionsCount(p));
            sb.AppendLine("- Otevřené otázky: " + otevrene.Count);
            if (AreMetricsOutdated(p))
                sb.AppendLine("- " + OutdatedMetricsNote);
            if (AreStoriesOutdated(p))
                sb.AppendLine("- " + OutdatedStoriesNote);
            sb.AppendLine();

            sb.AppendLine("## Log rozhodnutí");
            if (p.ChangeLog.Count == 0)
                sb.AppendLine("- *(zatím žádná rozhodnutí)*");
            else
                foreach (var r in p.ChangeLog)
                    sb.AppendLine("- " + FormatDate(r.Timestamp) + " · **" + r.Action + "** · " + r.Detail);

            return sb.ToString();
        }

        public static string RenderJson(ProjectSpecification p)
        {
            var sections = new List<object>();
            foreach (var sectionName in GetAllSections(p))
            {
                var items = new List<object>();
                foreach (var ot in GetProjectQuestions(p).Where(o => o.Section == sectionName))
                {
                    var odp = GetAnswerFor(p, ot.Id);
                    if (odp == null) continue;
                    items.Add(new { id = ot.Id, question = ot.GetText(p.ProjectTypeKey), answer = odp.Text, assumption = odp.IsAssumption });
                }
                sections.Add(new { name = sectionName, items });
            }

            var data = new
            {
                tool = "CodePlanner",
                toolVersion = "2.1.0",
                project = p.Name,
                projectType = p.ProjectTypeKey,
                projectTypeName = GetProjectTypeName(p.ProjectTypeKey),
                specificationVersion = p.Version,
                createdAt = p.CreatedAt,
                updatedAt = p.UpdatedAt,
                idea = p.Idea,
                referenceText = p.ReferenceText,
                referenceName = p.ReferenceName,
                sections,
                openQuestions = GetOpenQuestions(p).Select(o => new { id = o.Id, question = o.GetText(p.ProjectTypeKey) }).ToList(),
                consistencyCheck = ConsistencyChecker.Check(p)
                    .Select(n => new { severity = n.Severity.ToString(), title = n.Title, detail = n.Detail }).ToList(),
                decisionLog = p.ChangeLog.Select(r => new { timestamp = r.Timestamp, action = r.Action, detail = r.Detail }).ToList()
            };

            return JsonSerializer.Serialize(data, JsonOpt);
        }

        public static string RenderHtml(ProjectSpecification p)
        {
            var sb = new StringBuilder();
            string name = string.IsNullOrWhiteSpace(p.Name) ? "(nepojmenovaný projekt)" : p.Name.Trim();
            
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"cs\">");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine($"    <title>Specifikace: {System.Net.WebUtility.HtmlEncode(name)}</title>");
            sb.AppendLine("    <link href=\"https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap\" rel=\"stylesheet\">");
            sb.AppendLine("    <style>");
            sb.AppendLine("        :root {");
            sb.AppendLine("            --primary: #10233F;");
            sb.AppendLine("            --accent: #17B0A0;");
            sb.AppendLine("            --bg-page: #f5f7fa;");
            sb.AppendLine("            --bg-card: #ffffff;");
            sb.AppendLine("            --text: #212529;");
            sb.AppendLine("            --text-light: #6c757d;");
            sb.AppendLine("            --border: #dee2e6;");
            sb.AppendLine("            --prio-high: #dc3545;");
            sb.AppendLine("            --prio-med: #ffc107;");
            sb.AppendLine("            --prio-low: #28a745;");
            sb.AppendLine("        }");
            sb.AppendLine("        [data-theme=\"dark\"] {");
            sb.AppendLine("            --primary: #1e293b;");
            sb.AppendLine("            --accent: #2dd4bf;");
            sb.AppendLine("            --bg-page: #0f172a;");
            sb.AppendLine("            --bg-card: #1e293b;");
            sb.AppendLine("            --text: #f8fafc;");
            sb.AppendLine("            --text-light: #94a3b8;");
            sb.AppendLine("            --border: #334155;");
            sb.AppendLine("        }");
            sb.AppendLine("        * { box-sizing: border-box; margin: 0; padding: 0; transition: background-color 0.3s, border-color 0.3s; }");
            sb.AppendLine("        body { font-family: 'Inter', sans-serif; background-color: var(--bg-page); color: var(--text); line-height: 1.6; padding: 20px; }");
            sb.AppendLine("        .container { max-width: 1100px; margin: 0 auto; }");
            sb.AppendLine("        header { background-color: var(--primary); color: white; padding: 30px; border-radius: 12px; margin-bottom: 20px; position: relative; box-shadow: 0 4px 6px rgba(0,0,0,0.1); }");
            sb.AppendLine("        h1 { font-size: 2.2rem; margin-bottom: 8px; }");
            sb.AppendLine("        .meta-subtitle { font-size: 0.95rem; color: #cbd5e1; display: flex; gap: 15px; flex-wrap: wrap; }");
            sb.AppendLine("        .theme-switch { position: absolute; top: 30px; right: 30px; background-color: rgba(255,255,255,0.1); border: 1px solid rgba(255,255,255,0.2); color: white; padding: 8px 12px; border-radius: 20px; cursor: pointer; font-size: 0.85rem; font-weight: 500; display: flex; align-items: center; gap: 8px; }");
            sb.AppendLine("        .theme-switch:hover { background-color: rgba(255,255,255,0.2); }");
            sb.AppendLine("        .search-bar-container { margin-bottom: 20px; }");
            sb.AppendLine("        .search-input { width: 100%; padding: 12px 20px; font-size: 1rem; border-radius: 8px; border: 1px solid var(--border); background-color: var(--bg-card); color: var(--text); outline: none; box-shadow: 0 2px 4px rgba(0,0,0,0.02); }");
            sb.AppendLine("        .search-input:focus { border-color: var(--accent); }");
            sb.AppendLine("        .dashboard-grid { display: grid; grid-template-columns: 2fr 1fr; gap: 20px; }");
            sb.AppendLine("        @media (max-width: 900px) { .dashboard-grid { grid-template-columns: 1fr; } }");
            sb.AppendLine("        .card { background-color: var(--bg-card); border: 1px solid var(--border); border-radius: 12px; padding: 24px; margin-bottom: 20px; box-shadow: 0 2px 4px rgba(0,0,0,0.02); }");
            sb.AppendLine("        .card-title { font-size: 1.25rem; font-weight: 600; color: var(--primary); border-bottom: 2px solid var(--accent); padding-bottom: 8px; margin-bottom: 16px; display: flex; align-items: center; justify-content: space-between; }");
            sb.AppendLine("        [data-theme=\"dark\"] .card-title { color: var(--accent); }");
            sb.AppendLine("        .napad-quote { border-left: 4px solid var(--accent); padding: 8px 16px; background-color: rgba(23, 176, 160, 0.05); font-style: italic; border-radius: 0 8px 8px 0; margin-bottom: 12px; }");
            sb.AppendLine("        .spec-item { margin-bottom: 16px; border-bottom: 1px solid var(--border); padding-bottom: 12px; }");
            sb.AppendLine("        .spec-item:last-child { border-bottom: none; padding-bottom: 0; margin-bottom: 0; }");
            sb.AppendLine("        .spec-question { font-weight: 600; font-size: 0.95rem; margin-bottom: 4px; }");
            sb.AppendLine("        .spec-answer { font-size: 0.95rem; }");
            sb.AppendLine("        .badge { display: inline-block; padding: 2px 8px; font-size: 0.75rem; font-weight: 600; border-radius: 12px; margin-left: 8px; }");
            sb.AppendLine("        .badge-prio { color: white; }");
            sb.AppendLine("        .badge-prio-high { background-color: var(--prio-high); }");
            sb.AppendLine("        .badge-prio-med { background-color: var(--prio-med); color: #000; }");
            sb.AppendLine("        .badge-prio-low { background-color: var(--prio-low); }");
            sb.AppendLine("        .badge-predpoklad { background-color: #e2e8f0; color: #475569; }");
            sb.AppendLine("        [data-theme=\"dark\"] .badge-predpoklad { background-color: #334155; color: #cbd5e1; }");
            sb.AppendLine("        .metric-cards-container { display: grid; grid-template-columns: repeat(2, 1fr); gap: 12px; margin-bottom: 16px; }");
            sb.AppendLine("        .metric-mini-card { background-color: var(--bg-page); border: 1px solid var(--border); border-radius: 8px; padding: 12px; }");
            sb.AppendLine("        .metric-mini-label { font-size: 0.75rem; font-weight: 700; color: var(--text-light); text-transform: uppercase; margin-bottom: 4px; }");
            sb.AppendLine("        .metric-mini-value { font-size: 1rem; font-weight: 600; }");
            sb.AppendLine("        .backlog-item { display: flex; align-items: flex-start; gap: 12px; padding: 12px 0; border-bottom: 1px dashed var(--border); }");
            sb.AppendLine("        .backlog-item:last-child { border-bottom: none; }");
            sb.AppendLine("        .backlog-checkbox { margin-top: 5px; width: 16px; height: 16px; cursor: pointer; }");
            sb.AppendLine("        .backlog-text { flex: 1; }");
            sb.AppendLine("        .backlog-title { font-weight: 600; font-size: 0.95rem; margin-bottom: 2px; }");
            sb.AppendLine("        .backlog-desc { font-size: 0.85rem; color: var(--text-light); margin-bottom: 6px; }");
            sb.AppendLine("        .backlog-criteria-list { padding-left: 15px; font-size: 0.85rem; }");
            sb.AppendLine("        .backlog-criteria-item { list-style-type: square; margin-bottom: 2px; }");
            sb.AppendLine("        .completed .backlog-title { text-decoration: line-through; color: var(--text-light); }");
            sb.AppendLine("        .finding-warning { border-left: 4px solid var(--prio-med); background-color: rgba(255,193,7,0.05); padding: 8px 12px; margin-bottom: 8px; border-radius: 0 6px 6px 0; font-size: 0.85rem; }");
            sb.AppendLine("        .finding-conflict { border-left: 4px solid var(--prio-high); background-color: rgba(220,53,69,0.05); padding: 8px 12px; margin-bottom: 8px; border-radius: 0 6px 6px 0; font-size: 0.85rem; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("<body>");
            sb.AppendLine("    <script>");
            sb.AppendLine("        (function() {");
            sb.AppendLine("            const savedTheme = localStorage.getItem('theme');");
            sb.AppendLine("            let theme = 'light';");
            sb.AppendLine("            if (savedTheme) {");
            sb.AppendLine("                theme = savedTheme;");
            sb.AppendLine("            } else if (window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches) {");
            sb.AppendLine("                theme = 'dark';");
            sb.AppendLine("            }");
            sb.AppendLine("            document.body.setAttribute('data-theme', theme);");
            sb.AppendLine("        })();");
            sb.AppendLine("    </script>");
            sb.AppendLine("    <div class=\"container\">");
            sb.AppendLine("        <header>");
            sb.AppendLine($"            <h1>Specifikace: {System.Net.WebUtility.HtmlEncode(name)}</h1>");
            sb.AppendLine("            <div class=\"meta-subtitle\">");
            sb.AppendLine($"                <span>Typ projektu: <strong>{System.Net.WebUtility.HtmlEncode(GetProjectTypeName(p.ProjectTypeKey))}</strong></span>");
            sb.AppendLine($"                <span>Verze: <strong>{p.Version}</strong></span>");
            sb.AppendLine($"                <span>Aktualizováno: <strong>{FormatDate(p.UpdatedAt)}</strong></span>");
            sb.AppendLine("            </div>");
            sb.AppendLine("            <button class=\"theme-switch\" onclick=\"toggleTheme()\">");
            sb.AppendLine("                <span id=\"theme-icon\">🌙</span> <span id=\"theme-label\">Tmavý režim</span>");
            sb.AppendLine("            </button>");
            sb.AppendLine("        </header>");
            sb.AppendLine();
            sb.AppendLine("        <div class=\"search-bar-container\">");
            sb.AppendLine("            <input type=\"text\" class=\"search-input\" id=\"searchInput\" onkeyup=\"filterContent()\" placeholder=\"Vyhledat v otázkách, odpovědích nebo backlogu...\">");
            sb.AppendLine("        </div>");
            sb.AppendLine();
            sb.AppendLine("        <div class=\"dashboard-grid\">");
            sb.AppendLine("            <!-- LEVÝ SLOUPEC: Specifikace a nápad -->");
            sb.AppendLine("            <div class=\"left-column\">");
            sb.AppendLine("                <div class=\"card filterable-section\">");
            sb.AppendLine("                    <div class=\"card-title\">Původní nápad</div>");
            sb.AppendLine("                    <div class=\"napad-quote\">");
            if (string.IsNullOrWhiteSpace(p.Idea))
                sb.AppendLine("                        (zatím nezadán – napiš nebo nadiktuj svůj nápad)");
            else
                foreach (var radek in p.Idea.Trim().Split('\n'))
                    sb.AppendLine($"                        {System.Net.WebUtility.HtmlEncode(radek.TrimEnd())}<br>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                </div>");
            sb.AppendLine();

            foreach (var sekce in GetAllSections(p))
            {
                var otazkySekce = GetProjectQuestions(p).Where(o => o.Section == sekce).ToList();
                var odpovezene = otazkySekce.Where(o => GetAnswerFor(p, o.Id) != null).ToList();
                if (odpovezene.Count == 0) continue;

                sb.AppendLine($"                <div class=\"card filterable-section\">");
                sb.AppendLine($"                    <div class=\"card-title\">{System.Net.WebUtility.HtmlEncode(sekce)}</div>");
                foreach (var ot in odpovezene)
                {
                    var odp = GetAnswerFor(p, ot.Id);
                    string predpokladBadge = odp.IsAssumption ? "<span class=\"badge badge-predpoklad\">Předpoklad</span>" : "";
                    sb.AppendLine("                    <div class=\"spec-item\">");
                    sb.AppendLine($"                        <div class=\"spec-question\">{System.Net.WebUtility.HtmlEncode(ot.GetText(p.ProjectTypeKey))}{predpokladBadge}</div>");
                    sb.AppendLine($"                        <div class=\"spec-answer\">{System.Net.WebUtility.HtmlEncode(odp.Text)}</div>");
                    sb.AppendLine("                    </div>");
                }
                sb.AppendLine("                </div>");
                sb.AppendLine();
            }

            sb.AppendLine("            </div>");
            sb.AppendLine();
            sb.AppendLine("            <!-- PRAVÝ SLOUPEC: Metriky a Backlog -->");
            sb.AppendLine("            <div class=\"right-column\">");

            // Metriky
            if (p.Metrics != null && p.Metrics.CalculationTimestamp != default)
            {
                string komplexitaClass = p.Metrics.Complexity.Contains("Vysoká") ? "prio-high" : (p.Metrics.Complexity.Contains("Střední") ? "prio-med" : "prio-low");
                sb.AppendLine("                <div class=\"card filterable-section\">");
                sb.AppendLine("                    <div class=\"card-title\">Projektové metriky</div>");
                if (AreMetricsOutdated(p))
                    sb.AppendLine($"                    <div class=\"stale-note\" style=\"background-color: rgba(255,193,7,0.15); border: 1px solid var(--prio-med); border-radius: 8px; padding: 8px 12px; margin-bottom: 12px; font-size: 0.85rem;\">{OutdatedMetricsNote}</div>");
                sb.AppendLine("                    <div class=\"metric-cards-container\">");
                sb.AppendLine("                        <div class=\"metric-mini-card\">");
                sb.AppendLine("                            <div class=\"metric-mini-label\">Vývoj (odhad)</div>");
                sb.AppendLine($"                            <div class=\"metric-mini-value\">{System.Net.WebUtility.HtmlEncode(p.Metrics.TimeEstimateMin)} - {System.Net.WebUtility.HtmlEncode(p.Metrics.TimeEstimateMax)}</div>");
                sb.AppendLine("                        </div>");
                sb.AppendLine("                        <div class=\"metric-mini-card\">");
                sb.AppendLine("                            <div class=\"metric-mini-label\">Složitost</div>");
                sb.AppendLine($"                            <div class=\"metric-mini-value\"><span class=\"badge badge-prio badge-{komplexitaClass}\" style=\"margin-left:0;\">{System.Net.WebUtility.HtmlEncode(p.Metrics.Complexity)}</span></div>");
                sb.AppendLine("                        </div>");
                sb.AppendLine("                        <div class=\"metric-mini-card\">");
                sb.AppendLine("                            <div class=\"metric-mini-label\">Rozpočet</div>");
                sb.AppendLine($"                            <div class=\"metric-mini-value\">{System.Net.WebUtility.HtmlEncode(p.Metrics.RecommendedBudget)}</div>");
                sb.AppendLine("                        </div>");
                sb.AppendLine("                        <div class=\"metric-mini-card\">");
                sb.AppendLine("                            <div class=\"metric-mini-label\">Doporučený tým</div>");
                sb.AppendLine($"                            <div class=\"metric-mini-value\">{System.Net.WebUtility.HtmlEncode(p.Metrics.TeamComposition)}</div>");
                sb.AppendLine("                        </div>");
                sb.AppendLine("                    </div>");
                sb.AppendLine("                    <div style=\"font-size:0.85rem; line-height:1.4; border-top:1px solid var(--border); padding-top:12px;\">");
                sb.AppendLine("                        <strong>Architektura a technologie:</strong><br>");
                foreach (var radek in p.Metrics.TechnicalAnalysis.Split('\n'))
                {
                    if (string.IsNullOrWhiteSpace(radek)) continue;
                    sb.AppendLine($"                        {System.Net.WebUtility.HtmlEncode(radek)}<br>");
                }
                sb.AppendLine("                    </div>");
                sb.AppendLine("                </div>");
                sb.AppendLine();
            }

            // Otevřené otázky
            var otevrene = GetOpenQuestions(p);
            if (otevrene.Count > 0)
            {
                sb.AppendLine("                <div class=\"card filterable-section\">");
                sb.AppendLine($"                    <div class=\"card-title\">Otevřené otázky <span class=\"badge badge-predpoklad\" style=\"margin-left:8px;\">{otevrene.Count}</span></div>");
                sb.AppendLine("                    <ul style=\"padding-left:20px; font-size:0.9rem;\">");
                foreach (var ot in otevrene)
                {
                    string dopadText = ot.Impact == Impact.High ? "vysoký dopad" : "střední dopad";
                    sb.AppendLine($"                        <li style=\"margin-bottom:6px;\"><strong>{System.Net.WebUtility.HtmlEncode(ot.GetText(p.ProjectTypeKey))}</strong> <span style=\"color:var(--text-light); font-size:0.8rem;\">({dopadText})</span></li>");
                }
                sb.AppendLine("                    </ul>");
                sb.AppendLine("                </div>");
                sb.AppendLine();
            }

            // Kontrola konzistence
            var nalezy = ConsistencyChecker.Check(p);
            if (nalezy.Count > 0)
            {
                sb.AppendLine("                <div class=\"card filterable-section\">");
                sb.AppendLine("                    <div class=\"card-title\">Kontrola konzistence</div>");
                sb.AppendLine("                    <div>");
                foreach (var n in nalezy)
                {
                    string fClass = n.Severity == Severity.Conflict ? "finding-conflict" : "finding-warning";
                    string pfx = n.Severity == Severity.Conflict ? "❗ <strong>ROZPOR:</strong> " : "⚠️ <strong>Varování:</strong> ";
                    sb.AppendLine($"                        <div class=\"{fClass}\">");
                    sb.AppendLine($"                            {pfx}<strong>{System.Net.WebUtility.HtmlEncode(n.Title)}</strong> – {System.Net.WebUtility.HtmlEncode(n.Detail)}");
                    sb.AppendLine("                        </div>");
                }
                sb.AppendLine("                    </div>");
                sb.AppendLine("                </div>");
                sb.AppendLine();
            }

            // Agilní Backlog (User Stories)
            if (p.UserStories != null && p.UserStories.Count > 0)
            {
                sb.AppendLine("                <div class=\"card filterable-section\">");
                sb.AppendLine("                    <div class=\"card-title\">Agilní backlog</div>");
                if (AreStoriesOutdated(p))
                    sb.AppendLine($"                    <div class=\"stale-note\" style=\"background-color: rgba(255,193,7,0.15); border: 1px solid var(--prio-med); border-radius: 8px; padding: 8px 12px; margin-bottom: 12px; font-size: 0.85rem;\">{OutdatedStoriesNote}</div>");
                foreach (var us in p.UserStories)
                {
                    string safeId = new string((us.Id ?? "").Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_').ToArray());
                    string prioClass = us.Priority == "Vysoká" ? "prio-high" : (us.Priority == "Střední" ? "prio-med" : "prio-low");
                    sb.AppendLine($"                    <div class=\"backlog-item\" id=\"story-{safeId}\">");
                    sb.AppendLine($"                        <input type=\"checkbox\" class=\"backlog-checkbox\" onchange=\"toggleStory(this, 'story-{safeId}')\">");
                    sb.AppendLine("                        <div class=\"backlog-text\">");
                    sb.AppendLine($"                            <div class=\"backlog-title\">{System.Net.WebUtility.HtmlEncode(us.Id)}: {System.Net.WebUtility.HtmlEncode(us.Title)} <span class=\"badge badge-prio badge-{prioClass}\">{System.Net.WebUtility.HtmlEncode(us.Priority)}</span></div>");
                    sb.AppendLine($"                            <div class=\"backlog-desc\">{System.Net.WebUtility.HtmlEncode(us.Description)}</div>");
                    sb.AppendLine("                            <ul class=\"backlog-criteria-list\">");
                    foreach (var crit in us.Criteria)
                    {
                        sb.AppendLine($"                                <li class=\"backlog-criteria-item\">{System.Net.WebUtility.HtmlEncode(crit)}</li>");
                    }
                    sb.AppendLine("                            </ul>");
                    sb.AppendLine("                        </div>");
                    sb.AppendLine("                    </div>");
                }
                sb.AppendLine("                </div>");
                sb.AppendLine();
            }

            // Záznam rozhodnutí (Log)
            if (p.ChangeLog != null && p.ChangeLog.Count > 0)
            {
                sb.AppendLine("                <div class=\"card filterable-section\">");
                sb.AppendLine("                    <div class=\"card-title\">Záznam rozhodnutí (Log)</div>");
                sb.AppendLine("                    <div style=\"font-size:0.85rem; max-height:250px; overflow-y:auto;\">");
                foreach (var log in p.ChangeLog)
                {
                    sb.AppendLine("                        <div style=\"margin-bottom:8px; border-bottom:1px solid var(--border); padding-bottom:6px;\">");
                    sb.AppendLine($"                            <span style=\"color:var(--text-light); font-weight:500;\">{log.Timestamp:d. M. yyyy v H:mm}</span> - <strong>{System.Net.WebUtility.HtmlEncode(log.Action)}</strong><br>");
                    sb.AppendLine($"                            <span style=\"color:var(--text);\">{System.Net.WebUtility.HtmlEncode(log.Detail)}</span>");
                    sb.AppendLine("                        </div>");
                }
                sb.AppendLine("                    </div>");
                sb.AppendLine("                </div>");
                sb.AppendLine();
            }

            sb.AppendLine("            </div>");
            sb.AppendLine("        </div>");
            sb.AppendLine("    </div>");
            sb.AppendLine();
            sb.AppendLine("    <script>");
            sb.AppendLine("        document.addEventListener('DOMContentLoaded', () => {");
            sb.AppendLine("            const theme = document.body.getAttribute('data-theme');");
            sb.AppendLine("            document.getElementById('theme-icon').innerText = theme === 'dark' ? '☀' : '🌙';");
            sb.AppendLine("            document.getElementById('theme-label').innerText = theme === 'dark' ? 'Světlý režim' : 'Tmavý režim';");
            sb.AppendLine("        });");
            sb.AppendLine();
            sb.AppendLine("        function toggleTheme() {");
            sb.AppendLine("            const body = document.body;");
            sb.AppendLine("            const theme = body.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';");
            sb.AppendLine("            body.setAttribute('data-theme', theme);");
            sb.AppendLine("            localStorage.setItem('theme', theme);");
            sb.AppendLine("            document.getElementById('theme-icon').innerText = theme === 'dark' ? '☀' : '🌙';");
            sb.AppendLine("            document.getElementById('theme-label').innerText = theme === 'dark' ? 'Světlý režim' : 'Tmavý režim';");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        function toggleStory(checkbox, id) {");
            sb.AppendLine("            const element = document.getElementById(id);");
            sb.AppendLine("            if (checkbox.checked) {");
            sb.AppendLine("                element.classList.add('completed');");
            sb.AppendLine("            } else {");
            sb.AppendLine("                element.classList.remove('completed');");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        function filterContent() {");
            sb.AppendLine("            const query = document.getElementById('searchInput').value.toLowerCase();");
            sb.AppendLine("            const cards = document.querySelectorAll('.filterable-section');");
            sb.AppendLine("            cards.forEach(card => {");
            sb.AppendLine("                let cardVisible = false;");
            sb.AppendLine("                const specItems = card.querySelectorAll('.spec-item');");
            sb.AppendLine("                if (specItems.length > 0) {");
            sb.AppendLine("                    specItems.forEach(item => {");
            sb.AppendLine("                        const itemText = item.innerText.toLowerCase();");
            sb.AppendLine("                        if (itemText.includes(query)) {");
            sb.AppendLine("                            item.style.display = 'block';");
            sb.AppendLine("                            cardVisible = true;");
            sb.AppendLine("                        } else {");
            sb.AppendLine("                            item.style.display = 'none';");
            sb.AppendLine("                        }");
            sb.AppendLine("                    });");
            sb.AppendLine("                } else {");
            sb.AppendLine("                    const cardText = card.innerText.toLowerCase();");
            sb.AppendLine("                    if (cardText.includes(query)) {");
            sb.AppendLine("                        cardVisible = true;");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("                const backlogItems = card.querySelectorAll('.backlog-item');");
            sb.AppendLine("                if (backlogItems.length > 0) {");
            sb.AppendLine("                    backlogItems.forEach(item => {");
            sb.AppendLine("                        const itemText = item.innerText.toLowerCase();");
            sb.AppendLine("                        if (itemText.includes(query)) {");
            sb.AppendLine("                            item.style.display = 'flex';");
            sb.AppendLine("                            cardVisible = true;");
            sb.AppendLine("                        } else {");
            sb.AppendLine("                            item.style.display = 'none';");
            sb.AppendLine("                        }");
            sb.AppendLine("                    });");
            sb.AppendLine("                }");
            sb.AppendLine("                if (cardVisible || query === '') {");
            sb.AppendLine("                    card.style.display = 'block';");
            sb.AppendLine("                } else {");
            sb.AppendLine("                    card.style.display = 'none';");
            sb.AppendLine("                }");
            sb.AppendLine("            });");
            sb.AppendLine("        }");
            sb.AppendLine("    </script>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        // ---------- project storage ----------

        public static void SaveProject(ProjectSpecification p, string filepath)
        {
            string json = JsonSerializer.Serialize(p, JsonOpt);

            string folder = Path.GetDirectoryName(Path.GetFullPath(filepath));
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            string tmp = filepath + ".tmp";
            if (File.Exists(tmp)) File.Delete(tmp);

            File.WriteAllText(tmp, json, new UTF8Encoding(true));

            if (File.Exists(filepath))
            {
                File.Replace(tmp, filepath, filepath + ".bak");
            }
            else
            {
                File.Move(tmp, filepath);
            }
        }

        public static ProjectSpecification LoadProject(string filepath)
        {
            var text = File.ReadAllText(filepath);
            var node = JsonNode.Parse(text);
            if (node != null)
            {
                node = MigrateJson(node);
                var p = node.Deserialize<ProjectSpecification>(JsonOpt);
                if (p != null)
                {
                    if (p.Answers == null) p.Answers = new List<Answer>();
                    if (p.ChangeLog == null) p.ChangeLog = new List<DecisionLogEntry>();
                    if (p.Questions == null) p.Questions = new List<Question>();
                    if (p.UserStories == null) p.UserStories = new List<UserStory>();
                    if (p.ChatHistory == null) p.ChatHistory = new List<ChatMessage>();
                    if (p.Metrics == null) p.Metrics = new ProjectMetrics();
                    if (p.AiFindings == null) p.AiFindings = new List<AiFinding>();
                    return p;
                }
            }
            return new ProjectSpecification();
        }

        private static JsonNode MigrateJson(JsonNode node)
        {
            if (node is JsonObject obj)
            {
                var migrated = new JsonObject();
                foreach (var prop in obj)
                {
                    string oldKey = prop.Key;
                    string newKey = MapJsonKey(oldKey);
                    migrated[newKey] = prop.Value != null ? MigrateJson(prop.Value.DeepClone()) : null;
                }
                return migrated;
            }
            else if (node is JsonArray arr)
            {
                var migrated = new JsonArray();
                foreach (var item in arr)
                {
                    migrated.Add(item != null ? MigrateJson(item.DeepClone()) : null);
                }
                return migrated;
            }
            return node;
        }

        private static string MapJsonKey(string oldKey)
        {
            switch (oldKey)
            {
                // SpecProjekt / ProjectSpecification
                case "Nazev":
                case "nazev": return "Name";
                case "Napad":
                case "napad": return "Idea";
                case "TypProjektu":
                case "typProjektu": return "ProjectType";
                case "TypProjektuKlic":
                case "typProjektuKlic": return "ProjectTypeKey";
                case "ReferencniText":
                case "referencniText": return "ReferenceText";
                case "ReferencniNazev":
                case "referencniNazev": return "ReferenceName";
                case "MockupBase64":
                case "mockupBase64": return "MockupBase64";
                case "MockupNazev":
                case "mockupNazev": return "MockupName";
                case "Vytvoreno":
                case "vytvoreno": return "CreatedAt";
                case "Upraveno":
                case "upraveno": return "UpdatedAt";
                case "Verze":
                case "verze": return "Version";
                case "Odpovedi":
                case "odpovedi": return "Answers";
                case "Log":
                case "log": return "ChangeLog";
                case "Otazky":
                case "otazky": return "Questions";
                case "UserStories":
                case "userStories": return "UserStories";
                case "ChatHistory":
                case "chatHistory": return "ChatHistory";
                case "Metriky":
                case "metriky": return "Metrics";
                case "CasGenerovaniStories":
                case "casGenerovaniStories": return "StoriesGenerationTimestamp";
                case "AiNalezy":
                case "aiNalezy": return "AiFindings";
                case "CasAiKontroly":
                case "casAiKontroly": return "AiCheckTimestamp";

                // Odpoved / Answer
                case "OtazkaId":
                case "otazkaId": return "QuestionId";
                case "JePredpoklad":
                case "jePredpoklad": return "IsAssumption";
                case "Cas":
                case "cas": return "Timestamp";

                // Rozhodnuti / DecisionLogEntry
                case "Akce":
                case "akce": return "Action";
                case "Detail":
                case "detail": return "Detail";

                // UserStory
                case "Id":
                case "id": return "Id";
                case "Titulek":
                case "titulek": return "Title";
                case "Popis":
                case "popis": return "Description";
                case "Kriteria":
                case "kriteria": return "Criteria";
                case "Priorita":
                case "priorita": return "Priority";

                // ProjektMetriky / ProjectMetrics
                case "CasovyOdhadMin":
                case "casovyOdhadMin": return "TimeEstimateMin";
                case "CasovyOdhadMax":
                case "casovyOdhadMax": return "TimeEstimateMax";
                case "Komplexita":
                case "komplexita": return "Complexity";
                case "SlozeniTymu":
                case "slozeniTymu": return "TeamComposition";
                case "DoporucenyRozpocet":
                case "doporucenyRozpocet": return "RecommendedBudget";
                case "TechnickyRozbor":
                case "technickyRozbor": return "TechnicalAnalysis";
                case "RizikaMetriky":
                case "rizikaMetriky": return "MetricRisks";
                case "CasVypoctu":
                case "casVypoctu": return "CalculationTimestamp";

                // AiNalez / AiFinding
                case "Zavaznost":
                case "zavaznost": return "Severity";

                default: return oldKey;
            }
        }

        public static List<Question> GetProjectQuestions(ProjectSpecification p)
        {
            if (p.Questions != null && p.Questions.Count > 0)
            {
                return p.Questions;
            }
            return StandardQuestions.All.ToList();
        }

        public static Question GetQuestionById(ProjectSpecification p, string id)
            => GetProjectQuestions(p).FirstOrDefault(o => o.Id == id);
    }

    /// <summary>Severity of consistency warning.</summary>
    public enum Severity
    {
        Conflict,
        Warning
    }

    /// <summary>One consistency finding.</summary>
    public class ConsistencyFinding
    {
        public Severity Severity { get; set; }
        public string Title { get; set; } = "";
        public string Detail { get; set; } = "";
    }

    public static class ConsistencyChecker
    {
        public static List<ConsistencyFinding> Check(ProjectSpecification p)
        {
            var findings = new List<ConsistencyFinding>();
            CheckOfflineOnline(p, findings);
            CheckWebOffline(p, findings);
            CheckPersonalData(p, findings);
            CheckNonGoals(p, findings);
            CheckAcceptanceCriteria(p, findings);
            CheckDataExport(p, findings);
            CheckPlatform(p, findings);
            CheckAssumptions(p, findings);
            CheckMissingIdea(p, findings);
            CheckSqliteOnWeb(p, findings);
            CheckRolesWithoutAuth(p, findings);
            CheckBackupStrategy(p, findings);
            CheckApiDocumentation(p, findings);
            return findings;
        }

        private static string TextOdpovedi(ProjectSpecification p, string questionId)
        {
            var o = SpecificationService.GetAnswerFor(p, questionId);
            return o != null ? o.Text : "";
        }

        private static bool RikaOffline(ProjectSpecification p)
        {
            string s = Norm(TextOdpovedi(p, "tech-offline"));
            return s.Contains("plne offline") || s.Contains("lokalni uklada") || s.Contains("bez internetu");
        }

        private static List<string> Zdroje(ProjectSpecification p, params string[] excludeIds)
        {
            var list = new List<string>();
            foreach (var ot in SpecificationService.GetProjectQuestions(p))
            {
                if (excludeIds.Contains(ot.Id)) continue;
                var o = SpecificationService.GetAnswerFor(p, ot.Id);
                if (o != null && !o.IsAssumption && !string.IsNullOrWhiteSpace(o.Text))
                {
                    list.Add("otázka „" + ot.GetText(p.ProjectTypeKey) + "“: '" + o.Text + "'");
                }
            }
            if (!string.IsNullOrWhiteSpace(p.Idea))
            {
                list.Add("původní nápad: '" + p.Idea + "'");
            }
            return list;
        }

        private static string NajdiSlovo(List<string> zdroje, string[] hledana, out string zdroj)
        {
            foreach (var z in zdroje)
            {
                string nz = Norm(z);
                foreach (var h in hledana)
                {
                    if (nz.Contains(Norm(h)))
                    {
                        zdroj = z.Split(':')[0];
                        return h;
                    }
                }
            }
            zdroj = null;
            return null;
        }

        private static string Norm(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var sb = new StringBuilder();
            foreach (char c in text.ToLower())
            {
                switch (c)
                {
                    case 'á': sb.Append('a'); break;
                    case 'č': sb.Append('c'); break;
                    case 'ď': sb.Append('d'); break;
                    case 'é': sb.Append('e'); break;
                    case 'ě': sb.Append('e'); break;
                    case 'í': sb.Append('i'); break;
                    case 'ň': sb.Append('n'); break;
                    case 'ó': sb.Append('o'); break;
                    case 'ř': sb.Append('r'); break;
                    case 'š': sb.Append('s'); break;
                    case 'ť': sb.Append('t'); break;
                    case 'ú': sb.Append('u'); break;
                    case 'ů': sb.Append('u'); break;
                    case 'ý': sb.Append('y'); break;
                    case 'ž': sb.Append('z'); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        // ---------- rules ----------

        private static void CheckOfflineOnline(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            if (!RikaOffline(p)) return;
            var zdroje = Zdroje(p, "tech-offline");
            string zdroj;
            string slovo = NajdiSlovo(zdroje, new[] { "cloud", "synchronizac", "online" }, out zdroj);
            if (slovo != null)
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Conflict,
                    Title = "Offline vs. online",
                    Detail = "Technika říká „funguje offline“, ale " + zdroj + " zmiňuje „" + slovo + "“. Rozhodni, co platí."
                });
        }

        private static void CheckWebOffline(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            string plat = Norm(TextOdpovedi(p, "tech-platforma"));
            if (!RikaOffline(p) || (!plat.Contains("web") && !plat.Contains("prohlizec"))) return;
            findings.Add(new ConsistencyFinding
            {
                Severity = Severity.Warning,
                Title = "Web + plně offline",
                Detail = "Webová aplikace bez internetu funguje jen jako PWA s offline režimem – ověř, jestli to tak myslíš."
            });
        }

        private static void CheckPersonalData(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            string data = Norm(TextOdpovedi(p, "data-obsah"));
            bool bezOsobnich = data.Contains("bez osobnich") || data.Contains("neosobni") || data.Contains("zadne osobni");
            if (!bezOsobnich) return;

            var zdroje = Zdroje(p, "data-obsah");
            string zdroj;
            string slovo = NajdiSlovo(zdroje, new[] { "jmeno", "jmena", "email", "e-mail", "telefon", "heslo", "registrac", "prihlas" }, out zdroj);
            if (slovo != null)
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Conflict,
                    Title = "Osobní údaje",
                    Detail = "Data říkají „bez osobních údajů“, ale " + zdroj + " zmiňuje „" + slovo + "“. Osobní údaje = GDPR a vyšší nároky."
                });
        }

        private static void CheckNonGoals(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            var odp = SpecificationService.GetAnswerFor(p, "rozsah-nongoals");
            if (odp == null || odp.IsAssumption) return;

            var stop = new HashSet<string> { "zadne", "nebude", "nebudou", "nechci", "nesmi", "prvni", "verze", "verzi",
                "aplikace", "appka", "zatim", "pozdeji", "budou", "chceme", "nechceme", "resit", "nema", "mit" };
            var zdroje = Zdroje(p, "rozsah-nongoals", "akceptace", "rizika");
            int hitu = 0;

            foreach (var fragment in odp.Text.Split(new[] { ',', ';', '\n', '•' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var slova = Norm(fragment)
                    .Split(new[] { ' ', '.', '!', '?', '(', ')', '"', '-', ':' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length >= 5 && !stop.Contains(w));

                foreach (var s in slova)
                {
                    string zdroj;
                    string slovo = NajdiSlovo(zdroje, new[] { s }, out zdroj);
                    if (slovo != null)
                    {
                        hitu++;
                        findings.Add(new ConsistencyFinding
                        {
                            Severity = Severity.Warning,
                            Title = "Non-goal popsán jako cíl",
                            Detail = "V non-goals vylučuješ „" + fragment.Trim() + "“, ale " + zdroj + " obsahuje zmínku o „" + slovo + "“."
                        });
                        if (hitu >= 3) return; // Max 3 varování
                    }
                }
            }
        }

        private static void CheckAcceptanceCriteria(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            string akc = Norm(TextOdpovedi(p, "akceptace"));
            if (string.IsNullOrEmpty(akc)) return;

            bool vagni = akc.Contains("podle specifikace") || akc.Contains("budou splneny") ||
                          akc.Contains("vsechny body") || akc.Contains("az bude hotovo");
            if (vagni)
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Warning,
                    Title = "Vágní akceptační kritéria",
                    Detail = "Kritéria říkají „podle specifikace / až to bude fungovat“. Zkus uvést měřitelné cíle (např. ruční průchod scénářem)."
                });
        }

        private static void CheckDataExport(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            string exp = Norm(TextOdpovedi(p, "data-export"));
            bool rikaBezExportu = exp.Contains("zadny export") || exp.Contains("netreba export") || exp.Contains("pouze v aplikaci") || exp.Contains("neni vyzadovan") || (exp.Contains("zadny") && exp.Contains("export"));
            if (!rikaBezExportu) return;

            var zdroje = Zdroje(p, "data-export");
            string zdroj;
            string slovo = NajdiSlovo(zdroje, new[] { "export", "stahnout", "stahovani", "zaloha", "zalohovani" }, out zdroj);
            if (slovo != null)
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Conflict,
                    Title = "Export dat vs. žádný export",
                    Detail = "V exportu odmítáš exporty, ale " + zdroj + " zmiňuje „" + slovo + "“. Rozhodni, jestli data půjde stáhnout."
                });
        }

        private static void CheckPlatform(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            string plat = Norm(TextOdpovedi(p, "tech-platforma"));
            if (string.IsNullOrEmpty(plat)) return;

            bool web = plat.Contains("web") || plat.Contains("prohlizec") || plat.Contains("browser");
            bool mob = plat.Contains("mobil") || plat.Contains("android") || plat.Contains("ios") || plat.Contains("telefon");
            bool dsk = plat.Contains("desktop") || plat.Contains("windows") || plat.Contains("macos") || plat.Contains("linux") || plat.Contains("pc");

            if (web && mob && dsk)
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Warning,
                    Title = "Příliš široký záběr platforem",
                    Detail = "Plánuješ web, mobil i desktop najednou. Pro MVP se doporučuje začít jedinou platformou (např. webem)."
                });
        }

        private static void CheckAssumptions(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            int pCount = SpecificationService.GetAssumptionsCount(p);
            if (pCount >= 3)
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Warning,
                    Title = "Vysoký počet předpokladů (" + pCount + ")",
                    Detail = "Máš nahrazeno " + pCount + " otázek výchozími předpoklady. Zkus na ně odpovědět pro přesnější zadání."
                });
        }

        private static void CheckMissingIdea(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            if (string.IsNullOrWhiteSpace(p.Idea) && SpecificationService.GetAnsweredCount(p) > 0)
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Warning,
                    Title = "Prázdný původní nápad",
                    Detail = "Zadání obsahuje odpovědi na otázky, ale chybí původní nápad. Doplň původní myšlenku v kroku 1."
                });
        }

        private static void CheckSqliteOnWeb(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            string plat = Norm(TextOdpovedi(p, "tech-platforma"));
            string data = Norm(TextOdpovedi(p, "data-obsah"));

            bool isWeb = plat.Contains("web") || plat.Contains("prohlizec") || plat.Contains("browser");
            bool isSqlite = data.Contains("sqlite");

            if (isWeb && isSqlite && !data.Contains("wasm") && !data.Contains("localstorage"))
            {
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Warning,
                    Title = "SQLite databáze na webu",
                    Detail = "Technologie uvádí webovou aplikaci a SQLite databázi. SQLite standardně neběží v prohlížeči. Zvaž LocalStorage/IndexedDB, nebo to uveď jako WASM/backend."
                });
            }
        }

        private static void CheckRolesWithoutAuth(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            string uziv = Norm(TextOdpovedi(p, "cil-uzivatele"));
            string tech = Norm(TextOdpovedi(p, "tech-platforma"));
            string data = Norm(TextOdpovedi(p, "data-obsah"));

            bool maRole = uziv.Contains("admin") || uziv.Contains("moderator") || uziv.Contains("opravneni") || uziv.Contains("role");
            bool maPrihlaseni = tech.Contains("prihlas") || tech.Contains("login") || tech.Contains("auth") || tech.Contains("heslo") || tech.Contains("ucet") || tech.Contains("registrac") ||
                                data.Contains("prihlas") || data.Contains("login") || data.Contains("auth") || data.Contains("heslo") || data.Contains("ucet") || data.Contains("registrac");

            if (maRole && !maPrihlaseni)
            {
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Warning,
                    Title = "Uživatelské role bez přihlašování",
                    Detail = "Zmiňuješ uživatelské role (např. administrátor, oprávnění), ale v technologii ani datech se neřeší autentizace. Jak se role rozpoznají?"
                });
            }
        }

        private static void CheckBackupStrategy(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            string data = Norm(TextOdpovedi(p, "data-obsah"));
            string rizika = Norm(TextOdpovedi(p, "rizika-reseni"));

            bool utilizesDatabase = data.Contains("databaze") || data.Contains("db") || data.Contains("postgresql") ||
                                    data.Contains("mysql") || data.Contains("mssql") || data.Contains("mongodb") ||
                                    data.Contains("oracle") || data.Contains("sql") || data.Contains("sqlite");

            bool mentionsBackup = rizika.Contains("zaloha") || rizika.Contains("zaloh") || rizika.Contains("backup") ||
                                  rizika.Contains("dump") || rizika.Contains("sync") ||
                                  data.Contains("zaloha") || data.Contains("zaloh") || data.Contains("backup");

            if (utilizesDatabase && !mentionsBackup)
            {
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Warning,
                    Title = "Chybějící strategie zálohování",
                    Detail = "Aplikace využívá databázi, ale v krizovém plánu chybí zmínka o zálohování (záloha, backup, dump). Zvažte doplnění strategie záloh."
                });
            }
        }

        private static void CheckApiDocumentation(ProjectSpecification p, List<ConsistencyFinding> findings)
        {
            string tech = Norm(TextOdpovedi(p, "tech-platforma"));
            string data = Norm(TextOdpovedi(p, "data-obsah"));

            bool mentionsApi = tech.Contains("api") || tech.Contains("rest") || tech.Contains("graphql") ||
                              tech.Contains("soap") || tech.Contains("webhook") || tech.Contains("integrace") ||
                              tech.Contains("tretich stran") || tech.Contains("tretichstran") || tech.Contains("napojeni") ||
                              data.Contains("api") || data.Contains("rest") || data.Contains("integrace");

            bool hasReference = !string.IsNullOrWhiteSpace(p.ReferenceText);
            bool mentionsDocs = false;
            
            if (hasReference)
            {
                string refs = Norm(p.ReferenceText);
                mentionsDocs = refs.Contains("dokumentace") || refs.Contains("doc") || refs.Contains("swagger") ||
                               refs.Contains("openapi") || refs.Contains("schema") || refs.Contains("specification");
            }

            if (mentionsApi && !mentionsDocs)
            {
                findings.Add(new ConsistencyFinding
                {
                    Severity = Severity.Warning,
                    Title = "Chybějící dokumentace k externímu API",
                    Detail = "Projekt zmiňuje integraci externího API nebo služeb třetích stran, ale chybí reference na jejich dokumentaci (dokumentace, swagger, OpenAPI)."
                });
            }
        }
    }
}
