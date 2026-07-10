using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using VoiceCoderBrief.Core;

internal static class Testy
{
    private static int _ok = 0;

    private static void Over(bool podminka, string popis)
    {
        if (!podminka) throw new Exception("TEST SELHAL: " + popis);
        _ok++;
        Console.WriteLine("  ok: " + popis);
    }

    private static void Main()
    {
        Console.WriteLine("== Testy jádra VoiceCoder Brief ==");

        // --- základní stav ---
        var p = new SpecProjekt { Nazev = "Hotelová evidence" };
        Over(Otazky.Vse.Count == 10, "sada má 10 řízených otázek");
        Over(Otazky.Vse.Take(7).All(o => o.Dopad == Dopad.Vysoky), "prvních 7 otázek má vysoký dopad (question planner)");
        Over(SpecSluzba.DalsiNezodpovezena(p).Id == "cil-problem", "první otázka je cíl/problém");

        // --- nápad ---
        SpecSluzba.NastavNapad(p, "Udělej mi aplikaci na počítání hostů v hotelu.\nPotřebuju ji i bez internetu.");
        Over(p.Verze == 2, "úprava nápadu zvýšila verzi specifikace");
        Over(p.Log.Any(l => l.Akce == "Nápad"), "úprava nápadu je v logu");

        // --- odpovědi a předpoklady ---
        SpecSluzba.Odpovez(p, "cil-problem", "Recepce ztrácí přehled o počtu hostů; appka dá rychlý denní přehled.");
        SpecSluzba.Odpovez(p, "tech-platforma", "Windows počítač na recepci.");
        SpecSluzba.PouzijPredpoklad(p, "rozsah-nongoals");
        Over(SpecSluzba.PocetZodpovezenych(p) == 2, "2 skutečné odpovědi");
        Over(SpecSluzba.PocetPredpokladu(p) == 1, "1 označený předpoklad");
        Over(SpecSluzba.OtevreneOtazky(p).Count == 7, "7 otázek zůstává otevřených");
        Over(p.Verze == 5, "každé rozhodnutí zvyšuje verzi (2+3=5)");

        // --- přepsání odpovědi ---
        SpecSluzba.Odpovez(p, "tech-platforma", "Windows počítač na recepci + později tablet.");
        Over(SpecSluzba.PocetZodpovezenych(p) == 2, "přepsání odpovědi neduplikuje záznam");
        Over(p.Log.Any(l => l.Akce == "Změna odpovědi"), "přepsání odpovědi je v logu jako změna");

        // --- Markdown ---
        var md = SpecSluzba.RenderMarkdown(p);
        Over(md.Contains("# Specifikace: Hotelová evidence"), "MD: nadpis s názvem projektu");
        foreach (var s in SpecSluzba.PoradiSekci)
            Over(md.Contains("## " + s), "MD: obsahuje sekci " + s);
        Over(md.Contains("[PŘEDPOKLAD]"), "MD: předpoklad je viditelně označen");
        Over(md.Contains("> Udělej mi aplikaci"), "MD: původní nápad je citován");
        Over(md.Contains("## Otevřené otázky"), "MD: sekce otevřených otázek");
        Over(md.Contains("## Log rozhodnutí"), "MD: log rozhodnutí");

        // --- JSON ---
        var json = SpecSluzba.RenderJson(p);
        using (var doc = JsonDocument.Parse(json))
        {
            var root = doc.RootElement;
            Over(root.GetProperty("projekt").GetString() == "Hotelová evidence", "JSON: název projektu");
            Over(root.GetProperty("sekce").GetArrayLength() == 7, "JSON: 7 sekcí");
            Over(root.GetProperty("otevreneOtazky").GetArrayLength() == 7, "JSON: 7 otevřených otázek");
            Over(root.GetProperty("logRozhodnuti").GetArrayLength() == p.Log.Count, "JSON: kompletní log");
            var technika = root.GetProperty("sekce").EnumerateArray()
                .First(s => s.GetProperty("nazev").GetString() == "Technika");
            Over(technika.GetProperty("polozky").GetArrayLength() == 1, "JSON: Technika má 1 položku");
            Over(json.Contains("Hotelová"), "JSON: česká diakritika není escapovaná");
        }

        // --- uložení a načtení projektu ---
        string tmp = Path.Combine(Path.GetTempPath(), "test_projekt.vcbrief");
        SpecSluzba.UlozProjekt(p, tmp);
        var p2 = SpecSluzba.NactiProjekt(tmp);
        Over(p2.Nazev == p.Nazev, "roundtrip: název sedí");
        Over(p2.Odpovedi.Count == p.Odpovedi.Count, "roundtrip: počet odpovědí sedí");
        Over(p2.Log.Count == p.Log.Count, "roundtrip: log sedí");
        Over(p2.Verze == p.Verze, "roundtrip: verze sedí");
        Over(SpecSluzba.RenderMarkdown(p2).Contains("[PŘEDPOKLAD]"), "roundtrip: render funguje i po načtení");
        File.Delete(tmp);

        // --- prázdný projekt se vyrenderuje bez chyby ---
        var prazdny = new SpecProjekt();
        var mdPrazdny = SpecSluzba.RenderMarkdown(prazdny);
        Over(mdPrazdny.Contains("(nepojmenovaný projekt)"), "prázdný projekt: bezpečný nadpis");
        Over(mdPrazdny.Contains("(zatím nezadán"), "prázdný projekt: výzva k zadání nápadu");
        SpecSluzba.RenderJson(prazdny);

        Console.WriteLine();
        Console.WriteLine("VSECHNY TESTY OK (" + _ok + " kontrol)");
    }
}
