using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using CodePlanner.Core;

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
        Console.WriteLine("== Testy jádra CodePlanner ==");

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

        // --- kontrola konzistence ---
        var cisty = new SpecProjekt { Napad = "Jednoduchá kalkulačka." };
        SpecSluzba.Odpovez(cisty, "tech-offline", "Plně offline.");
        Over(KonzistencniKontrola.Zkontroluj(cisty).Count == 0, "kontrola: čistý projekt bez nálezů");

        var k1 = new SpecProjekt { Napad = "Appka se synchronizací do cloudu mezi zařízeními." };
        SpecSluzba.Odpovez(k1, "tech-offline", "Musí fungovat plně offline.");
        var n1 = KonzistencniKontrola.Zkontroluj(k1);
        Over(n1.Any(n => n.Titulek == "Offline vs. online" && n.Zavaznost == Zavaznost.Rozpor), "kontrola: offline×cloud je rozpor");

        var k2 = new SpecProjekt { Napad = "Evidence hostů: jméno a email každého hosta." };
        SpecSluzba.Odpovez(k2, "data-obsah", "Jen neosobní provozní data, bez osobních údajů.");
        Over(KonzistencniKontrola.Zkontroluj(k2).Any(n => n.Titulek == "Osobní údaje"), "kontrola: osobní údaje×bez osobních je rozpor");

        var k3 = new SpecProjekt { Napad = "Program na skladovou evidenci." };
        SpecSluzba.Odpovez(k3, "rozsah-nongoals", "tisk sestav, statistiky prodeje");
        SpecSluzba.Odpovez(k3, "ux-obrazovky", "Hlavní obrazovka se seznamem, detail položky, tisk sestav na konci měsíce.");
        Over(KonzistencniKontrola.Zkontroluj(k3).Any(n => n.Titulek == "Non-goal se objevuje jinde"), "kontrola: non-goal zmíněný v UX je varování");

        var k4 = new SpecProjekt();
        SpecSluzba.Odpovez(k4, "akceptace", "funguje");
        Over(KonzistencniKontrola.Zkontroluj(k4).Any(n => n.Titulek == "Akceptace je moc stručná"), "kontrola: vágní akceptace je varování");

        var k5 = new SpecProjekt { Napad = "Chci export do CSV pro účetní." };
        SpecSluzba.PouzijPredpoklad(k5, "data-export");
        Over(KonzistencniKontrola.Zkontroluj(k5).Any(n => n.Titulek == "Export ano, nebo ne?"), "kontrola: export v nápadu × bez exportu je rozpor");

        var k6 = new SpecProjekt();
        SpecSluzba.PouzijPredpoklad(k6, "cil-problem");
        SpecSluzba.PouzijPredpoklad(k6, "cil-uzivatele");
        SpecSluzba.PouzijPredpoklad(k6, "tech-platforma");
        var n6 = KonzistencniKontrola.Zkontroluj(k6);
        Over(n6.Any(n => n.Titulek == "Hodně předpokladů s vysokým dopadem"), "kontrola: 3 předpoklady s vysokým dopadem jsou varování");
        Over(n6.Any(n => n.Titulek == "Chybí původní nápad"), "kontrola: prázdný nápad + odpovědi je varování");

        Over(SpecSluzba.RenderMarkdown(k1).Contains("## Kontrola konzistence"), "MD: sekce kontroly konzistence při nálezech");
        Over(!SpecSluzba.RenderMarkdown(cisty).Contains("## Kontrola konzistence"), "MD: sekce kontroly chybí bez nálezů");
        using (var docK = JsonDocument.Parse(SpecSluzba.RenderJson(k1)))
            Over(docK.RootElement.GetProperty("kontrolaKonzistence").GetArrayLength() >= 1, "JSON: pole kontrolaKonzistence");

        var kSqliteWeb = new SpecProjekt();
        SpecSluzba.Odpovez(kSqliteWeb, "tech-platforma", "Funguje to ve webovém prohlížeči.");
        SpecSluzba.Odpovez(kSqliteWeb, "data-obsah", "Uložíme to do SQLite databáze.");
        Over(KonzistencniKontrola.Zkontroluj(kSqliteWeb).Any(n => n.Titulek == "SQLite databáze na webu"), "kontrola: SQLite na webu je varování");

        var kAuth = new SpecProjekt();
        SpecSluzba.Odpovez(kAuth, "cil-uzivatele", "Bude tam administrátor a běžný host.");
        SpecSluzba.Odpovez(kAuth, "tech-platforma", "Pouze desktop.");
        SpecSluzba.Odpovez(kAuth, "data-obsah", "Data budeme ukládat lokálně do souborů.");
        Over(KonzistencniKontrola.Zkontroluj(kAuth).Any(n => n.Titulek == "Uživatelské role bez přihlašování"), "kontrola: role bez auth je varování");

        // --- šablony otázek ---
        var pSablony = new SpecProjekt { Nazev = "Test šablon" };
        Over(pSablony.TypProjektu == TypProjektu.Obecna, "výchozí typ projektu je Obecna");
        SpecSluzba.PouzijPredpoklad(pSablony, "cil-problem");
        var odpObecna = SpecSluzba.OdpovedNa(pSablony, "cil-problem");
        Over(odpObecna.Text == Otazky.Podle("cil-problem").GetVychoziPredpoklad(TypProjektu.Obecna), "předpoklad odpovídá obecnému typu");

        SpecSluzba.ZmenTypProjektu(pSablony, TypProjektu.Hra);
        Over(pSablony.TypProjektu == TypProjektu.Hra, "změna typu projektu na Hra");
        var odpHra = SpecSluzba.OdpovedNa(pSablony, "cil-problem");
        Over(odpHra.Text == Otazky.Podle("cil-problem").GetVychoziPredpoklad(TypProjektu.Hra), "předpoklad se zaktualizoval na Hra");
        Over(pSablony.Log.Any(l => l.Akce == "Typ projektu" && l.Detail.Contains("Změna typu projektu")), "záznam o změně typu v logu");

        // --- Gemini nastavení a prompt testy ---
        string tmpSettings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CodePlanner", "settings.json");
        string originalSettingsContent = null;
        if (File.Exists(tmpSettings))
        {
            originalSettingsContent = File.ReadAllText(tmpSettings);
        }

        try
        {
            var settings = new GeminiNastaveni
            {
                GeminiApiKey = "test-key-12345",
                GeminiModel = "gemini-2.5-flash"
            };
            settings.Uloz();

            var loaded = GeminiNastaveni.Nacti();
            Over(loaded.GeminiApiKey == "test-key-12345", "nastavení: načtení API klíče funguje");
            Over(loaded.GeminiModel == "gemini-2.5-flash", "nastavení: načtení modelu funguje");
            Over(loaded.EfektivniApiKey == "test-key-12345", "nastavení: efektivní klíč vrací hodnotu z nastavení");

            Environment.SetEnvironmentVariable("GEMINI_API_KEY", "env-key-999");
            loaded.GeminiApiKey = "";
            Over(loaded.EfektivniApiKey == "env-key-999", "nastavení: fallback na proměnnou prostředí funguje");
            Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);

            // --- Testy historie nedávných projektů (v0.8) ---
            var settingsRef = new GeminiNastaveni();
            Over(settingsRef.NedavneProjekty != null, "nastavení: nedávné projekty nejsou null po inicializaci");

            settingsRef.PridejNedavnyProjekt("c:\\projekty\\a.vcbrief");
            Over(settingsRef.NedavneProjekty.Count == 1, "nedávné: počet položek je 1");
            Over(settingsRef.NedavneProjekty[0] == "c:\\projekty\\a.vcbrief", "nedávné: nová položka je na začátku");

            settingsRef.PridejNedavnyProjekt("c:\\projekty\\b.vcbrief");
            Over(settingsRef.NedavneProjekty.Count == 2, "nedávné: počet položek je 2");
            Over(settingsRef.NedavneProjekty[0] == "c:\\projekty\\b.vcbrief", "nedávné: druhá přidaná položka je na začátku");
            Over(settingsRef.NedavneProjekty[1] == "c:\\projekty\\a.vcbrief", "nedávné: první položka se posunula");

            // duplicitní přidání by mělo položku přesunout na začátek
            settingsRef.PridejNedavnyProjekt("c:\\projekty\\a.vcbrief");
            Over(settingsRef.NedavneProjekty.Count == 2, "nedávné: duplicitní přidání nezvýšilo počet");
            Over(settingsRef.NedavneProjekty[0] == "c:\\projekty\\a.vcbrief", "nedávné: přesunutí na začátek");

            // naplníme více než 5 souborů
            settingsRef.PridejNedavnyProjekt("c:\\projekty\\1.vcbrief");
            settingsRef.PridejNedavnyProjekt("c:\\projekty\\2.vcbrief");
            settingsRef.PridejNedavnyProjekt("c:\\projekty\\3.vcbrief");
            settingsRef.PridejNedavnyProjekt("c:\\projekty\\4.vcbrief");
            settingsRef.PridejNedavnyProjekt("c:\\projekty\\5.vcbrief");
            Over(settingsRef.NedavneProjekty.Count == 5, "nedávné: maximální počet položek je 5");
            Over(settingsRef.NedavneProjekty[0] == "c:\\projekty\\5.vcbrief", "nedávné: poslední přidaný je na indexu 0");
            Over(!settingsRef.NedavneProjekty.Contains("c:\\projekty\\a.vcbrief"), "nedávné: nejstarší byl vytlačen");

            // odebrání položky
            settingsRef.OdeberNedavnyProjekt("c:\\projekty\\3.vcbrief");
            Over(settingsRef.NedavneProjekty.Count == 4, "nedávné: odebrání snížilo počet na 4");
            Over(!settingsRef.NedavneProjekty.Contains("c:\\projekty\\3.vcbrief"), "nedávné: odebraný prvek chybí");
        }
        finally
        {
            if (originalSettingsContent != null)
            {
                File.WriteAllText(tmpSettings, originalSettingsContent);
            }
            else if (File.Exists(tmpSettings))
            {
                File.Delete(tmpSettings);
            }
        }

        var prompt = GeminiService.SestavPrompt("Chci vytvořit jednoduchou kalkulačku", TypProjektu.Obecna.ToString());
        Over(!string.IsNullOrWhiteSpace(prompt), "prompt: sestavení promptu vrací neprázdný text");
        Over(prompt.Contains("Chci vytvořit jednoduchou kalkulačku"), "prompt: obsahuje původní nápad");
        Over(prompt.Contains("cil-problem"), "prompt: obsahuje ID otázky");
        Over(prompt.Contains("Výchozí předpoklad: Plně offline"), "prompt: obsahuje výchozí předpoklady");

        // --- Testy referenčních podkladů (v0.7) ---
        var promptSReferenci = GeminiService.SestavPrompt("Nápad", TypProjektu.Obecna.ToString(), "Toto jsou referenční specifikace.");
        Over(promptSReferenci.Contains("Toto jsou referenční specifikace."), "prompt: obsahuje referenční podklady");

        var pRef = new SpecProjekt
        {
            Nazev = "Test reference",
            Napad = "Nápad",
            ReferencniText = "Dokumentace k API v1",
            ReferencniNazev = "api.txt"
        };

        string mdRef = SpecSluzba.RenderMarkdown(pRef);
        Over(mdRef.Contains("Referenční podklady (api.txt)"), "MD: obsahuje sekci referenčních podkladů");
        Over(mdRef.Contains("Dokumentace k API v1"), "MD: obsahuje text referenčních podkladů");

        string jsonRef = SpecSluzba.RenderJson(pRef);
        Over(jsonRef.Contains("\"referencniText\": \"Dokumentace k API v1\""), "JSON: obsahuje referencniText");
        Over(jsonRef.Contains("\"referencniNazev\": \"api.txt\""), "JSON: obsahuje referencniNazev");

        // Otestujeme uložení a načtení (roundtrip)
        string tmpCestaRef = Path.Combine(Path.GetTempPath(), "test_ref.vcbrief");
        try
        {
            SpecSluzba.UlozProjekt(pRef, tmpCestaRef);
            var nactenyRef = SpecSluzba.NactiProjekt(tmpCestaRef);
            Over(nactenyRef.ReferencniText == "Dokumentace k API v1", "roundtrip: referencniText sedí");
            Over(nactenyRef.ReferencniNazev == "api.txt", "roundtrip: referencniNazev sedí");
        }
        finally
        {
            if (File.Exists(tmpCestaRef)) File.Delete(tmpCestaRef);
        }

        // --- Testy vlastních šablon (v0.9) ---
        var customSablona = new SablonaProjektu
        {
            Klic = "discord-bot",
            Nazev = "Discord Bot",
            Otazky = new List<SablonaOtazka>
            {
                new SablonaOtazka
                {
                    Id = "cil-problem",
                    Text = "Jaké příkazy má Discord bot umět?",
                    Napoveda = "Nápověda k botovi.",
                    VychoziPredpoklad = "Základní příkazy."
                }
            }
        };

        SablonaSluzba.CustomSablony.Add(customSablona);

        var pCustom = new SpecProjekt
        {
            Nazev = "Test bot",
            Napad = "Nápad na bota",
            TypProjektuKlic = "discord-bot"
        };

        var otCil = Otazky.Podle("cil-problem");
        Over(otCil.GetText(pCustom.TypProjektuKlic) == "Jaké příkazy má Discord bot umět?", "custom: přepsaný text otázky");
        Over(otCil.GetNapoveda(pCustom.TypProjektuKlic) == "Nápověda k botovi.", "custom: přepsaná nápověda");
        Over(otCil.GetVychoziPredpoklad(pCustom.TypProjektuKlic) == "Základní příkazy.", "custom: přepsaný předpoklad");

        var otTech = Otazky.Podle("tech-platforma");
        Over(otTech.GetText(pCustom.TypProjektuKlic) == otTech.GetText("Obecna"), "custom fallback: text otázky z Obecna");
        Over(otTech.GetVychoziPredpoklad(pCustom.TypProjektuKlic) == otTech.GetVychoziPredpoklad("Obecna"), "custom fallback: předpoklad z Obecna");

        var pCustomZmena = new SpecProjekt();
        SpecSluzba.ZmenTypProjektu(pCustomZmena, "discord-bot");
        Over(pCustomZmena.TypProjektuKlic == "discord-bot", "custom: změna typu projektu klíče");
        
        SpecSluzba.PouzijPredpoklad(pCustomZmena, "cil-problem");
        var odpCil = SpecSluzba.OdpovedNa(pCustomZmena, "cil-problem");
        Over(odpCil.Text == "Základní příkazy.", "custom: použitý předpoklad z vlastní šablony");

        Over(pCustomZmena.Log.Any(l => l.Akce == "Typ projektu" && l.Detail.Contains("Změna typu projektu z Obecná aplikace na Discord Bot")), "custom: správná zpráva v logu změn");

        SablonaSluzba.CustomSablony.Clear();

        // --- Testy dynamických otázek (v1.0) ---
        var pDyn = new SpecProjekt
        {
            Nazev = "Dynamicky projekt",
            Napad = "Dynamicky napad"
        };

        pDyn.Otazky.Add(new Otazka
        {
            Id = "dyn-q1",
            Sekce = "Technika",
            Dopad = Dopad.Vysoky,
            Text = "Otazka 1?",
            Napoveda = "Napoveda 1",
            VychoziPredpoklad = "Predpoklad 1"
        });
        pDyn.Otazky.Add(new Otazka
        {
            Id = "dyn-q2",
            Sekce = "Data",
            Dopad = Dopad.Stredni,
            Text = "Otazka 2?",
            Napoveda = "Napoveda 2",
            VychoziPredpoklad = "Predpoklad 2"
        });

        var dynOtazky = SpecSluzba.VratOtazkyProjektu(pDyn).ToList();
        Over(dynOtazky.Count == 2, "dynamic: projekt má přesně 2 otázky");
        Over(dynOtazky[0].Id == "dyn-q1", "dynamic: první otázka sedí");

        Over(SpecSluzba.DalsiNezodpovezena(pDyn).Id == "dyn-q1", "dynamic: další nezodpovězená je první");
        Over(SpecSluzba.PocetZodpovezenych(pDyn) == 0, "dynamic: 0 zodpovězených");

        SpecSluzba.Odpovez(pDyn, "dyn-q1", "Moje odpoved 1");
        Over(SpecSluzba.PocetZodpovezenych(pDyn) == 1, "dynamic: 1 zodpovězená");
        Over(SpecSluzba.DalsiNezodpovezena(pDyn).Id == "dyn-q2", "dynamic: další nezodpovězená je druhá");

        SpecSluzba.PouzijPredpoklad(pDyn, "dyn-q2");
        Over(SpecSluzba.PocetPredpokladu(pDyn) == 1, "dynamic: 1 předpoklad");
        Over(SpecSluzba.DalsiNezodpovezena(pDyn) == null, "dynamic: žádná další nezodpovězená");

        string mdDyn = SpecSluzba.RenderMarkdown(pDyn);
        Over(mdDyn.Contains("Otazka 1?"), "dynamic MD: obsahuje text první otázky");
        Over(mdDyn.Contains("Moje odpoved 1"), "dynamic MD: obsahuje odpověď na první");
        Over(mdDyn.Contains("Predpoklad 2"), "dynamic MD: obsahuje předpoklad na druhou");

        string tmpCestaDyn = Path.Combine(Path.GetTempPath(), "test_dyn.vcbrief");
        try
        {
            SpecSluzba.UlozProjekt(pDyn, tmpCestaDyn);
            var nactenyDyn = SpecSluzba.NactiProjekt(tmpCestaDyn);
            Over(nactenyDyn.Otazky.Count == 2, "dynamic roundtrip: počet uložených otázek sedí");
            Over(nactenyDyn.Otazky[0].Id == "dyn-q1", "dynamic roundtrip: ID první otázky sedí");
            Over(nactenyDyn.Otazky[1].Text == "Otazka 2?", "dynamic roundtrip: text druhé otázky sedí");
        }
        finally
        {
            if (File.Exists(tmpCestaDyn)) File.Delete(tmpCestaDyn);
        }

        // --- rychlé nápovědy odpovědí (quick options) ---
        var pQ = new SpecProjekt();
        var otQ1 = SpecSluzba.VratOtazkyProjektu(pQ).First(o => o.Id == "tech-platforma");
        var opts1 = otQ1.GetMoznosti(pQ.TypProjektuKlic);
        Over(opts1.Count == 3, "quick options: výchozí otázka má 3 možnosti");
        Over(opts1.Contains("Web (React + Node.js)"), "quick options: obsahuje správnou výchozí platformu");

        var pDynQ = new SpecProjekt();
        pDynQ.Otazky.Add(new Otazka { Id = "q-dyn", Text = "Spec q?", Moznosti = new List<string> { "Ano", "Ne", "Možná" } });
        var otDynQ = pDynQ.Otazky[0];
        Over(otDynQ.GetMoznosti(pDynQ.TypProjektuKlic).Count == 3, "quick options: dynamická otázka vrací své možnosti");

        string tmpCestaQ = Path.Combine(Path.GetTempPath(), "test_quick.vcbrief");
        try
        {
            SpecSluzba.UlozProjekt(pDynQ, tmpCestaQ);
            var nactenyQ = SpecSluzba.NactiProjekt(tmpCestaQ);
            Over(nactenyQ.Otazky[0].Moznosti.Count == 3, "quick options roundtrip: počet možností sedí");
            Over(nactenyQ.Otazky[0].Moznosti[2] == "Možná", "quick options roundtrip: hodnota možnosti sedí");
        }
        finally
        {
            if (File.Exists(tmpCestaQ)) File.Delete(tmpCestaQ);
        }

        // --- uživatelské příběhy (user stories) ---
        var pUS = new SpecProjekt();
        pUS.UserStories.Add(new UserStory
        {
            Id = "US-100",
            Titulek = "Test story",
            Popis = "Jako tester chci spustit test, abych overil kod.",
            Priorita = "Vysoká",
            Kriteria = new List<string> { "Kriterium 1", "Kriterium 2" }
        });

        string tmpCestaUS = Path.Combine(Path.GetTempPath(), "test_us.vcbrief");
        try
        {
            SpecSluzba.UlozProjekt(pUS, tmpCestaUS);
            var nactenyUS = SpecSluzba.NactiProjekt(tmpCestaUS);
            Over(nactenyUS.UserStories.Count == 1, "user stories roundtrip: počet stories sedí");
            Over(nactenyUS.UserStories[0].Id == "US-100", "user stories roundtrip: ID story sedí");
            Over(nactenyUS.UserStories[0].Titulek == "Test story", "user stories roundtrip: titulek story sedí");
            Over(nactenyUS.UserStories[0].Kriteria.Count == 2, "user stories roundtrip: počet kritérií sedí");
            Over(nactenyUS.UserStories[0].Kriteria[1] == "Kriterium 2", "user stories roundtrip: hodnota kritéria sedí");
        }
        finally
        {
            if (File.Exists(tmpCestaUS)) File.Delete(tmpCestaUS);
        }

        // --- chat history ---
        var pChat = new SpecProjekt();
        pChat.ChatHistory.Add(new ChatMessage { Role = "user", Text = "Dotaz 1", Cas = DateTime.Now });
        pChat.ChatHistory.Add(new ChatMessage { Role = "model", Text = "Odpoved 1", Cas = DateTime.Now });

        string tmpCestaChat = Path.Combine(Path.GetTempPath(), "test_chat.vcbrief");
        try
        {
            SpecSluzba.UlozProjekt(pChat, tmpCestaChat);
            var nactenyChat = SpecSluzba.NactiProjekt(tmpCestaChat);
            Over(nactenyChat.ChatHistory.Count == 2, "chat history roundtrip: počet zpráv sedí");
            Over(nactenyChat.ChatHistory[0].Role == "user", "chat history roundtrip: role první zprávy sedí");
            Over(nactenyChat.ChatHistory[1].Text == "Odpoved 1", "chat history roundtrip: text druhé zprávy sedí");
        }
        finally
        {
            if (File.Exists(tmpCestaChat)) File.Delete(tmpCestaChat);
        }

        // --- mockup image ---
        var pMockup = new SpecProjekt();
        pMockup.MockupNazev = "mockup.png";
        pMockup.MockupBase64 = "SGVsbG8gd29ybGQ="; // Base64 for "Hello world"

        string tmpCestaMockup = Path.Combine(Path.GetTempPath(), "test_mockup.vcbrief");
        try
        {
            SpecSluzba.UlozProjekt(pMockup, tmpCestaMockup);
            var nactenyMockup = SpecSluzba.NactiProjekt(tmpCestaMockup);
            Over(nactenyMockup.MockupNazev == "mockup.png", "mockup roundtrip: název mockupů sedí");
            Over(nactenyMockup.MockupBase64 == "SGVsbG8gd29ybGQ=", "mockup roundtrip: Base64 kódování sedí");
        }
        finally
        {
            if (File.Exists(tmpCestaMockup)) File.Delete(tmpCestaMockup);
        }

        // --- metrics ---
        var pMetriky = new SpecProjekt();
        pMetriky.Metriky = new ProjektMetriky
        {
            CasovyOdhadMin = "80",
            CasovyOdhadMax = "120",
            Komplexita = "Vysoká",
            SlozeniTymu = "1x dev",
            DoporucenyRozpocet = "100k",
            TechnickyRozbor = "arch",
            RizikaMetriky = new List<string> { "Risk 1" },
            CasVypoctu = DateTime.Now
        };

        string tmpCestaMetriky = Path.Combine(Path.GetTempPath(), "test_metriky.vcbrief");
        try
        {
            SpecSluzba.UlozProjekt(pMetriky, tmpCestaMetriky);
            var nactenyMetriky = SpecSluzba.NactiProjekt(tmpCestaMetriky);
            Over(nactenyMetriky.Metriky.CasovyOdhadMin == "80", "metrics roundtrip: odhad min sedí");
            Over(nactenyMetriky.Metriky.CasovyOdhadMax == "120", "metrics roundtrip: odhad max sedí");
            Over(nactenyMetriky.Metriky.Komplexita == "Vysoká", "metrics roundtrip: komplexita sedí");
            Over(nactenyMetriky.Metriky.RizikaMetriky.Count == 1, "metrics roundtrip: počet rizik sedí");
            Over(nactenyMetriky.Metriky.RizikaMetriky[0] == "Risk 1", "metrics roundtrip: detail rizika sedí");
        }
        finally
        {
            if (File.Exists(tmpCestaMetriky)) File.Delete(tmpCestaMetriky);
        }

        // --- HTML rendering ---
        var pHtml = new SpecProjekt { Nazev = "HTML Test Projekt" };
        string html = SpecSluzba.RenderHtml(pHtml);
        Over(!string.IsNullOrWhiteSpace(html), "HTML rendering: výstup není prázdný");
        Over(html.Contains("<!DOCTYPE html>"), "HTML rendering: obsahuje doctype");
        Over(html.Contains("HTML Test Projekt"), "HTML rendering: obsahuje název projektu");
        Over(html.Contains("toggleTheme"), "HTML rendering: obsahuje JS přepínač témat");

        Console.WriteLine();
        Console.WriteLine("VSECHNY TESTY OK (" + _ok + " kontrol)");
    }
}
// konec souboru
