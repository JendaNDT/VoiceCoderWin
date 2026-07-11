using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodePlanner.Core
{
    /// <summary>Dopad otázky na projekt – řídí pořadí dotazování (question planner dle návrhu, kap. 7).</summary>
    public enum Dopad
    {
        Vysoky,
        Stredni
    }

    public class SablonaProjektu
    {
        [JsonPropertyName("klic")]
        public string Klic { get; set; } = "";

        [JsonPropertyName("nazev")]
        public string Nazev { get; set; } = "";

        [JsonPropertyName("otazky")]
        public List<SablonaOtazka> Otazky { get; set; } = new List<SablonaOtazka>();
    }

    public class SablonaOtazka
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("text")]
        public string Text { get; set; } = "";

        [JsonPropertyName("napoveda")]
        public string Napoveda { get; set; } = "";

        [JsonPropertyName("vychoziPredpoklad")]
        public string VychoziPredpoklad { get; set; } = "";

        [JsonPropertyName("moznosti")]
        public List<string> Moznosti { get; set; } = new List<string>();
    }

    public static class SablonaSluzba
    {
        public static List<SablonaProjektu> CustomSablony { get; private set; } = new List<SablonaProjektu>();

        public static void NactiCustomSablony()
        {
            CustomSablony = new List<SablonaProjektu>();
            try
            {
                string cesta = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sablony.json");
                if (File.Exists(cesta))
                {
                    string json = File.ReadAllText(cesta);
                    var opt = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var data = JsonSerializer.Deserialize<Dictionary<string, List<SablonaProjektu>>>(json, opt);
                    if (data != null && data.TryGetValue("sablony", out var list))
                    {
                        CustomSablony = list;
                    }
                }
            }
            catch
            {
                // Tichý fallback
            }
        }
    }

    /// <summary>Typ projektu / Šablona otázek.</summary>
    public enum TypProjektu
    {
        Obecna,
        Hra,
        Evidence,
        Nastroj
    }

    /// <summary>Jedna řízená otázka. Otázky s vysokým dopadem jdou první.</summary>
    public class Otazka
    {
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
        public string Napoveda { get; set; } = "";
        public Dopad Dopad { get; set; }
        public string Sekce { get; set; } = "";
        public string VychoziPredpoklad { get; set; } = "";
        public List<string> Moznosti { get; set; } = new List<string>();

        // Slovníky pro šablonové texty
        [JsonIgnore]
        public Dictionary<TypProjektu, string> Texty { get; set; } = new Dictionary<TypProjektu, string>();
        [JsonIgnore]
        public Dictionary<TypProjektu, string> Napovedy { get; set; } = new Dictionary<TypProjektu, string>();
        [JsonIgnore]
        public Dictionary<TypProjektu, string> Predpoklady { get; set; } = new Dictionary<TypProjektu, string>();

        public List<string> GetMoznosti(string typKlic)
        {
            var sablona = SablonaSluzba.CustomSablony.FirstOrDefault(s => string.Equals(s.Klic, typKlic, StringComparison.OrdinalIgnoreCase));
            if (sablona != null)
            {
                var ot = sablona.Otazky.FirstOrDefault(o => string.Equals(o.Id, Id, StringComparison.OrdinalIgnoreCase));
                if (ot != null && ot.Moznosti != null && ot.Moznosti.Count > 0) return ot.Moznosti;
            }

            if (Moznosti != null && Moznosti.Count > 0) return Moznosti;

            return VratVychoziMoznostiProId(Id);
        }

        private static List<string> VratVychoziMoznostiProId(string id)
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

        public string GetText(string typKlic)
        {
            if (Enum.TryParse<TypProjektu>(typKlic, true, out var enumTyp))
            {
                if (enumTyp == TypProjektu.Obecna) return Text;
                return Texty.TryGetValue(enumTyp, out var val) && !string.IsNullOrWhiteSpace(val) ? val : Text;
            }

            var sablona = SablonaSluzba.CustomSablony.FirstOrDefault(s => string.Equals(s.Klic, typKlic, StringComparison.OrdinalIgnoreCase));
            if (sablona != null)
            {
                var ot = sablona.Otazky.FirstOrDefault(o => string.Equals(o.Id, Id, StringComparison.OrdinalIgnoreCase));
                if (ot != null && !string.IsNullOrWhiteSpace(ot.Text)) return ot.Text;
            }

            return Text;
        }

        public string GetNapoveda(string typKlic)
        {
            if (Enum.TryParse<TypProjektu>(typKlic, true, out var enumTyp))
            {
                if (enumTyp == TypProjektu.Obecna) return Napoveda;
                return Napovedy.TryGetValue(enumTyp, out var val) && !string.IsNullOrWhiteSpace(val) ? val : Napoveda;
            }

            var sablona = SablonaSluzba.CustomSablony.FirstOrDefault(s => string.Equals(s.Klic, typKlic, StringComparison.OrdinalIgnoreCase));
            if (sablona != null)
            {
                var ot = sablona.Otazky.FirstOrDefault(o => string.Equals(o.Id, Id, StringComparison.OrdinalIgnoreCase));
                if (ot != null && !string.IsNullOrWhiteSpace(ot.Napoveda)) return ot.Napoveda;
            }

            return Napoveda;
        }

        public string GetVychoziPredpoklad(string typKlic)
        {
            if (Enum.TryParse<TypProjektu>(typKlic, true, out var enumTyp))
            {
                if (enumTyp == TypProjektu.Obecna) return VychoziPredpoklad;
                return Predpoklady.TryGetValue(enumTyp, out var val) && !string.IsNullOrWhiteSpace(val) ? val : VychoziPredpoklad;
            }

            var sablona = SablonaSluzba.CustomSablony.FirstOrDefault(s => string.Equals(s.Klic, typKlic, StringComparison.OrdinalIgnoreCase));
            if (sablona != null)
            {
                var ot = sablona.Otazky.FirstOrDefault(o => string.Equals(o.Id, Id, StringComparison.OrdinalIgnoreCase));
                if (ot != null && !string.IsNullOrWhiteSpace(ot.VychoziPredpoklad)) return ot.VychoziPredpoklad;
            }

            return VychoziPredpoklad;
        }

        public string GetText(TypProjektu typ) => GetText(typ.ToString());
        public string GetNapoveda(TypProjektu typ) => GetNapoveda(typ.ToString());
        public string GetVychoziPredpoklad(TypProjektu typ) => GetVychoziPredpoklad(typ.ToString());
    }

    /// <summary>Odpověď uživatele, nebo označený předpoklad (kap. 7: „Drobnosti může vyplnit označeným předpokladem.“).</summary>
    public class Odpoved
    {
        public string OtazkaId { get; set; } = "";
        public string Text { get; set; } = "";
        public bool JePredpoklad { get; set; }
        public DateTime Cas { get; set; }
    }

    /// <summary>Záznam v logu rozhodnutí – každá změna má čas a důvod (kap. 7).</summary>
    public class Rozhodnuti
    {
        public DateTime Cas { get; set; }
        public string Akce { get; set; } = "";
        public string Detail { get; set; } = "";
    }

    public class UserStory
    {
        public string Id { get; set; } = "";
        public string Titulek { get; set; } = "";
        public string Popis { get; set; } = "";
        public List<string> Kriteria { get; set; } = new List<string>();
        public string Priorita { get; set; } = "Střední";
    }

    public class ChatMessage
    {
        public string Role { get; set; } = "user"; // "user" / "model"
        public string Text { get; set; } = "";
        public DateTime Cas { get; set; } = DateTime.Now;
    }

    public class ProjektMetriky
    {
        public string CasovyOdhadMin { get; set; } = "";
        public string CasovyOdhadMax { get; set; } = "";
        public string Komplexita { get; set; } = "";
        public string SlozeniTymu { get; set; } = "";
        public string DoporucenyRozpocet { get; set; } = "";
        public string TechnickyRozbor { get; set; } = "";
        public List<string> RizikaMetriky { get; set; } = new List<string>();
        public DateTime CasVypoctu { get; set; }
    }

    /// <summary>Celý projekt = zdroj pravdy. Ukládá se jako .vcbrief (JSON).</summary>
    public class SpecProjekt
    {
        public string Nazev { get; set; } = "";
        public string Napad { get; set; } = "";
        public TypProjektu TypProjektu { get; set; } = TypProjektu.Obecna;

        private string _typProjektuKlic = null;
        public string TypProjektuKlic
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_typProjektuKlic))
                {
                    return TypProjektu.ToString();
                }
                return _typProjektuKlic;
            }
            set => _typProjektuKlic = value;
        }

        public string ReferencniText { get; set; } = null;
        public string ReferencniNazev { get; set; } = null;
        public string MockupBase64 { get; set; } = null;
        public string MockupNazev { get; set; } = null;
        public DateTime Vytvoreno { get; set; } = DateTime.Now;
        public DateTime Upraveno { get; set; } = DateTime.Now;
        public int Verze { get; set; } = 1;
        public List<Odpoved> Odpovedi { get; set; } = new List<Odpoved>();
        public List<Rozhodnuti> Log { get; set; } = new List<Rozhodnuti>();
        public List<Otazka> Otazky { get; set; } = new List<Otazka>();
        public List<UserStory> UserStories { get; set; } = new List<UserStory>();
        public List<ChatMessage> ChatHistory { get; set; } = new List<ChatMessage>();
        public ProjektMetriky Metriky { get; set; } = new ProjektMetriky();
    }

    /// <summary>Pevná sada řízených otázek – seřazená podle dopadu (nejdřív rozhodnutí, která mění architekturu, cenu nebo bezpečnost).</summary>
    public static class Otazky
    {
        public static readonly IReadOnlyList<Otazka> Vse = new List<Otazka>
        {
            new Otazka { Id = "cil-problem", Sekce = "Cíl a uživatelé", Dopad = Dopad.Vysoky,
                Text = "Jaký problém má aplikace vyřešit a jaký přínos od ní čekáš?",
                Napoveda = "Určuje smysl celé specifikace – všechno ostatní se od toho odvíjí.",
                VychoziPredpoklad = "Přínos je popsán jen v původním nápadu; bude upřesněn po první ukázce.",
                Texty = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Jaký zážitek má hra přinést a jaký je cíl hráče?" },
                    { TypProjektu.Evidence, "Jaké objekty se budou evidovat a jaký přínos má jejich přehled přinést?" },
                    { TypProjektu.Nastroj, "Jaký úkol nebo operaci má nástroj automatizovat a co je cílem?" }
                },
                Napovedy = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "U her je hlavním přínosem zábava, hratelnost, odreagování nebo skóre." },
                    { TypProjektu.Evidence, "U evidencí jde o přehlednost, rychlé vyhledávání a spolehlivé ukládání." },
                    { TypProjektu.Nastroj, "U nástrojů jde o úsporu času a eliminaci lidských chyb při rutinní práci." }
                },
                Predpoklady = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Hra pro zábavu, cíl hráče je dosáhnout co nejvyššího skóre." },
                    { TypProjektu.Evidence, "Evidence a přehledné ukládání specifických záznamů s možností filtrování." },
                    { TypProjektu.Nastroj, "Jednoúčelový nástroj pro automatizaci specifického úkonu a úsporu času." }
                }
            },

            new Otazka { Id = "cil-uzivatele", Sekce = "Cíl a uživatelé", Dopad = Dopad.Vysoky,
                Text = "Kdo bude aplikaci používat? (role, zkušenost s počítačem, kolik lidí)",
                Napoveda = "Jiné UX pro recepční, jiné pro vývojáře. Ovlivňuje složitost rozhraní.",
                VychoziPredpoklad = "Jediný uživatel – autor nápadu.",
                Texty = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Pro jaké hráče je hra určena? (věk, občasný hráč, hardcore, lokální multiplayer?)" },
                    { TypProjektu.Evidence, "Kdo bude data spravovat a kdo je bude jen prohlížet? (role, oprávnění)" },
                    { TypProjektu.Nastroj, "Kdo je typickým uživatelem nástroje a jaké má technické znalosti?" }
                },
                Napovedy = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Ovlivňuje obtížnost, ovládání a přítomnost hry pro více hráčů." },
                    { TypProjektu.Evidence, "Určuje, zda potřebujeme různé uživatelské účty a úroveň přístupu." },
                    { TypProjektu.Nastroj, "Určuje, zda stačí příkazová řádka (CLI) nebo je nutné jednoduché klikací rozhraní (GUI)." }
                },
                Predpoklady = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Jeden hráč na lokálním počítači, jednoduché ovládání." },
                    { TypProjektu.Evidence, "Jediný správce s plným přístupem k zápisu i čtení." },
                    { TypProjektu.Nastroj, "Technicky zdatný uživatel, stačí jednoduché a přímočaré rozhraní." }
                }
            },

            new Otazka { Id = "tech-platforma", Sekce = "Technika", Dopad = Dopad.Vysoky,
                Text = "Na čem má běžet první verze? (Windows program, web, mobil…)",
                Napoveda = "Mění architekturu i pracnost – proto je to jedna z prvních otázek.",
                VychoziPredpoklad = "Desktopová aplikace pro Windows.",
                Texty = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Na čem se bude hra hrát? (Windows .exe, web, mobil, gamepad?)" },
                    { TypProjektu.Evidence, "Na čem má evidence běžet? (web, lokální program pro Windows, intranet…)" },
                    { TypProjektu.Nastroj, "Na jaké platformě se bude nástroj spouštět? (CLI/konzole, desktop, web...)" }
                },
                Napovedy = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Určuje herní engine (Unity, Godot, MonoGame) a způsob ovládání." },
                    { TypProjektu.Evidence, "Web umožňuje přístup odkudkoliv, desktop je jednodušší na vývoj a offline." },
                    { TypProjektu.Nastroj, "CLI je ideální pro automatizaci a skripty, desktop pro interaktivní práci." }
                },
                Predpoklady = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Desktopová hra pro Windows, ovládání klávesnicí a myší." },
                    { TypProjektu.Evidence, "Lokální desktopová aplikace pro Windows." },
                    { TypProjektu.Nastroj, "Konzolová aplikace (CLI) nebo jednoduchý desktopový program pro Windows." }
                }
            },

            new Otazka { Id = "tech-offline", Sekce = "Technika", Dopad = Dopad.Vysoky,
                Text = "Musí fungovat bez internetu, nebo může počítat s připojením?",
                Napoveda = "Offline režim zásadně ovlivňuje ukládání dat i synchronizaci.",
                VychoziPredpoklad = "Plně offline, bez závislosti na připojení.",
                Texty = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Vyžaduje hra připojení k internetu? (multiplayer, online žebříčky?)" },
                    { TypProjektu.Evidence, "Budou data uložena lokálně u uživatele, nebo na sdíleném serveru v síti?" },
                    { TypProjektu.Nastroj, "Potřebuje nástroj pro svou práci internet, nebo běží plně lokálně?" }
                },
                Napovedy = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Offline hra je jednodušší. Online vyžaduje server a řešení síťové latence." },
                    { TypProjektu.Evidence, "Lokální DB je offline a bezpečná; sdílená DB vyžaduje připojení a řešení konfliktů." },
                    { TypProjektu.Nastroj, "Např. stahování z webu vs. lokální konverze souborů na disku." }
                },
                Predpoklady = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Plně offline singleplayer, bez online funkcí." },
                    { TypProjektu.Evidence, "Lokální databáze uložená na disku počítače." },
                    { TypProjektu.Nastroj, "Plně lokální běh bez nutnosti síťového připojení." }
                }
            },

            new Otazka { Id = "data-obsah", Sekce = "Data", Dopad = Dopad.Vysoky,
                Text = "Jaká data se budou ukládat? Budou mezi nimi osobní údaje?",
                Napoveda = "Osobní údaje = právní dopad (GDPR) a vyšší nároky na zabezpečení.",
                VychoziPredpoklad = "Jen neosobní provozní data; bez osobních údajů.",
                Texty = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Jaká herní data se ukládají? (pozice, skóre, statistiky, nastavení)" },
                    { TypProjektu.Evidence, "Jaké přesné údaje o položkách se budou ukládat? Budou tam osobní data?" },
                    { TypProjektu.Nastroj, "Jaká data/soubory nástroj načítá, zpracovává a co si musí trvale pamatovat?" }
                },
                Napovedy = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Určuje strukturu ukládání herního stavu (save files)." },
                    { TypProjektu.Evidence, "Definuje databázová pole (např. název, datum, cena, jméno klienta - GDPR)." },
                    { TypProjektu.Nastroj, "Většina nástrojů si pamatuje jen konfiguraci/historii, samotná data neukládá." }
                },
                Predpoklady = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Ukládání nejvyššího dosaženého skóre a lokálního nastavení hry." },
                    { TypProjektu.Evidence, "Základní datová pole bez citlivých nebo osobních údajů." },
                    { TypProjektu.Nastroj, "Pouze konfigurační soubor a historie posledních operací." }
                }
            },

            new Otazka { Id = "rozsah-nongoals", Sekce = "Rozsah", Dopad = Dopad.Vysoky,
                Text = "Co v první verzi záměrně NEMÁ být? (non-goals)",
                Napoveda = "Výslovné non-goals chrání projekt před nafukováním rozsahu.",
                VychoziPredpoklad = "Non-goals zatím neurčeny – riziko rozšiřování rozsahu.",
                Texty = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Co ve hře v první verzi záměrně NEBUDE? (non-goals, např. editor levelů, zvuk)" },
                    { TypProjektu.Evidence, "Jaké pokročilé funkce v první verzi NEBUDOU? (např. práva, tisk, historie)" },
                    { TypProjektu.Nastroj, "Co v první verzi nástroj NEBUDE umět? (např. dávkové zpracování, GUI)" }
                },
                Napovedy = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "U her je snadné sklouznout k vymýšlení dalších mechanik. Drž se jádra." },
                    { TypProjektu.Evidence, "Evidence se často komplikují reporty a synchronizací. Definuj, co počká." },
                    { TypProjektu.Nastroj, "Pomáhá omezit funkčnost na nejdůležitější hlavní úkol." }
                },
                Predpoklady = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Zatím bez hudby, bez online žebříčků a bez editoru map." },
                    { TypProjektu.Evidence, "Bez uživatelských rolí, bez automatických e-mailů a bez PDF reportů." },
                    { TypProjektu.Nastroj, "Bez podpory hromadného zpracování a bez grafického rozhraní (pokud je CLI)." }
                }
            },

            new Otazka { Id = "akceptace", Sekce = "Akceptace", Dopad = Dopad.Vysoky,
                Text = "Podle čeho poznáš, že je hotovo? Napiš 2–3 ověřitelné podmínky.",
                Napoveda = "Testovatelné podmínky určují hotový výsledek – bez nich nejde ověřit úspěch.",
                VychoziPredpoklad = "Akceptační kritéria budou doplněna po první ukázce.",
                Texty = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Kdy je hra hratelná a hotová? (např. dokončení kola, zobrazení Game Over)" },
                    { TypProjektu.Evidence, "Jaké scénáře manipulace s daty musí bezchybně fungovat? (přidat, najít, smazat)" },
                    { TypProjektu.Nastroj, "Jaké konkrétní vstupy musí nástroj úspěšně zpracovat a co vyprodukovat?" }
                },
                Napovedy = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Definuj minimální hratelnou verzi (core gameplay loop)." },
                    { TypProjektu.Evidence, "Základní operace (CRUD) – popiš úspěšný průchod." },
                    { TypProjektu.Nastroj, "Popiš testovací scénář (např. 'vezme soubor X a vytvoří správný soubor Y')." }
                },
                Predpoklady = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Hráč může hru spustit, ovládat postavu, dosáhnout cíle a vidět skóre." },
                    { TypProjektu.Evidence, "Lze přidat nový záznam, vyhledat ho podle názvu a smazat." },
                    { TypProjektu.Nastroj, "Nástroj správně zpracuje vzorový vstupní soubor a vypíše výsledek bez chyb." }
                }
            },

            new Otazka { Id = "ux-obrazovky", Sekce = "UX", Dopad = Dopad.Stredni,
                Text = "Jaké hlavní obrazovky nebo kroky uživatel projde?",
                Napoveda = "Stačí hrubý tah: kde uživatel začne, co udělá, kde skončí.",
                VychoziPredpoklad = "Jedna hlavní obrazovka; detaily vzejdou z první verze.",
                Texty = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Jaké stavy a obrazovky hra obsahuje? (Menu, Hrací plocha, Pause, Konec)" },
                    { TypProjektu.Evidence, "Jaké formuláře a pohledy uživatel uvidí? (tabulka, karta záznamu, nastavení)" },
                    { TypProjektu.Nastroj, "Jak vypadá interakce s nástrojem? (parametry v konzoli, jedno okno s tlačítkem)" }
                },
                Napovedy = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Určuje strukturu přepínání herních scén." },
                    { TypProjektu.Evidence, "Navrhni rozvržení prvků pro pohodlnou práci s daty." },
                    { TypProjektu.Nastroj, "Popiš, jak uživatel nástroj spouští a jak se dozví výsledek." }
                },
                Predpoklady = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Úvodní menu, herní obrazovka a obrazovka s výsledky." },
                    { TypProjektu.Evidence, "Hlavní okno s tabulkou záznamů a dialogové okno pro nový záznam." },
                    { TypProjektu.Nastroj, "Jednoduché okno s výběrem souboru a tlačítkem Spustit." }
                }
            },

            new Otazka { Id = "data-export", Sekce = "Data", Dopad = Dopad.Stredni,
                Text = "Potřebuješ export nebo import dat? V jakém formátu?",
                Napoveda = "Export (CSV, PDF…) bývá levný teď, drahý dodatečně.",
                VychoziPredpoklad = "Bez exportu a importu v první verzi.",
                Texty = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Bude možné skóre nebo pozice sdílet/exportovat? (snímky obrazovky, export skóre)" },
                    { TypProjektu.Evidence, "Je vyžadován export tabulky nebo import stávajících dat? (Excel, CSV, JSON)" },
                    { TypProjektu.Nastroj, "V jakém formátu nástroj odevzdává svůj výsledek? (nový soubor, konzolový výpis...)" }
                },
                Napovedy = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "U her většinou export netřeba, případně jen formou uložení do souboru." },
                    { TypProjektu.Evidence, "Užitečné pro migraci ze starých tabulek nebo posílání reportů." },
                    { TypProjektu.Nastroj, "Popiš výstupní formát (TXT, CSV, přepsání původního souboru...)." }
                },
                Predpoklady = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Bez exportu herních dat." },
                    { TypProjektu.Evidence, "Možnost exportu zobrazené tabulky do CSV souboru." },
                    { TypProjektu.Nastroj, "Uložení výsledného zpracovaného souboru ve stejném formátu." }
                }
            },

            new Otazka { Id = "rizika", Sekce = "Rizika", Dopad = Dopad.Stredni,
                Text = "Je něco nejasného nebo rizikového, co je potřeba ověřit?",
                Napoveda = "Nejasnosti pojmenované předem šetří opravy později.",
                VychoziPredpoklad = "Rizika zatím nezmapována.",
                Texty = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Jaká jsou herní rizika? (příliš těžká hratelnost, výkon vykreslování, fyzika)" },
                    { TypProjektu.Evidence, "Co může ohrozit integritu dat nebo stabilitu? (výpadek proudu, kolize klíčů)" },
                    { TypProjektu.Nastroj, "Na čem může nástroj selhat? (nečekaný formát vstupu, velké soubory)" }
                },
                Napovedy = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Pomáhá zaměřit se na prototypování zábavnosti (fun factor)." },
                    { TypProjektu.Evidence, "Zaměř se na bezpečnost dat a konkurentní přístup." },
                    { TypProjektu.Nastroj, "Určuje, jak robustní musí být parsování chyb a ošetření výjimek." }
                },
                Predpoklady = new Dictionary<TypProjektu, string>
                {
                    { TypProjektu.Hra, "Riziko, že herní mechanika nebude zábavná; vyžaduje včasné herní testy." },
                    { TypProjektu.Evidence, "Riziko ztráty dat při neočekávaném vypnutí; nutné bezpečné ukládání." },
                    { TypProjektu.Nastroj, "Riziko chybného formátu vstupních dat; nutné ošetřit chybová hlášení." }
                }
            },
        };

        public static Otazka Podle(string id)
        {
            foreach (var o in Vse) if (o.Id == id) return o;
            return null;
        }
    }

    public static class SpecSluzba
    {
        public static IEnumerable<Otazka> VratOtazkyProjektu(SpecProjekt p)
        {
            if (p != null && p.Otazky != null && p.Otazky.Count > 0)
            {
                return p.Otazky;
            }
            return Otazky.Vse;
        }

        public static Otazka Podle(SpecProjekt p, string id)
        {
            foreach (var o in VratOtazkyProjektu(p)) if (o.Id == id) return o;
            return null;
        }

        public static readonly string[] PoradiSekci = new[]
        {
            "Cíl a uživatelé", "Rozsah", "UX", "Data", "Technika", "Akceptace", "Rizika"
        };

        private static readonly JsonSerializerOptions JsonOpt = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            Converters = { new JsonStringEnumConverter() }
        };

        // ---------- změny stavu ----------

        public static string VratNazevTypu(TypProjektu typ) => VratNazevTypu(typ.ToString());

        public static string VratNazevTypu(string typKlic)
        {
            if (Enum.TryParse<TypProjektu>(typKlic, true, out var enumTyp))
            {
                return enumTyp switch
                {
                    TypProjektu.Hra => "Hra (Game)",
                    TypProjektu.Evidence => "Evidence / Registr",
                    TypProjektu.Nastroj => "Nástroj / Utilita",
                    _ => "Obecná aplikace"
                };
            }

            var sablona = SablonaSluzba.CustomSablony.FirstOrDefault(s => string.Equals(s.Klic, typKlic, StringComparison.OrdinalIgnoreCase));
            if (sablona != null) return sablona.Nazev;

            return typKlic;
        }

        public static void ZmenTypProjektu(SpecProjekt p, TypProjektu novyTyp) => ZmenTypProjektu(p, novyTyp.ToString());

        public static void ZmenTypProjektu(SpecProjekt p, string novyTypKlic)
        {
            if (p.TypProjektuKlic == novyTypKlic) return;
            var staryTypKlic = p.TypProjektuKlic;
            p.TypProjektuKlic = novyTypKlic;

            if (Enum.TryParse<TypProjektu>(novyTypKlic, true, out var enumTyp))
            {
                p.TypProjektu = enumTyp;
            }
            else
            {
                p.TypProjektu = TypProjektu.Obecna;
            }

            // Aktualizujeme všechny automatické předpoklady
            foreach (var ot in VratOtazkyProjektu(p))
            {
                var odp = OdpovedNa(p, ot.Id);
                if (odp != null && odp.JePredpoklad)
                {
                    odp.Text = ot.GetVychoziPredpoklad(novyTypKlic);
                    odp.Cas = DateTime.Now;
                }
            }

            Zmena(p, "Typ projektu", $"Změna typu projektu z {VratNazevTypu(staryTypKlic)} na {VratNazevTypu(novyTypKlic)}.");
        }

        public static void NastavNapad(SpecProjekt p, string napad)
        {
            if ((p.Napad ?? "") == (napad ?? "")) return;
            p.Napad = napad ?? "";
            Zmena(p, "Nápad", "Upraven text původního nápadu.");
        }

        public static void Odpovez(SpecProjekt p, string otazkaId, string text)
        {
            var ot = Podle(p, otazkaId);
            if (ot == null || string.IsNullOrWhiteSpace(text)) return;

            var stara = p.Odpovedi.FirstOrDefault(o => o.OtazkaId == otazkaId);
            if (stara != null) p.Odpovedi.Remove(stara);

            p.Odpovedi.Add(new Odpoved { OtazkaId = otazkaId, Text = text.Trim(), JePredpoklad = false, Cas = DateTime.Now });
            Zmena(p, stara == null ? "Odpověď" : "Změna odpovědi", ot.GetText(p.TypProjektuKlic) + " → " + Zkrat(text, 120));
        }

        public static void PouzijPredpoklad(SpecProjekt p, string otazkaId)
        {
            var ot = Podle(p, otazkaId);
            if (ot == null) return;

            var stara = p.Odpovedi.FirstOrDefault(o => o.OtazkaId == otazkaId);
            if (stara != null) p.Odpovedi.Remove(stara);

            p.Odpovedi.Add(new Odpoved { OtazkaId = otazkaId, Text = ot.GetVychoziPredpoklad(p.TypProjektuKlic), JePredpoklad = true, Cas = DateTime.Now });
            Zmena(p, "Předpoklad", ot.GetText(p.TypProjektuKlic) + " → [PŘEDPOKLAD] " + ot.GetVychoziPredpoklad(p.TypProjektuKlic));
        }

        private static void Zmena(SpecProjekt p, string akce, string detail)
        {
            p.Verze++;
            p.Upraveno = DateTime.Now;
            p.Log.Add(new Rozhodnuti { Cas = DateTime.Now, Akce = akce, Detail = detail });
        }

        // ---------- dotazy na stav ----------

        public static Odpoved OdpovedNa(SpecProjekt p, string otazkaId)
            => p.Odpovedi.FirstOrDefault(o => o.OtazkaId == otazkaId);

        public static Otazka DalsiNezodpovezena(SpecProjekt p)
            => VratOtazkyProjektu(p).FirstOrDefault(ot => OdpovedNa(p, ot.Id) == null);

        public static int PocetZodpovezenych(SpecProjekt p)
            => VratOtazkyProjektu(p).Count(ot => { var o = OdpovedNa(p, ot.Id); return o != null && !o.JePredpoklad; });

        public static int PocetPredpokladu(SpecProjekt p)
            => VratOtazkyProjektu(p).Count(ot => { var o = OdpovedNa(p, ot.Id); return o != null && o.JePredpoklad; });

        public static List<Otazka> OtevreneOtazky(SpecProjekt p)
            => VratOtazkyProjektu(p).Where(ot => OdpovedNa(p, ot.Id) == null).ToList();

        // ---------- rendering ----------

        private static string Datum(DateTime d) => d.ToString("d. M. yyyy H:mm");

        private static string Zkrat(string s, int max)
        {
            s = (s ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return s.Length <= max ? s : s.Substring(0, max - 1) + "…";
        }

        /// <summary>Čitelný dokument pro člověka (kap. 7: „Jedna kanonická struktura se renderuje dvěma způsoby…“).</summary>
        public static string RenderMarkdown(SpecProjekt p)
        {
            var sb = new StringBuilder();
            string nazev = string.IsNullOrWhiteSpace(p.Nazev) ? "(nepojmenovaný projekt)" : p.Nazev.Trim();

            sb.AppendLine("# Specifikace: " + nazev);
            sb.AppendLine("*Typ projektu: " + VratNazevTypu(p.TypProjektuKlic) + "*");
            sb.AppendLine("*Verze specifikace " + p.Verze + " · aktualizováno " + Datum(p.Upraveno) + "*");
            sb.AppendLine("*Vytvořeno nástrojem CodePlanner (demonstrátor bez AI)*");
            sb.AppendLine();

            sb.AppendLine("## Původní nápad");
            if (string.IsNullOrWhiteSpace(p.Napad))
                sb.AppendLine("> (zatím nezadán – napiš nebo nadiktuj svůj nápad)");
            else
                foreach (var radek in p.Napad.Trim().Split('\n'))
                    sb.AppendLine("> " + radek.TrimEnd());
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(p.ReferencniText))
            {
                sb.AppendLine("## Referenční podklady (" + (p.ReferencniNazev ?? "příloha") + ")");
                sb.AppendLine("```text");
                sb.AppendLine(p.ReferencniText.Trim());
                sb.AppendLine("```");
                sb.AppendLine();
            }

            if (!string.IsNullOrWhiteSpace(p.MockupNazev))
            {
                sb.AppendLine("## Vizuální nákres rozhraní (Mockup)");
                sb.AppendLine("- Přiložen vizuální návrh: **" + p.MockupNazev + "** (obrázek odesílán jako vizuální kontext do Gemini)");
                sb.AppendLine();
            }

            foreach (var sekce in PoradiSekci)
            {
                sb.AppendLine("## " + sekce);
                var otazkySekce = VratOtazkyProjektu(p).Where(o => o.Sekce == sekce).ToList();
                bool neco = false;

                foreach (var ot in otazkySekce)
                {
                    var odp = OdpovedNa(p, ot.Id);
                    if (odp == null) continue;
                    neco = true;
                    string znacka = odp.JePredpoklad ? " **[PŘEDPOKLAD]**" : "";
                    sb.AppendLine("- **" + ot.GetText(p.TypProjektuKlic) + "**" + znacka);
                    foreach (var radek in odp.Text.Trim().Split('\n'))
                        sb.AppendLine("  " + radek.TrimEnd());
                }

                if (!neco) sb.AppendLine("- *(zatím bez rozhodnutí)*");
                sb.AppendLine();
            }

            var otevrene = OtevreneOtazky(p);
            sb.AppendLine("## Otevřené otázky");
            if (otevrene.Count == 0)
                sb.AppendLine("- *(žádné – všechny otázky jsou vyřešené)*");
            else
                foreach (var ot in otevrene)
                    sb.AppendLine("- [" + (ot.Dopad == Dopad.Vysoky ? "vysoký dopad" : "střední dopad") + "] " + ot.GetText(p.TypProjektuKlic));
            sb.AppendLine();

            var nalezy = KonzistencniKontrola.Zkontroluj(p);
            if (nalezy.Count > 0)
            {
                sb.AppendLine("## Kontrola konzistence");
                foreach (var n in nalezy)
                    sb.AppendLine("- " + (n.Zavaznost == Zavaznost.Rozpor ? "❗ **ROZPOR: " : "⚠️ **Varování: ") + n.Titulek + "** – " + n.Detail);
                sb.AppendLine();
            }

            sb.AppendLine("## Souhrn stavu");
            sb.AppendLine("- Zodpovězeno: " + PocetZodpovezenych(p) + " / " + Otazky.Vse.Count);
            sb.AppendLine("- Označené předpoklady: " + PocetPredpokladu(p));
            sb.AppendLine("- Otevřené otázky: " + otevrene.Count);
            sb.AppendLine();

            sb.AppendLine("## Log rozhodnutí");
            if (p.Log.Count == 0)
                sb.AppendLine("- *(zatím žádná rozhodnutí)*");
            else
                foreach (var r in p.Log)
                    sb.AppendLine("- " + Datum(r.Cas) + " · **" + r.Akce + "** · " + r.Detail);

            return sb.ToString();
        }

        /// <summary>Stabilní strojová struktura pro orchestrátor/agenta (kap. 7).</summary>
        public static string RenderJson(SpecProjekt p)
        {
            var sekce = new List<object>();
            foreach (var nazevSekce in PoradiSekci)
            {
                var polozky = new List<object>();
                foreach (var ot in Otazky.Vse.Where(o => o.Sekce == nazevSekce))
                {
                    var odp = OdpovedNa(p, ot.Id);
                    if (odp == null) continue;
                    polozky.Add(new { id = ot.Id, otazka = ot.GetText(p.TypProjektu), odpoved = odp.Text, predpoklad = odp.JePredpoklad });
                }
                sekce.Add(new { nazev = nazevSekce, polozky });
            }

            var data = new
            {
                nastroj = "CodePlanner",
                verzeNastroje = "0.9.0",
                projekt = p.Nazev,
                typProjektu = p.TypProjektuKlic,
                typProjektuNazev = VratNazevTypu(p.TypProjektuKlic),
                verzeSpecifikace = p.Verze,
                vytvoreno = p.Vytvoreno,
                upraveno = p.Upraveno,
                napad = p.Napad,
                referencniText = p.ReferencniText,
                referencniNazev = p.ReferencniNazev,
                sekce,
                otevreneOtazky = OtevreneOtazky(p).Select(o => new { id = o.Id, otazka = o.GetText(p.TypProjektuKlic) }).ToList(),
                kontrolaKonzistence = KonzistencniKontrola.Zkontroluj(p)
                    .Select(n => new { zavaznost = n.Zavaznost.ToString(), titulek = n.Titulek, detail = n.Detail }).ToList(),
                logRozhodnuti = p.Log.Select(r => new { cas = r.Cas, akce = r.Akce, detail = r.Detail }).ToList()
            };

            return JsonSerializer.Serialize(data, JsonOpt);
        }

        public static string RenderHtml(SpecProjekt p)
        {
            var sb = new StringBuilder();
            string nazev = string.IsNullOrWhiteSpace(p.Nazev) ? "(nepojmenovaný projekt)" : p.Nazev.Trim();
            
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"cs\">");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine($"    <title>Specifikace: {System.Net.WebUtility.HtmlEncode(nazev)}</title>");
            sb.AppendLine("    <link href=\"https://fonts.googleapis.com/css2?family=Inter:wght@300;400;500;600;700&display=swap\" rel=\"stylesheet\">");
            sb.AppendLine("    <style>");
            sb.AppendLine("        :root {");
            sb.AppendLine("            --primary: #10233F;");
            sb.AppendLine("            --accent: #149689;");
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
            sb.AppendLine("        .napad-quote { border-left: 4px solid var(--accent); padding: 8px 16px; background-color: rgba(20, 150, 137, 0.05); font-style: italic; border-radius: 0 8px 8px 0; margin-bottom: 12px; }");
            sb.AppendLine("        .spec-item { margin-bottom: 16px; border-bottom: 1px solid var(--border); padding-bottom: 12px; }");
            sb.AppendLine("        .spec-item:last-child { border-bottom: none; padding-bottom: 0; margin-bottom: 0; }");
            sb.AppendLine("        .spec-question { font-weight: 600; font-size: 0.95rem; margin-bottom: 4px; }");
            sb.AppendLine("        .spec-answer { font-size: 0.95rem; }");
            sb.AppendLine("        .badge { display: inline-block; padding: 2px 8px; font-size: 0.75rem; font-weight: 600; border-radius: 12px; margin-left: 8px; }");
            sb.AppendLine("        .badge-prio { color: white; }");
            sb.AppendLine("        .badge-prio-high { background-color: var(--prio-high); }");
            sb.AppendLine("        .badge-prio-med { background-color: var(--prio-med); color: #000; }");
            sb.AppendLine("        .badge-prio-low { background-color: var(--prio-low); }");
            sb.AppendLine("        .badge-předpoklad { background-color: #e2e8f0; color: #475569; }");
            sb.AppendLine("        [data-theme=\"dark\"] .badge-předpoklad { background-color: #334155; color: #cbd5e1; }");
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
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("    <div class=\"container\">");
            sb.AppendLine("        <header>");
            sb.AppendLine($"            <h1>Specifikace: {System.Net.WebUtility.HtmlEncode(nazev)}</h1>");
            sb.AppendLine("            <div class=\"meta-subtitle\">");
            sb.AppendLine($"                <span>Typ projektu: <strong>{System.Net.WebUtility.HtmlEncode(VratNazevTypu(p.TypProjektuKlic))}</strong></span>");
            sb.AppendLine($"                <span>Verze: <strong>{p.Verze}</strong></span>");
            sb.AppendLine($"                <span>Aktualizováno: <strong>{Datum(p.Upraveno)}</strong></span>");
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
            if (string.IsNullOrWhiteSpace(p.Napad))
                sb.AppendLine("                        (zatím nezadán – napiš nebo nadiktuj svůj nápad)");
            else
                foreach (var radek in p.Napad.Trim().Split('\n'))
                    sb.AppendLine($"                        {System.Net.WebUtility.HtmlEncode(radek.TrimEnd())}<br>");
            sb.AppendLine("                    </div>");
            sb.AppendLine("                </div>");
            sb.AppendLine();

            foreach (var sekce in PoradiSekci)
            {
                var otazkySekce = VratOtazkyProjektu(p).Where(o => o.Sekce == sekce).ToList();
                var odpovezene = otazkySekce.Where(o => OdpovedNa(p, o.Id) != null).ToList();
                if (odpovezene.Count == 0) continue;

                sb.AppendLine($"                <div class=\"card filterable-section\">");
                sb.AppendLine($"                    <div class=\"card-title\">{System.Net.WebUtility.HtmlEncode(sekce)}</div>");
                foreach (var ot in odpovezene)
                {
                    var odp = OdpovedNa(p, ot.Id);
                    string predpokladBadge = odp.JePredpoklad ? "<span class=\"badge badge-předpoklad\">Předpoklad</span>" : "";
                    sb.AppendLine("                    <div class=\"spec-item\">");
                    sb.AppendLine($"                        <div class=\"spec-question\">{System.Net.WebUtility.HtmlEncode(ot.GetText(p.TypProjektuKlic))}{predpokladBadge}</div>");
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
            if (p.Metriky != null && p.Metriky.CasVypoctu != default)
            {
                string komplexitaClass = p.Metriky.Komplexita.Contains("Vysoká") ? "prio-high" : (p.Metriky.Komplexita.Contains("Střední") ? "prio-med" : "prio-low");
                sb.AppendLine("                <div class=\"card filterable-section\">");
                sb.AppendLine("                    <div class=\"card-title\">Projektové metriky</div>");
                sb.AppendLine("                    <div class=\"metric-cards-container\">");
                sb.AppendLine("                        <div class=\"metric-mini-card\">");
                sb.AppendLine("                            <div class=\"metric-mini-label\">Vývoj (odhad)</div>");
                sb.AppendLine($"                            <div class=\"metric-mini-value\">{System.Net.WebUtility.HtmlEncode(p.Metriky.CasovyOdhadMin)} - {System.Net.WebUtility.HtmlEncode(p.Metriky.CasovyOdhadMax)}</div>");
                sb.AppendLine("                        </div>");
                sb.AppendLine("                        <div class=\"metric-mini-card\">");
                sb.AppendLine("                            <div class=\"metric-mini-label\">Složitost</div>");
                sb.AppendLine($"                            <div class=\"metric-mini-value\"><span class=\"badge badge-prio badge-{komplexitaClass}\" style=\"margin-left:0;\">{System.Net.WebUtility.HtmlEncode(p.Metriky.Komplexita)}</span></div>");
                sb.AppendLine("                        </div>");
                sb.AppendLine("                        <div class=\"metric-mini-card\">");
                sb.AppendLine("                            <div class=\"metric-mini-label\">Rozpočet</div>");
                sb.AppendLine($"                            <div class=\"metric-mini-value\">{System.Net.WebUtility.HtmlEncode(p.Metriky.DoporucenyRozpocet)}</div>");
                sb.AppendLine("                        </div>");
                sb.AppendLine("                        <div class=\"metric-mini-card\">");
                sb.AppendLine("                            <div class=\"metric-mini-label\">Doporučený tým</div>");
                sb.AppendLine($"                            <div class=\"metric-mini-value\">{System.Net.WebUtility.HtmlEncode(p.Metriky.SlozeniTymu)}</div>");
                sb.AppendLine("                        </div>");
                sb.AppendLine("                    </div>");
                sb.AppendLine("                    <div style=\"font-size:0.85rem; line-height:1.4; border-top:1px solid var(--border); padding-top:12px;\">");
                sb.AppendLine("                        <strong>Architektura a technologie:</strong><br>");
                foreach (var radek in p.Metriky.TechnickyRozbor.Split('\n'))
                {
                    if (string.IsNullOrWhiteSpace(radek)) continue;
                    sb.AppendLine($"                        {System.Net.WebUtility.HtmlEncode(radek)}<br>");
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
                foreach (var us in p.UserStories)
                {
                    string prioClass = us.Priorita == "Vysoká" ? "prio-high" : (us.Priorita == "Střední" ? "prio-med" : "prio-low");
                    sb.AppendLine($"                    <div class=\"backlog-item\" id=\"story-{us.Id}\">");
                    sb.AppendLine($"                        <input type=\"checkbox\" class=\"backlog-checkbox\" onchange=\"toggleStory(this, 'story-{us.Id}')\">");
                    sb.AppendLine("                        <div class=\"backlog-text\">");
                    sb.AppendLine($"                            <div class=\"backlog-title\">{System.Net.WebUtility.HtmlEncode(us.Id)}: {System.Net.WebUtility.HtmlEncode(us.Titulek)} <span class=\"badge badge-prio badge-{prioClass}\">{System.Net.WebUtility.HtmlEncode(us.Priorita)}</span></div>");
                    sb.AppendLine($"                            <div class=\"backlog-desc\">{System.Net.WebUtility.HtmlEncode(us.Popis)}</div>");
                    sb.AppendLine("                            <ul class=\"backlog-criteria-list\">");
                    foreach (var crit in us.Kriteria)
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

            sb.AppendLine("            </div>");
            sb.AppendLine("        </div>");
            sb.AppendLine("    </div>");
            sb.AppendLine();
            sb.AppendLine("    <script>");
            sb.AppendLine("        function toggleTheme() {");
            sb.AppendLine("            const body = document.body;");
            sb.AppendLine("            const theme = body.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';");
            sb.AppendLine("            body.setAttribute('data-theme', theme);");
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
            sb.AppendLine("            const sections = document.querySelectorAll('.filterable-section');");
            sb.AppendLine("            sections.forEach(section => {");
            sb.AppendLine("                const text = section.innerText.toLowerCase();");
            sb.AppendLine("                if (text.includes(query)) {");
            sb.AppendLine("                    section.style.display = 'block';");
            sb.AppendLine("                } else {");
            sb.AppendLine("                    section.style.display = 'none';");
            sb.AppendLine("                }");
            sb.AppendLine("            });");
            sb.AppendLine("        }");
            sb.AppendLine("    </script>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        // ---------- ukládání projektu ----------

        public static void UlozProjekt(SpecProjekt p, string cesta)
        {
            File.WriteAllText(cesta, JsonSerializer.Serialize(p, JsonOpt), new UTF8Encoding(true));
        }

        public static SpecProjekt NactiProjekt(string cesta)
        {
            var text = File.ReadAllText(cesta);
            var p = JsonSerializer.Deserialize<SpecProjekt>(text, JsonOpt);
            return p ?? new SpecProjekt();
        }
    }

    /// <summary>Závažnost nálezu konzistenční kontroly.</summary>
    public enum Zavaznost
    {
        Rozpor,
        Varovani
    }

    /// <summary>Jeden nález kontroly konzistence (kap. 7: hlídání rozporů ve specifikaci).</summary>
    public class Nalez
    {
        public Zavaznost Zavaznost { get; set; }
        public string Titulek { get; set; } = "";
        public string Detail { get; set; } = "";
    }

    /// <summary>Pravidlová kontrola rozporů – offline, bez AI. Hrubé porovnání klíčových slov:
    /// cílem je upozornit na možný problém, ne vynášet soudy. Falešný poplach je přijatelný, mlčení ne.</summary>
    public static class KonzistencniKontrola
    {
        public static List<Nalez> Zkontroluj(SpecProjekt p)
        {
            var nalezy = new List<Nalez>();
            ZkontrolujOfflineOnline(p, nalezy);
            ZkontrolujWebOffline(p, nalezy);
            ZkontrolujOsobniUdaje(p, nalezy);
            ZkontrolujNonGoals(p, nalezy);
            ZkontrolujAkceptaci(p, nalezy);
            ZkontrolujExport(p, nalezy);
            ZkontrolujPlatformu(p, nalezy);
            ZkontrolujPredpoklady(p, nalezy);
            ZkontrolujChybejiciNapad(p, nalezy);
            ZkontrolujSQLiteWeb(p, nalezy);
            ZkontrolujUlohyBezAuth(p, nalezy);
            return nalezy;
        }

        // ---------- pravidla ----------

        /// <summary>Specifikace tvrdí offline, ale jinde se mluví o cloudu/synchronizaci/online.</summary>
        private static void ZkontrolujOfflineOnline(SpecProjekt p, List<Nalez> nalezy)
        {
            if (!RikaOffline(p)) return;
            var zdroje = Zdroje(p, "tech-offline");
            string zdroj;
            string slovo = NajdiSlovo(zdroje, new[] { "cloud", "synchronizac", "online" }, out zdroj);
            if (slovo != null)
                nalezy.Add(new Nalez
                {
                    Zavaznost = Zavaznost.Rozpor,
                    Titulek = "Offline vs. online",
                    Detail = "Technika říká „funguje offline“, ale " + zdroj + " zmiňuje „" + slovo + "“. Rozhodni, co platí."
                });
        }

        /// <summary>Webová platforma + požadavek plně offline = jde jen jako PWA, stojí za ověření.</summary>
        private static void ZkontrolujWebOffline(SpecProjekt p, List<Nalez> nalezy)
        {
            string plat = Norm(TextOdpovedi(p, "tech-platforma"));
            if (!RikaOffline(p) || (!plat.Contains("web") && !plat.Contains("prohlizec"))) return;
            nalezy.Add(new Nalez
            {
                Zavaznost = Zavaznost.Varovani,
                Titulek = "Web + plně offline",
                Detail = "Webová aplikace bez internetu funguje jen jako PWA s offline režimem – ověř, jestli to tak myslíš."
            });
        }

        /// <summary>Tvrdíme „bez osobních údajů“, ale jinde se objevují jména, e-maily, registrace…</summary>
        private static void ZkontrolujOsobniUdaje(SpecProjekt p, List<Nalez> nalezy)
        {
            string data = Norm(TextOdpovedi(p, "data-obsah"));
            bool bezOsobnich = data.Contains("bez osobnich") || data.Contains("neosobni") || data.Contains("zadne osobni");
            if (!bezOsobnich) return;

            var zdroje = Zdroje(p, "data-obsah");
            string zdroj;
            string slovo = NajdiSlovo(zdroje, new[] { "jmeno", "jmena", "email", "e-mail", "telefon", "heslo", "registrac", "prihlas" }, out zdroj);
            if (slovo != null)
                nalezy.Add(new Nalez
                {
                    Zavaznost = Zavaznost.Rozpor,
                    Titulek = "Osobní údaje",
                    Detail = "Data říkají „bez osobních údajů“, ale " + zdroj + " zmiňuje „" + slovo + "“. Osobní údaje = GDPR a vyšší nároky."
                });
        }

        /// <summary>Něco je v non-goals, ale jinde se to popisuje jako funkce.</summary>
        private static void ZkontrolujNonGoals(SpecProjekt p, List<Nalez> nalezy)
        {
            var odp = SpecSluzba.OdpovedNa(p, "rozsah-nongoals");
            if (odp == null || odp.JePredpoklad) return;

            var stop = new HashSet<string> { "zadne", "nebude", "nebudou", "nechci", "nesmi", "prvni", "verze", "verzi",
                "aplikace", "appka", "zatim", "pozdeji", "budou", "chceme", "nechceme", "resit", "nema", "mit" };
            var zdroje = Zdroje(p, "rozsah-nongoals", "akceptace", "rizika");
            int hitu = 0;

            foreach (var fragment in odp.Text.Split(new[] { ',', ';', '\n', '•' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var slova = Norm(fragment)
                    .Split(new[] { ' ', '.', '!', '?', '(', ')', '"', '-', ':' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length >= 5 && !stop.Contains(w));

                foreach (var w in slova)
                {
                    string zdrojNg = null;
                    foreach (var z in zdroje)
                        if (z.Value.Contains(w)) { zdrojNg = z.Key; break; }
                    if (zdrojNg == null) continue;

                    nalezy.Add(new Nalez
                    {
                        Zavaznost = Zavaznost.Varovani,
                        Titulek = "Non-goal se objevuje jinde",
                        Detail = "„" + fragment.Trim() + "“ je v non-goals, ale " + zdrojNg + " o tom mluví („" + w + "“). Patří to do v1, nebo ne?"
                    });
                    hitu++;
                    break;
                }
                if (hitu >= 2) break;
            }
        }

        /// <summary>Akceptační kritéria moc stručná na to, aby šla ověřit.</summary>
        private static void ZkontrolujAkceptaci(SpecProjekt p, List<Nalez> nalezy)
        {
            var odp = SpecSluzba.OdpovedNa(p, "akceptace");
            if (odp == null || odp.JePredpoklad) return;
            if (Norm(odp.Text).Trim().Length >= 20) return;
            nalezy.Add(new Nalez
            {
                Zavaznost = Zavaznost.Varovani,
                Titulek = "Akceptace je moc stručná",
                Detail = "Podle takhle krátkých kritérií nepůjde poznat, že je hotovo. Napiš 2–3 konkrétní ověřitelné podmínky."
            });
        }

        /// <summary>Export je odmítnutý, ale jinde se o něm mluví.</summary>
        private static void ZkontrolujExport(SpecProjekt p, List<Nalez> nalezy)
        {
            string exp = Norm(TextOdpovedi(p, "data-export"));
            bool bezExportu = exp.Contains("bez export") || exp.Contains("zadny export");
            if (!bezExportu) return;

            // non-goals a rizika legitimně říkají „bez exportu“ – ty nekontrolujeme
            var zdroje = Zdroje(p, "data-export", "rozsah-nongoals", "rizika", "akceptace", "tech-offline", "tech-platforma", "cil-uzivatele");
            string zdroj;
            string slovo = NajdiSlovo(zdroje, new[] { "export", "csv", "tisk", "import", "do pdf", "sestav" }, out zdroj);
            if (slovo != null)
                nalezy.Add(new Nalez
                {
                    Zavaznost = Zavaznost.Rozpor,
                    Titulek = "Export ano, nebo ne?",
                    Detail = "Data říkají „bez exportu“, ale " + zdroj + " zmiňuje „" + slovo + "“. Export je levný teď, drahý dodatečně."
                });
        }

        /// <summary>Platforma je desktop/Windows, ale jinde se mluví o mobilu.</summary>
        private static void ZkontrolujPlatformu(SpecProjekt p, List<Nalez> nalezy)
        {
            string plat = Norm(TextOdpovedi(p, "tech-platforma"));
            bool desktop = plat.Contains("windows") || plat.Contains("desktop") || plat.Contains("pocitac");
            if (!desktop) return;

            var zdroje = Zdroje(p, "tech-platforma");
            string zdroj;
            string slovo = NajdiSlovo(zdroje, new[] { "mobil", "telefon", "android", "iphone" }, out zdroj);
            if (slovo != null)
                nalezy.Add(new Nalez
                {
                    Zavaznost = Zavaznost.Varovani,
                    Titulek = "Desktop vs. mobil",
                    Detail = "Platforma je Windows/desktop, ale " + zdroj + " zmiňuje „" + slovo + "“. Patří mobil do první verze?"
                });
        }

        /// <summary>Moc předpokladů u otázek s vysokým dopadem = křehká specifikace.</summary>
        private static void ZkontrolujPredpoklady(SpecProjekt p, List<Nalez> nalezy)
        {
            int pocet = SpecSluzba.VratOtazkyProjektu(p).Count(ot =>
            {
                if (ot.Dopad != Dopad.Vysoky) return false;
                var o = SpecSluzba.OdpovedNa(p, ot.Id);
                return o != null && o.JePredpoklad;
            });
            if (pocet < 3) return;
            nalezy.Add(new Nalez
            {
                Zavaznost = Zavaznost.Varovani,
                Titulek = "Hodně předpokladů s vysokým dopadem",
                Detail = "Specifikace stojí na " + pocet + " nepotvrzených předpokladech s vysokým dopadem. Projdi je a potvrď, ať agent nestaví na písku."
            });
        }

        /// <summary>Odpovídá se na otázky, ale chybí původní nápad.</summary>
        private static void ZkontrolujChybejiciNapad(SpecProjekt p, List<Nalez> nalezy)
        {
            if (!string.IsNullOrWhiteSpace(p.Napad) || p.Odpovedi.Count < 3) return;
            nalezy.Add(new Nalez
            {
                Zavaznost = Zavaznost.Varovani,
                Titulek = "Chybí původní nápad",
                Detail = "Máš zodpovězené otázky, ale pole s nápadem je prázdné. Bez něj chybí kontext, proč appka vzniká."
            });
        }

        // ---------- pomocné ----------

        private static bool RikaOffline(SpecProjekt p)
        {
            string off = Norm(TextOdpovedi(p, "tech-offline"));
            return off.Contains("offline") || off.Contains("bez internetu") || off.Contains("bez pripojeni");
        }

        /// <summary>Normalizace pro porovnávání: malá písmena, bez české diakritiky.
        /// Ruční mapování – string.Normalize() nefunguje s InvariantGlobalization (bez ICU).</summary>
        private const string Diakritika = "áčďéěíňóřšťúůýž";
        private const string BezDiakritiky = "acdeeinorstuuyz";

        private static string Norm(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char puvodni in s)
            {
                char c = char.ToLowerInvariant(puvodni);
                int i = Diakritika.IndexOf(c);
                sb.Append(i >= 0 ? BezDiakritiky[i] : c);
            }
            return sb.ToString();
        }

        private static string TextOdpovedi(SpecProjekt p, string id)
        {
            var o = SpecSluzba.OdpovedNa(p, id);
            return o == null ? "" : o.Text;
        }

        /// <summary>Texty k prohledání: nápad + všechny odpovědi kromě vyjmenovaných otázek (klíč = popis zdroje, hodnota = normalizovaný text).</summary>
        private static List<KeyValuePair<string, string>> Zdroje(SpecProjekt p, params string[] krome)
        {
            var vysledek = new List<KeyValuePair<string, string>>();
            if (!string.IsNullOrWhiteSpace(p.Napad))
                vysledek.Add(new KeyValuePair<string, string>("nápad", Norm(p.Napad)));
            foreach (var o in p.Odpovedi)
            {
                if (krome.Contains(o.OtazkaId)) continue;
                var ot = SpecSluzba.Podle(p, o.OtazkaId);
                if (ot == null) continue;
                vysledek.Add(new KeyValuePair<string, string>("odpověď na „" + Zkratka(ot.Text) + "“", Norm(o.Text)));
            }
            return vysledek;
        }

        private static string Zkratka(string s) => s.Length <= 40 ? s : s.Substring(0, 39) + "…";

        private static string NajdiSlovo(List<KeyValuePair<string, string>> zdroje, string[] slova, out string zdroj)
        {
            foreach (var z in zdroje)
                foreach (var s in slova)
                    if (z.Value.Contains(s)) { zdroj = z.Key; return s; }
            zdroj = null;
            return null;
        }

        private static void ZkontrolujSQLiteWeb(SpecProjekt p, List<Nalez> nalezy)
        {
            string plat = Norm(TextOdpovedi(p, "tech-platforma"));
            string data = Norm(TextOdpovedi(p, "data-obsah"));

            bool isWeb = plat.Contains("web") || plat.Contains("prohlizec") || plat.Contains("browser");
            bool isSqlite = data.Contains("sqlite");

            if (isWeb && isSqlite && !data.Contains("wasm") && !data.Contains("localstorage"))
            {
                nalezy.Add(new Nalez
                {
                    Zavaznost = Zavaznost.Varovani,
                    Titulek = "SQLite databáze na webu",
                    Detail = "Technologie uvádí webovou aplikaci a SQLite databázi. SQLite standardně neběží v prohlížeči. Zvaž LocalStorage/IndexedDB, nebo to uveď jako WASM/backend."
                });
            }
        }

        private static void ZkontrolujUlohyBezAuth(SpecProjekt p, List<Nalez> nalezy)
        {
            string uziv = Norm(TextOdpovedi(p, "cil-uzivatele"));
            string tech = Norm(TextOdpovedi(p, "tech-platforma"));
            string data = Norm(TextOdpovedi(p, "data-obsah"));

            bool maRole = uziv.Contains("admin") || uziv.Contains("moderator") || uziv.Contains("opravneni") || uziv.Contains("role");
            bool maPrihlaseni = tech.Contains("prihlas") || tech.Contains("login") || tech.Contains("auth") || tech.Contains("heslo") || tech.Contains("ucet") || tech.Contains("registrac") ||
                                data.Contains("prihlas") || data.Contains("login") || data.Contains("auth") || data.Contains("heslo") || data.Contains("ucet") || data.Contains("registrac");

            if (maRole && !maPrihlaseni)
            {
                nalezy.Add(new Nalez
                {
                    Zavaznost = Zavaznost.Varovani,
                    Titulek = "Uživatelské role bez přihlašování",
                    Detail = "Zmiňuješ uživatelské role (např. administrátor, oprávnění), ale v technologii ani datech se neřeší autentizace. Jak se role rozpoznají?"
                });
            }
        }
    }
}
// konec souboru
