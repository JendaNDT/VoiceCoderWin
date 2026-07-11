# CodePlanner – Komplexní finální audit (AUDIT1)

*Datum auditu: 11. 7. 2026 · Auditovaný stav: pracovní strom po commitu `90d30b5` (v2.0)*
*Rozsah: 6 833 řádků zdrojového kódu (CodePlanner + CoreTests), 3 paralelní hloubkové průchody (logika+stabilita, architektura+čistota, UX+UI). Kritické nálezy ověřeny druhým čtením přímo v kódu.*

---

## Shrnutí

Jádro aplikace je na vibecoding projekt nadprůměrně solidní: čistě oddělené od GUI, pokryté ~128 automatickými testy, s promyšlenou ochranou neuložené práce a poctivým escapováním výstupů. Hlavní rizika leží na švech mezi AI a zbytkem aplikace — **JSON export po AI analýze tiše ztrácí data, diktování v režimu „klikni-a-mluv" se nedá zastavit, klávesové zkratky obcházejí zámek UI během běžící analýzy a HTML export má jednu bezpečnostní díru (XSS)**. Nic z toho není architektonický průšvih — jsou to bodové chyby s malými opravami, ale všechny podkopávají důvěru přesně v momentech, kdy odevzdáváš výsledek práce. Doporučení: před jakoukoli další funkcí zafixovat 4 kritické nálezy, pak sladit verze a README (aktuálně existují 4 různá čísla verze najednou) a teprve potom pokračovat v rozvoji.

---

## 1. Vnitřní logika

### Silné stránky

- Datový tok nápad → otázky → specifikace → exporty → `.vcbrief` je konzistentní a persistence dělá plný roundtrip všech entit (odpovědi, dynamické otázky, user stories, chat, mockup, metriky) — ověřeno testy.
- Defenzivní parsování odpovědí Gemini: `TryGetProperty` řetězce, čištění markdown plotů (`CleanJson`), limity velikosti příloh (2 MB reference / 4 MB obrázek) a validace obrázků přes `Image.FromStream`.
- Systematické HTML escapování v HTML exportu (`WebUtility.HtmlEncode` na názvech, otázkách, odpovědích, metrikách) — až na jednu výjimku níže. RTF escapování včetně Unicode surrogátů je korektní (`MainForm.cs:1078–1091`).
- Determinismus vyřešen vědomě: explicitní formáty dat (`SpecCore.cs:674`), vlastní odstranění diakritiky kvůli `InvariantGlobalization` (`SpecCore.cs:1309–1325`), data v JSON jako ISO 8601.
- Dirty flag + potvrzení před ztrátou práce na všech vstupních bodech (Ctrl+N, otevření souboru, zavření okna).

### Nálezy

**KRITICKÉ**

- **[L1] JSON export ignoruje dynamické otázky z AI** — `SpecCore.cs:780`. `RenderJson` iteruje statickou desítku `Otazky.Vse` místo `VratOtazkyProjektu(p)`. Po AI analýze (vlajková funkce, která nahradí otázky sadou na míru) JSON export „pro kódovacího agenta" **tiše vypustí všechny odpovědi s AI-generovanými ID**. Markdown to dělá správně (`SpecCore.cs:721`), takže si rozdílu snadno nevšimneš — agent ale dostane torzo specifikace. Souvisí: `SpecCore.cs:784` používá enum overload `GetText(p.TypProjektu)`, takže custom šablony exportují výchozí texty otázek; `SpecCore.cs:758` počítá souhrn vždy proti 10 otázkám („8 / 10" i u projektu s 8 otázkami).
- **[L2] XSS / JS injection v HTML exportu přes ID user story** — `SpecCore.cs:992–993`. `id="story-{us.Id}"` a `onchange="toggleStory(this, 'story-{us.Id}')"` vkládají ID **bez HtmlEncode** (o dva řádky níž, na ř. 995, encodované je). ID pochází z odpovědi Gemini nebo z ručně upraveného/cizího `.vcbrief`. Škodlivá hodnota se v exportovaném webu spustí jako JavaScript — tedy: otevřeš cizí `.vcbrief`, vyexportuješ HTML, pošleš klientovi, a v jeho prohlížeči běží cizí kód.

**DŮLEŽITÉ**

- **[L3] Výsledek AI analýzy se aplikuje bez validace** — `MainForm.cs:1679–1708`. (a) Validní JSON s prázdným polem otázek nejdřív provede `Otazky.Clear(); Odpovedi.Clear()` — nenávratná ztráta všech odpovědí. (b) Duplicitní/prázdná ID z AI se nekontrolují → duplicitní záznamy, které se vzájemně stíní. (c) Výjimka uprostřed bloku nechá `_nacitani = true` → od té chvíle se tiše přestane propisovat dirty flag i změny názvu/nápadu. (d) Staré `UserStories` a `Metriky` se nemažou a popisují už neexistující specifikaci.
- **[L4] Odpovědi v sekci mimo 7 pevných sekcí tiše mizí ze všech exportů** — `SpecCore.cs:718–737, 922–941` (pevný seznam `PoradiSekci` na ř. 551–554). Prompt AI nabádá sekce dodržet, ale nevynucuje to. Otázka se sekcí „Bezpečnost" se zobrazí v seznamu, ale její odpověď se nevyrenderuje do specifikace, MD, HTML ani JSON — bez jakéhokoli varování.
- **[L5] PDF export může hlásit úspěch, i když PDF nevzniklo** — `MainForm.cs:2637–2651`. Vybere se první tiskárna obsahující „PDF" v názvu (může být PDFCreator, PDF-XChange… s jinou sémantikou `PrintToFile`); když žádná není, tisk jde přes dialog i na fyzickou tiskárnu a zvolená cesta ze SaveFileDialogu se ignoruje. V obou případech se ukáže „úspěšně exportováno do PDF" (`MainForm.cs:2294`) a nabídka otevřít soubor, který nemusí existovat.
- **[L6] Zacházení s API klíčem** — `GeminiService.cs:326` (a 434, 497, 597, 705, 792) + `GeminiService.cs:87–89`. Klíč se posílá v query stringu URL (běžně končí v logách proxy; navíc není URL-encodovaný — klíč s `&` rozbije request s matoucí chybou) a ukládá se v čitelném textu do `%APPDATA%\CodePlanner\settings.json`. Správně patří do hlavičky `x-goog-api-key` a šifrovaného úložiště (DPAPI).

**KOSMETICKÉ**

- **[L7]** Poškozený `sablony.json` / `settings.json` se tiše ignoruje (`SpecCore.cs:71–74`, `GeminiService.cs:68–71`) — uživateli beze slova zmizí šablony nebo API klíč.
- **[L8]** `PouzijPredpoklad` nekontroluje prázdný text (`SpecCore.cs:636–645`) — u AI otázky bez výchozího předpokladu vznikne prázdný záznam „**[PŘEDPOKLAD]**".
- **[L9]** CSV export neřeší formula injection (`MainForm.cs:3357–3365`) — pole začínající `=`, `+`, `-`, `@` se v Excelu spustí jako vzorec; stačí prefixovat apostrofem.
- **[L10]** Ručně upravený `.vcbrief` s `"odpovedi": null` skončí NRE se zavádějící hláškou (`SpecCore.cs:657–658`).
- **[L11]** Při selhání odeslání chatu zůstane nezodpovězená zpráva trvale v historii (a uloží se do projektu), zatímco napsaný text z inputu zmizí (`MainForm.cs:2354, 2361`).

---

## 2. Provázanost funkcí (architektura)

### Silné stránky

- **Správný směr závislostí**: UI (MainForm, SettingsForm) → Core (SpecCore, GeminiService, HlasovyVstup). Core nemá žádnou referenci na WinForms — důkazem je, že se kompiluje v CoreTests pod čistým `net8.0` i na Linuxu. Žádné kruhové závislosti.
- **CoreTests testují skutečný kód aplikace, ne kopii** — `CoreTests.csproj:12–13` linkuje reálné zdrojáky z `CodePlanner/Core/`.
- `KonzistencniKontrola` (`SpecCore.cs:1112–1399`) je vzorně izolovaná pravidlová vrstva s dokumentovaným záměrem („falešný poplach je přijatelný, mlčení ne").
- Žádné TODO/FIXME/HACK — kód není rozdělaný, je „jen" narostlý.

### Nálezy

**DŮLEŽITÉ**

- **[A1] God object: MainForm.cs obsahuje 6 tříd v jednom souboru** (3 726 ř.): MainForm (15–2422), ComboItemTypProjektu (2424), NalezyForm (2431–2615), PdfExporter (2617–2963), UserStoriesForm (2965–3366), MetrikyForm (3368–3725). MainForm sám dělá stavbu UI, event handling, file I/O, markdown→RTF konvertor, PDF orchestraci, stavový automat diktování, AI orchestraci i chat. Nejdelší metody: `PostavHlavniPlochu` 431 ř. (257–687), `Pd_PrintPage` 245 ř. (2661–2905), `RenderHtml` 267 ř. (`SpecCore.cs:812–1078`). Odhad: **~50 % (cca 1 850 řádků) lze přesunout** — ~35 % je mechanický přesun tříd do vlastních souborů bez rizika, zbytek přesun logiky do Core, kde by se dostala pod testy.
- **[A2] Duplicitní kód**: Gemini HTTP boilerplate 6× copy-paste (`GeminiService.cs:326, 434, 497, 597, 705, 792` — jedna sdílená metoda by ušetřila ~200 řádků); dva nezávislé markdown parsery (`MainForm.cs:981–1039` pro RTF vs. `2787–2818` pro PDF); detekce MIME mockupů 2× (`MainForm.cs:1676` vs. `GeminiService.cs:725`); paleta barev definovaná 7× (MainForm, SettingsForm, NalezyForm, PdfExporter, UserStoriesForm, MetrikyForm, HTML v SpecCore); porovnávání českých stringů priorit „Vysoká"/„Střední" na 4 místech; guard `IsDisposed || !Created` opsán 10×.
- **[A3] Mrtvý kód (ověřeno Grepem)**: `GeminiService.AnalyzujNapadAsync(TypProjektu)` overload + třídy `GeminiAnalizaVysledek` a `GeminiOdpoved` (`GeminiService.cs:107–134, 407–421`) nevolá nikdo; `PdfExporter.ZmerVyskuOdstavce` (`MainForm.cs:2938–2947`) nevolána; řada metod v Core žije už jen pro testy (`SpecSluzba.NastavNapad` — aplikace ho obchází, enum overloady `GetText`/`GetNapoveda`/…).
- **[A4] Věci visící ve vzduchu**: `CancellationToken` existuje ve všech 6 AI metodách, ale UI žádný nikdy nepředá (`MainForm.cs:1677, 1874, 2372, 2584, 3225, 3656`) — běžící request nejde zrušit; nálezy hloubkové AI kontroly se zobrazí v dialogu a zmizí (nejdou do logu, exportů ani do `.vcbrief`); custom šablony `sablony.json` jsou skrytá funkce (žádný vzor v repu, žádné UI, README mlčí); `ChatMessage.Cas` se ukládá, ale nikde nezobrazuje.
- **[A5] Logika verzování existuje na 3 místech** — `SpecCore.cs:648` (`Zmena`), `MainForm.cs:446–453` (ručně opakuje `NastavNapad`), `MainForm.cs:1710–1717` (AI analýza). Navíc nekonzistentní auditní stopa: přílohy/mockup logují, ale nezvyšují verzi (`MainForm.cs:1996, 2145`); změna názvu projektu neloguje ani neverzuje (310–317). Princip „každé rozhodnutí zvyšuje verzi" tedy neplatí.

**KOSMETICKÉ**

- **[A6]** Duální reprezentace typu projektu — enum `TypProjektu` + string `TypProjektuKlic` se ručně synchronizují (`SpecCore.cs:594–601`); enum už slouží jen zpětné kompatibilitě a mrtvým overloadům.

---

## 3. Stabilita

### Silné stránky

- Korektní async vzor bez deadlocků: nikde `.Result`/`.Wait()`, po každém `await` kontrola `IsDisposed`/`Created` (např. `MainForm.cs:1876, 2374, 3226`), UI se aktualizuje jen z UI vlákna.
- `HttpClient` je správně jediná statická instance (`GeminiService.cs:222–228`).
- Základní ochrana proti dvojkliku: busy stav, `_chatBusy` flag, potvrzovací dialogy před destruktivními akcemi.

### Nálezy

**KRITICKÉ**

- **[S1] Diktování v režimu „klikni-a-mluv" nejde nikdy zastavit** — `MainForm.cs:59, 1795, 1803, 1814–1817`. Rychlý klik (<400 ms) nastaví `_diktovaniClickToggle = true`, ale flag se **nikde v kódu neresetuje**: `MouseUp` se v toggle režimu okamžitě vrací (ř. 1795), `Click` handler je prázdný (ř. 1814–1817) a další `MouseDown` (ř. 1758) jen znovu spustí nahrávání. Neexistuje cesta k `ZastavADiktuj`. Důsledek: mikrofon nahrává donekonečna (soukromí + rostoucí buffer v paměti), přepis nikdy neproběhne, a protože flag zůstane `true`, je **celé diktování rozbité až do restartu aplikace** — včetně hold-to-talk. *(Ověřeno druhým čtením.)*
- **[S2] Klávesové zkratky obcházejí zámek UI během AI analýzy** — `MainForm.cs:129–156` vs. `1737–1756`. `NastavitStavBusy` vypne tlačítka a pole, ale `ProcessCmdKey` běží dál. Ctrl+N během 30s analýzy založí nový projekt a callback analýzy pak zapíše výsledky do **nového prázdného projektu** — ztráta rozpracovaného projektu + cizí data v čistém. *(Ověřeno: v `ProcessCmdKey` není žádný busy guard.)*

**DŮLEŽITÉ**

- **[S3] `NastavitStavBusy` nezamyká všechno** — `MainForm.cs:1737–1756`. Během AI requestu zůstávají aktivní `cmbTyp` (změna šablony přepisuje odpovědi souběžně s čekající analýzou), obě diktovací tlačítka (druhý souběžný async request), přílohy a mockupy. Chat busy naopak zamyká jen chat — analýza a chat můžou běžet souběžně nad týmž projektem a `finally` v `OdeslatChat` (2388–2399) bezpodmínečně odemkne vstupy.
- **[S4] Únik GDI objektů** — `MainForm.cs:1418` (`pnlQuickOptions.Controls.Clear()` nedisposuje odstraněné buttony s fonty a handlery), `2318–2333` (`VykresliHistoriiChatu` vytváří 2 nové `Font` objekty na každou zprávu při každém překreslení — a volá se při každé změně projektu), `3185–3209` (detail user story totéž). Při delší práci roste počet GDI handles → artefakty vykreslování až pád (limit 10 000/proces).
- **[S5] Timeout 30 s je pro reálné požadavky málo** — `GeminiService.cs:227`. Analýza s 2 MB referencí + 4 MB mockupu na `gemini-2.5-pro` běžně přesáhne 30 s; `TaskCanceledException` se uživateli ukáže jako anglické „A task was canceled" bez vysvětlení.

**KOSMETICKÉ**

- **[S6]** `HlasovyVstup.cs:13, 33–35` — `mciSendStringA` (ANSI) selže na temp cestě se znaky mimo systémovou kódovou stránku; návratové kódy `set` příkazů se ignorují, takže odhad délky nahrávky 32 000 B/s (`MainForm.cs:1852`) nemusí sedět.
- **[S7]** Hledání v náhledu překreslí celou specifikaci při každém úhozu, včetně konzistenční kontroly a vloženého referenčního textu (`MainForm.cs:1512–1532`, `SpecCore.cs:702–709`) — lag u velkých projektů. Konzistenční kontrola navíc běží 2× při každém překreslení (`SpecCore.cs:748` + `MainForm.cs:1536`).
- **[S8]** Case-sensitive detekce MIME: `.JPG` se pošle jako `image/png` (`MainForm.cs:1676`, `GeminiService.cs:725`).
- **[S9]** Selhání zápisu `settings.json` při odebírání nedávného projektu propadne do neošetřeného event handleru (`MainForm.cs:1146–1148`, `GeminiService.cs:91–94`).

---

## 4. Čistota a přehlednost kódu

### Silné stránky

- Dobré XML-doc komentáře v Core s odkazy na kapitoly návrhového PDF (kap. 7, 11, 18) — kód se dá číst spolu s návrhem.
- Pojmenované barevné konstanty v hlavním okně (Navy, Teal, Oranzova…) — základ designového systému existuje.
- `.gitignore` je promyšlený (build výstupy, ZIPy, token) a `.github_token` skutečně není trackovaný v gitu.

### Nálezy

**DŮLEŽITÉ**

- **[C1] Čtyři různé verze aplikace najednou**: `CodePlanner.csproj:12` → 2.0.0, toolbar `MainForm.cs:211` → „v1.7", titulek okna `MainForm.cs:1606` → „v1.2", JSON export `SpecCore.cs:792` → `verzeNastroje: "0.9.0"`. Uživatel vidí v jednom okně v1.7 i v1.2 současně. Navíc si protiřečí exporty: Markdown tvrdí „*demonstrátor bez AI*" (`SpecCore.cs:691`), PDF titulka říká „CodePlanner (AI-Powered)" (`MainForm.cs:2736`).
- **[C2] README popisuje o ~10 verzí starší aplikaci** — `README.md:7` („Bez AI, plně offline") vs. realita s Gemini analýzou, chatem a diktováním přes API; novinky končí v0.3; „36 kontrol" vs. reálných ~128; tabulka struktury nezná GeminiService.cs, HlasovyVstup.cs ani SettingsForm.cs; roadmapa slibuje „napojení na Claude API", implementované je Gemini. Nový uživatel z README nepozná, že potřebuje API klíč.
- **[C3] Testovací harness je křehký** — `CoreTests/Program.cs:12–17`: vlastní `Over()` s výjimkou znamená, že první selhání zabije celý běh (neuvidíš úplný obraz); testy zapisují do **reálného** uživatelského `settings.json` (ř. 156–225 — záloha je ve `finally`, ale kill procesu uprostřed testu poškodí nastavení); linkování souborů místo project reference znamená, že nový Core soubor se do testů automaticky nedostane.

**KOSMETICKÉ**

- **[C4]** Jazyková směska v identifikátorech: čeština (`SpecSluzba`, `Otazka`) × angličtina (`UserStory`, `ChatMessage`) × slovenština (`btnReferencie`, `MainForm.cs:46`) × překlep `GeminiAnalizaVysledek` (`GeminiService.cs:112`).
- **[C5]** Magická čísla bez konstant: 2 MB limit (`MainForm.cs:1981`), 4 MB (`2122`), 32 000 B/s + 44 B WAV hlavička (`1852`), 400 ms práh hold/toggle (`1800`), 500 ms debounce (`80`), max 5 nedávných (`GeminiService.cs:35`), zkracování názvů na 20/17 znaků duplikované 2× (`1948, 2080`).
- **[C6]** Stavy otázek jako znaky `'○' '≈' '✔'` v `List<char>` (`MainForm.cs:36`) místo enumu; placeholder textboxů detekovaný přes `ForeColor == Color.Gray` (`610, 2344`) — křehké triky.
- **[C7]** Nekonzistentní BOM: MD/JSON export s BOM (`1228`), HTML bez (`1253`), CSV bez (`3329`) — Excel s českou diakritikou bez BOM zlobí.
- **[C8]** Root repa: v gitu trackované `AUDIT.md`, `FINAL_REPORT.md` a 145kB návrhové PDF; na disku navíc 60MB ZIP + rozbalená release složka. `.github_token` leží na disku hned vedle zdrojáků — je v .gitignore, ale hrozí omylem přibalení do ZIPu.
- **[C9]** Seznam modelů natvrdo vč. zastaralých 1.5 (`SettingsForm.cs:108–112`), default `"gemini-2.5-flash"` duplikovaný (`GeminiService.cs:20`); `// konec souboru` šum (`SpecCore.cs:1401`, `CoreTests/Program.cs:505`); nekonzistentní tykání/vykání v hláškách („Napiš odpověď…" `1345` vs. „Otevřete prosím Nastavení" `1647`).

### Pokrytí testy

CoreTests pokrývají dobře: stavovou logiku jádra, verzování, roundtrip persistence, všech 11 konzistenčních pravidel, custom šablony, nastavení. **Nepokrývají**: parsování odpovědí Gemini (nejrizikovější místo — ani jeden test s mock odpovědí), `RenderJson` s dynamickými otázkami (test by okamžitě odhalil L1), escapování nepřátelských vstupů v HTML (odhalilo by L2), chybové cesty (poškozené soubory) a celou UI vrstvu (3 726 řádků MainForm, PdfExporter, diktování — včetně rozbitého S1).

---

## 5. UX

### Silné stránky

- Důsledná ochrana neuložené práce: hvězdička v titulku, Yes/No/Cancel při zavření, Ctrl+N i otevření (`MainForm.cs:118–121, 1611–1620`); chybějící soubor v „nedávných" se nabídne k odebrání.
- Dobrá zpětná vazba po exportech: potvrzení s celou cestou, u PDF/HTML nabídka „ihned otevřít" (`MainForm.cs:1230, 1256–1266`).
- Promyšlené empty staty — prázdný chat, User Stories i metriky říkají, co udělat dál (`MainForm.cs:2320, 3166, 3603–3609`).
- Konzistentní klávesové konvence: Ctrl+N/O/S/M/J/P, Ctrl+Enter, Enter/Shift+Enter v chatu (`MainForm.cs:129–156, 625–632, 777`).

### Nálezy

**DŮLEŽITÉ**

- **[U1] Chybové hlášky AI nejsou pro neprogramátory** — `GeminiService.cs:368` (a 469, 549, 655, 763, 848): do MessageBoxu jde surový anglický JSON od Googlu („Detail: {errContent}"). Chybí překlad běžných stavů (špatný klíč, překročený limit, výpadek sítě). Timeout se ukáže jako „A task was canceled" (`MainForm.cs:1728`). Laik netuší, co má udělat.
- **[U2] Čekání na AI nejde zrušit a nemá průběh** — `MainForm.cs:1737–1756`: UI se až na 30 s zamkne bez tlačítka Zrušit a bez indikace průběhu. `CancellationToken` v GeminiService přitom existuje (viz A4), jen se nevyužívá.
- **[U3] Onboarding API klíče** — `MainForm.cs:1646–1649`, `SettingsForm.cs:67`: hláška „Není nastaven API klíč" otevře dialog, kde je jen pole — žádný odkaz na Google AI Studio, žádné „Otestovat připojení". Špatný klíč se pozná až pádem první analýzy (viz U1).
- **[U4] HTML export není obsahově rovnocenný** — `SpecCore.cs:922–941`: chybí Otevřené otázky, Kontrola konzistence a Log rozhodnutí — klient ve „webové" verzi nevidí rizika ani nedořešené body, přestože v MD exportu jsou.
- **[U5] Hledání v náhledu ničí kontext** — `MainForm.cs:1512–1532`: každý úhoz přerenderuje celý dokument, scroll skočí na začátek a na první nález se neposkočí — jen se obarví někde mimo viewport.
- **[U6] Opakovaná AI kontrola konzistence duplikuje nálezy** — `MainForm.cs:2556`: `Items.Clear()` se volá jen pro offline kontrolu (`if (!isAi)`), druhé spuštění AI kontroly přidá tytéž řádky podruhé.
- **[U7] Přístupnost** — NalezyForm, UserStoriesForm ani MetrikyForm nenastavují `CancelButton` → Esc je nezavře; chybí mnemoniky (&) a `AccessibleName`; owner-drawn seznam otázek je pro čtečky obrazovky jen holý text bez stavu.
- **[U8] Nejednotná terminologie a oslovování** — tentýž objekt je „podklad" (`MainForm.cs:381`), „příloha" (`395, 2000`) i „referenční soubor" (`2005`); obrázek je „skica", „nákres rozhraní (mockup)" i „vizuální mockup" (`401, 2109, 2149`). Toolbar doporučuje diktování Win+H (`205`), zatímco vedle jsou 🎤 tlačítka diktující přes Gemini — dvě konkurenční cesty bez vysvětlení rozdílu.

**KOSMETICKÉ**

- **[U9]** Rychlé volby („Tip:") ukládají odpověď okamžitě po kliknutí bez potvrzení a aplikace nemá Undo (`MainForm.cs:1447–1452`) — překlep myší znamená ruční opravu.
- **[U10]** Úspěšné akce hlásí modální MessageBox („Analýza dokončena", „Zkopírováno" — `1723, 3240, 3677, 3718`) — zbytečné klikání na šťastné cestě, stavový řádek by stačil.
- **[U11]** Každé uložení odpovědi překreslí i chat a odscrolluje ho na konec (`1467, 2306`) — čteš-li starší odpověď asistenta, o pozici přijdeš.
- **[U12]** Nápověda „výšku upravíš tažením horního okraje" je zabudovaná do titulku GroupBoxu (`233`) — splitter je jinak neviditelný (barva = pozadí).

---

## 6. UI

### Silné stránky

- Konzistentní značka Navy+Teal napříč hlavním oknem, dialogy, PDF i HTML exportem.
- Stavové odznaky otázek kombinují tvar + barvu (✔/P/○) — fungují i pro barvoslepé (`MainForm.cs:893–923`).
- HTML export má viewport, breakpoint 900 px, tmavý režim a fulltextový filtr (`SpecCore.cs:822–1073`).

### Nálezy

**DŮLEŽITÉ**

- **[I1] Smíšené DPI strategie → rozbité škálování** — MainForm má `AutoScaleMode.Dpi` (`MainForm.cs:94`), všechny dialogy `AutoScaleMode.Font` (`SettingsForm.cs:35`, `MainForm.cs:2451, 2991, 3397`). MetrikyForm navíc ručně násobí velikost fontu `DeviceDpi/96` (`3453, 3476, 3484`) — body se ale škálují samy, takže na 150% monitoru budou popisky zvětšené dvakrát. Absolutní výšky řádků layoutu (nápad 100 px, odpověď 78 px — `709–712, 280`) se s DPI neškálují.
- **[I2] MinimumSize 1100×720** (`MainForm.cs:90`) — po 125% škálování na běžném notebooku (1366×768) je minimální okno větší než obrazovka; log a status bar zajedou mimo displej.
- **[I3] Kontrasty pod WCAG AA**: verze v Color.Silver na bílé ~1,7:1 (`MainForm.cs:214`), placeholder Color.Gray ~3,9:1 (`506, 606`), štítek dopadu „S" DimGray na Gainsboro hraniční (`927–933`). Norma je 4,5:1.
- **[I4] Pevné šířky ořezávají obsah**: hlavička „Živá specifikace · verze X · zodpovězeno Y/Z" v Labelu 250 px se ořízne bez výpustky (`490, 1506`); patička User Stories má 580 px, ale tlačítka potřebují ~610+ px a `WrapContents = false` → „Zavřít" se ořízne (`3101`).
- **[I5] PDF vykresluje syrový Markdown** — `MainForm.cs:2787–2818`: řádek s jedním `#` nespadá do žádné větve → v PDF je vidět mřížka; `**tučné**` se nestripuje → klientský dokument obsahuje hvězdičky. Je to výstup, který se posílá ven — na hraně KRITICKÉ.

**KOSMETICKÉ**

- **[I6]** Paleta se postupným přidáváním rozjela: ~35 unikátních barev ve WinForms + ~19 v HTML; téměř duplicitní dvojice (pozadí 246,248,250 vs. 245,247,250; oranžová 230,140,0 vs. 217,119,6; text varování 146,90,4 vs. 180,110,0); akcent HTML `#149689` ≠ aplikační Teal `#17B0A0`.
- **[I7]** ~14 unikátních velikostí písma bez typografické škály (8; 8,5; 9; 9,25; 9,5; 9,75; 10; 10,5; …) — rozdíly 9/9,25/9,5 jsou okem nerozlišitelné a jen komplikují údržbu. „Segoe UI Semibold" + `FontStyle.Bold` = dvojité ztučnění (`2327, 3018, 3185, 3426`).
- **[I8]** `Padding` na RichTextBoxu WinForms ignoruje → text nalepený na hranu (`3137, 3515`); `Margin` u dokovaného tlačítka se neuplatní (`3575`).
- **[I9]** Emoji jako ikony toolbaru (`182–202`) — vzhled závisí na systému; při min. šířce se 10 položek vejde těsně a tip „Win+H" padá do overflow.
- **[I10]** HTML export: tmavý režim se nepamatuje a nerespektuje `prefers-color-scheme` (`SpecCore.cs:1015–1021`); font Inter z CDN → offline fallback (`823`); absolutně pozicovaný přepínač tématu se na mobilu překryje s dlouhým H1 (`852`).
- **[I11]** Kódový plot ``` ``` ``` z referenčních podkladů se v RTF náhledu zobrazí doslovně — renderer ho nezná (`SpecCore.cs:705–707`, `MainForm.cs:989–1035`).

---

## Doporučené další kroky

**1. Okamžitě (než se pošle komukoli dalšímu) — 4 kritické opravy, každá malá:**

1. **L1** — v `RenderJson` zaměnit `Otazky.Vse` za `VratOtazkyProjektu(p)` a `GetText(p.TypProjektu)` za variantu s `TypProjektuKlic` (+ opravit jmenovatel souhrnu na ř. 758). Přidat test „JSON export po AI analýze obsahuje všechny odpovědi".
2. **L2** — obalit `us.Id` v `SpecCore.cs:992–993` HtmlEncode (a pro jistotu whitelistovat ID na alfanumerické znaky). Přidat test s nepřátelským ID.
3. **S1** — v `BtnDiktovat_Click` obsloužit toggle režim: když `_diktovaniClickToggle == true`, resetovat flag a zavolat `ZastavADiktuj`.
4. **S2** — na začátek `ProcessCmdKey` přidat guard: když běží AI operace, zkratky (kromě Esc) ignorovat.

**2. Druhá vlna (stabilita a důvěryhodnost výstupů):** validace AI výsledku před `Clear()` (L3) + reset `_nacitani` ve `finally`; dozamknout `cmbTyp`, diktování a přílohy v busy stavu (S3); opravit GDI úniky — cachovat fonty, disposovat odstraněné controly (S4); srozumitelné české chybové hlášky + delší timeout / tlačítko Zrušit s využitím existujícího CancellationTokenu (U1, U2, S5); PDF export omezit na „Microsoft Print to PDF" a po tisku ověřit existenci souboru (L5, I5).

**3. Třetí vlna (konzistence navenek):** jedno místo pravdy pro verzi — číst z csproj/assembly za běhu a propsat do toolbaru, titulku i JSON exportu (C1); přepsat README podle reality v2.0 (C2); doplnit HTML export o Otevřené otázky, konzistenci a log (U4); sekce mimo `PoradiSekci` renderovat do bloku „Ostatní" místo tichého zahození (L4).

**4. Odložit (nespěchá, ale stojí za to):** refaktor MainForm — přesun 4 tříd do vlastních souborů a logiky do Core pod testy (A1, A5); sjednocení palety a typografické škály (I6, I7); DPI úklid (I1, I2); API klíč do hlavičky + DPAPI (L6); sjednocení terminologie a tykání (U8, C9); úklid mrtvého kódu (A3).

**Pravidlo pro příště:** každá funkce, která bere výstup z AI a zapisuje ho do projektu nebo do exportu, by měla mít (a) validaci vstupu a (b) test s rozbitým/nepřátelským vstupem. Tři ze čtyř kritických nálezů vznikly přesně tady.

---

## Otevřené otázky pro Jendu

1. **JSON export** — je pořád hlavním výstupem „pro kódovacího agenta"? Pokud ano, oprava L1 je priorita č. 1. Pokud už reálně používáš spíš MD/HTML, dej vědět a priority se přeskládají.
2. **PDF export** — stačí ti spolehlivá podpora jen přes „Microsoft Print to PDF" (je na každých Windows 10/11), nebo chceš do budoucna vlastní PDF generátor nezávislý na tiskárnách (víc práce, ale plná kontrola)?
3. **Komu posíláš HTML exporty?** Pokud je posíláš klientům / otevíráš cizí `.vcbrief` soubory, oprava XSS (L2) a CSV apostrofu (L9) je nutnost. Pokud jen pro sebe, je to méně urgentní — ale opravit doporučuji tak jako tak.
4. **Verze** — má být oficiální číslo 2.0 (podle csproj a ZIPu)? Sjednotím pak všechny 4 výskyty na jedno místo pravdy.
5. **`.github_token`** — leží ve složce projektu vedle zdrojáků. Doporučuji přesunout mimo projekt (např. do `%APPDATA%`), ať se omylem nepřibalí do ZIP balíčku.

---

*Audit provedly 3 paralelní hloubkové průchody kódem (celkem ~600 tis. tokenů analýzy); kritické nálezy L1, L2, S1 a S2 byly ověřeny nezávislým druhým čtením zdrojových souborů. Aplikace nebyla spouštěna — UX/UI nálezy vycházejí z kódu, ne z živého testování.*

