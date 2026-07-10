using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VoiceCoderBrief.Core
{
    /// <summary>Dopad otázky na projekt – řídí pořadí dotazování (question planner dle návrhu, kap. 7).</summary>
    public enum Dopad
    {
        Vysoky,
        Stredni
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

    /// <summary>Celý projekt = zdroj pravdy. Ukládá se jako .vcbrief (JSON).</summary>
    public class SpecProjekt
    {
        public string Nazev { get; set; } = "";
        public string Napad { get; set; } = "";
        public DateTime Vytvoreno { get; set; } = DateTime.Now;
        public DateTime Upraveno { get; set; } = DateTime.Now;
        public int Verze { get; set; } = 1;
        public List<Odpoved> Odpovedi { get; set; } = new List<Odpoved>();
        public List<Rozhodnuti> Log { get; set; } = new List<Rozhodnuti>();
    }

    /// <summary>Pevná sada řízených otázek – seřazená podle dopadu (nejdřív rozhodnutí, která mění architekturu, cenu nebo bezpečnost).</summary>
    public static class Otazky
    {
        public static readonly IReadOnlyList<Otazka> Vse = new List<Otazka>
        {
            new Otazka { Id = "cil-problem", Sekce = "Cíl a uživatelé", Dopad = Dopad.Vysoky,
                Text = "Jaký problém má aplikace vyřešit a jaký přínos od ní čekáš?",
                Napoveda = "Určuje smysl celé specifikace – všechno ostatní se od toho odvíjí.",
                VychoziPredpoklad = "Přínos je popsán jen v původním nápadu; bude upřesněn po první ukázce." },

            new Otazka { Id = "cil-uzivatele", Sekce = "Cíl a uživatelé", Dopad = Dopad.Vysoky,
                Text = "Kdo bude aplikaci používat? (role, zkušenost s počítačem, kolik lidí)",
                Napoveda = "Jiné UX pro recepční, jiné pro vývojáře. Ovlivňuje složitost rozhraní.",
                VychoziPredpoklad = "Jediný uživatel – autor nápadu." },

            new Otazka { Id = "tech-platforma", Sekce = "Technika", Dopad = Dopad.Vysoky,
                Text = "Na čem má běžet první verze? (Windows program, web, mobil…)",
                Napoveda = "Mění architekturu i pracnost – proto je to jedna z prvních otázek.",
                VychoziPredpoklad = "Desktopová aplikace pro Windows." },

            new Otazka { Id = "tech-offline", Sekce = "Technika", Dopad = Dopad.Vysoky,
                Text = "Musí fungovat bez internetu, nebo může počítat s připojením?",
                Napoveda = "Offline režim zásadně ovlivňuje ukládání dat i synchronizaci.",
                VychoziPredpoklad = "Plně offline, bez závislosti na připojení." },

            new Otazka { Id = "data-obsah", Sekce = "Data", Dopad = Dopad.Vysoky,
                Text = "Jaká data se budou ukládat? Budou mezi nimi osobní údaje?",
                Napoveda = "Osobní údaje = právní dopad (GDPR) a vyšší nároky na zabezpečení.",
                VychoziPredpoklad = "Jen neosobní provozní data; bez osobních údajů." },

            new Otazka { Id = "rozsah-nongoals", Sekce = "Rozsah", Dopad = Dopad.Vysoky,
                Text = "Co v první verzi záměrně NEMÁ být? (non-goals)",
                Napoveda = "Výslovné non-goals chrání projekt před nafukováním rozsahu.",
                VychoziPredpoklad = "Non-goals zatím neurčeny – riziko rozšiřování rozsahu." },

            new Otazka { Id = "akceptace", Sekce = "Akceptace", Dopad = Dopad.Vysoky,
                Text = "Podle čeho poznáš, že je hotovo? Napiš 2–3 ověřitelné podmínky.",
                Napoveda = "Testovatelné podmínky určují hotový výsledek – bez nich nejde ověřit úspěch.",
                VychoziPredpoklad = "Akceptační kritéria budou doplněna po první ukázce." },

            new Otazka { Id = "ux-obrazovky", Sekce = "UX", Dopad = Dopad.Stredni,
                Text = "Jaké hlavní obrazovky nebo kroky uživatel projde?",
                Napoveda = "Stačí hrubý tah: kde uživatel začne, co udělá, kde skončí.",
                VychoziPredpoklad = "Jedna hlavní obrazovka; detaily vzejdou z první verze." },

            new Otazka { Id = "data-export", Sekce = "Data", Dopad = Dopad.Stredni,
                Text = "Potřebuješ export nebo import dat? V jakém formátu?",
                Napoveda = "Export (CSV, PDF…) bývá levný teď, drahý dodatečně.",
                VychoziPredpoklad = "Bez exportu a importu v první verzi." },

            new Otazka { Id = "rizika", Sekce = "Rizika", Dopad = Dopad.Stredni,
                Text = "Je něco nejasného nebo rizikového, co je potřeba ověřit?",
                Napoveda = "Nejasnosti pojmenované předem šetří opravy později.",
                VychoziPredpoklad = "Rizika zatím nezmapována." },
        };

        public static Otazka Podle(string id)
        {
            foreach (var o in Vse) if (o.Id == id) return o;
            return null;
        }
    }

    /// <summary>Logika nad projektem: odpovědi, předpoklady, verzování, rendering a ukládání.</summary>
    public static class SpecSluzba
    {
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

        public static void NastavNapad(SpecProjekt p, string napad)
        {
            if ((p.Napad ?? "") == (napad ?? "")) return;
            p.Napad = napad ?? "";
            Zmena(p, "Nápad", "Upraven text původního nápadu.");
        }

        public static void Odpovez(SpecProjekt p, string otazkaId, string text)
        {
            var ot = Otazky.Podle(otazkaId);
            if (ot == null || string.IsNullOrWhiteSpace(text)) return;

            var stara = p.Odpovedi.FirstOrDefault(o => o.OtazkaId == otazkaId);
            if (stara != null) p.Odpovedi.Remove(stara);

            p.Odpovedi.Add(new Odpoved { OtazkaId = otazkaId, Text = text.Trim(), JePredpoklad = false, Cas = DateTime.Now });
            Zmena(p, stara == null ? "Odpověď" : "Změna odpovědi", ot.Text + " → " + Zkrat(text, 120));
        }

        public static void PouzijPredpoklad(SpecProjekt p, string otazkaId)
        {
            var ot = Otazky.Podle(otazkaId);
            if (ot == null) return;

            var stara = p.Odpovedi.FirstOrDefault(o => o.OtazkaId == otazkaId);
            if (stara != null) p.Odpovedi.Remove(stara);

            p.Odpovedi.Add(new Odpoved { OtazkaId = otazkaId, Text = ot.VychoziPredpoklad, JePredpoklad = true, Cas = DateTime.Now });
            Zmena(p, "Předpoklad", ot.Text + " → [PŘEDPOKLAD] " + ot.VychoziPredpoklad);
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
            => Otazky.Vse.FirstOrDefault(ot => OdpovedNa(p, ot.Id) == null);

        public static int PocetZodpovezenych(SpecProjekt p)
            => Otazky.Vse.Count(ot => { var o = OdpovedNa(p, ot.Id); return o != null && !o.JePredpoklad; });

        public static int PocetPredpokladu(SpecProjekt p)
            => Otazky.Vse.Count(ot => { var o = OdpovedNa(p, ot.Id); return o != null && o.JePredpoklad; });

        public static List<Otazka> OtevreneOtazky(SpecProjekt p)
            => Otazky.Vse.Where(ot => OdpovedNa(p, ot.Id) == null).ToList();

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
            sb.AppendLine("*Verze specifikace " + p.Verze + " · aktualizováno " + Datum(p.Upraveno) + "*");
            sb.AppendLine("*Vytvořeno nástrojem VoiceCoder Brief (demonstrátor bez AI)*");
            sb.AppendLine();

            sb.AppendLine("## Původní nápad");
            if (string.IsNullOrWhiteSpace(p.Napad))
                sb.AppendLine("> (zatím nezadán – napiš nebo nadiktuj svůj nápad)");
            else
                foreach (var radek in p.Napad.Trim().Split('\n'))
                    sb.AppendLine("> " + radek.TrimEnd());
            sb.AppendLine();

            foreach (var sekce in PoradiSekci)
            {
                sb.AppendLine("## " + sekce);
                var otazkySekce = Otazky.Vse.Where(o => o.Sekce == sekce).ToList();
                bool neco = false;

                foreach (var ot in otazkySekce)
                {
                    var odp = OdpovedNa(p, ot.Id);
                    if (odp == null) continue;
                    neco = true;
                    string znacka = odp.JePredpoklad ? " **[PŘEDPOKLAD]**" : "";
                    sb.AppendLine("- **" + ot.Text + "**" + znacka);
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
                    sb.AppendLine("- [" + (ot.Dopad == Dopad.Vysoky ? "vysoký dopad" : "střední dopad") + "] " + ot.Text);
            sb.AppendLine();

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
                    polozky.Add(new { id = ot.Id, otazka = ot.Text, odpoved = odp.Text, predpoklad = odp.JePredpoklad });
                }
                sekce.Add(new { nazev = nazevSekce, polozky });
            }

            var data = new
            {
                nastroj = "VoiceCoder Brief",
                verzeNastroje = "0.1.0",
                projekt = p.Nazev,
                verzeSpecifikace = p.Verze,
                vytvoreno = p.Vytvoreno,
                upraveno = p.Upraveno,
                napad = p.Napad,
                sekce,
                otevreneOtazky = OtevreneOtazky(p).Select(o => new { id = o.Id, otazka = o.Text }).ToList(),
                logRozhodnuti = p.Log.Select(r => new { cas = r.Cas, akce = r.Akce, detail = r.Detail }).ToList()
            };

            return JsonSerializer.Serialize(data, JsonOpt);
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
}
