# Komplexní audit aplikace CodePlanner

Tento audit slouží jako podrobný přehled stavu aplikace **CodePlanner** (C# / .NET 8 / Windows Forms) před rozhodnutím o jejím dalším vývoji. Cílem je poskytnout Jendovi jasný, čitelný a upřímný pohled na to, kde je aplikace hotová, kde leží její největší technologická rizika a jaké kroky by měly následovat.

---

## 📋 Krátké shrnutí

CodePlanner je ve velmi dobrém stavu z pohledu **funkčnosti a konceptu** – integrace s Gemini API, hlasový vstup (Push-to-Talk) a generování strukturované specifikace s exporty fungují spolehlivě a plní účel MVP. Největším rizikem aplikace je **stabilitní a architektonická stránka**; kvůli zbrklému vývoji (vibecodingu) se v kódu nacházejí kritické chyby, které mohou způsobit pád aplikace (zejména chybějící ošetření uvolnění objektů po asynchronních operacích a nepovolené znaky v názvech souborů), a kód trpí silnou provázaností (obří monolitické soubory). Pro další rozvoj je klíčové stabilizovat asynchronní chování, oddělit UI od logiky a opravit vykreslování na monitorech s vysokým rozlišením (DPI).

---

## 1. Vnitřní logika
*Datové toky, stavový management, hraniční případy, validace vstupů, error handling.*

### Silné stránky
- Model specifikace se 7 bloky je logicky oddělen a funguje jako solidní jádro aplikace.
- Šablony otázek a dynamické doplňování předpokladů fungují spolehlivě.
- Sémantická kontrola specifikace přes Gemini API dává výborné a relevantní výsledky.

### Nálezy

#### 🔴 Kritické
- **ObjectDisposedException při zavření oken během AI operací**
  - **Soubor:** [MainForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/MainForm.cs#L2485) (`NalezyForm.BtnAiCheck_Click` řádky 2485–2518, `UserStoriesForm.BtnAiStories_Click` řádky 3078–3114, `MetrikyForm.BtnAiMetriky_Click` řádky 3500–3542)
  - **Dopad:** Metody jsou typu `async void` a spouštějí API volání. Pokud uživatel zavře modální dialog dříve, než Gemini odpoví, pokus o zápis do již uvolněných (disposed) ovládacích prvků způsobí okamžitý pád celé aplikace.
  - **Řešení:** Po každém `await` ověřit stav okna: `if (this.IsDisposed || !this.Created) return;`.

#### 🟡 Důležité
- **Křehké zpracování JSON odpovědí z Gemini API**
  - **Soubor:** [GeminiService.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/Core/GeminiService.cs) (všechna API volání)
  - **Dopad:** Kód přistupuje k prvkům JSON přímo přes `parts[0].GetProperty("text").GetString()`. Pokud API vrátí prázdnou odpověď nebo zablokuje obsah kvůli filtrům, aplikace spadne na `KeyNotFoundException`.
  - **Řešení:** Použít bezpečnější parsování s `TryGetProperty` a čistit markdown obaly robustnějším způsobem.
- **Absence limitu velikosti u referenčních souborů**
  - **Soubor:** [MainForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/MainForm.cs#L1938) (`NahratReferenci` řádky 1938–1973)
  - **Dopad:** Při nahrávání referenčního souboru se nekontroluje jeho velikost. Nahrání obřího souboru (např. 200 MB logu) způsobí `OutOfMemoryException`.
  - **Řešení:** Zavést pevný limit na velikost textové reference (např. max 2 MB).
- **Chybějící validace formátu nahraných skic (mockupů)**
  - **Soubor:** [MainForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/MainForm.cs#L2066) (`NahratMockup` řádky 2066–2107)
  - **Dopad:** Uživatel může nahrát jakýkoliv binární soubor (nejen obrázek). Ten se zakóduje do Base64 a při pokusu o zobrazení nebo odeslání do Gemini API způsobí pád.
  - **Řešení:** Ověřit validitu obrázku pokusem o jeho načtení do paměti (`Image.FromStream`) před uložením.

#### 🟢 Kosmetické
- **Riziko zablokování UI příznakem `_nacitani` při výjimce**
  - **Soubor:** [MainForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/MainForm.cs#L1086) (`NovyProjekt`, `OtevritProjektCestu`, `BtnAiAnalyza_Click`)
  - **Dopad:** Příznak `_nacitani` blokuje eventy při programovém plnění prvků. Pokud uprostřed bloku nastane chyba, flag zůstane nastaven na `true` a aplikace přestane reagovat na uživatelské změny.
  - **Řešení:** Zabalit plnění do `try-finally` a vypínat příznak ve `finally` bloku.
- **Tiché maskování chyb při načítání šablon**
  - **Soubor:** [SpecCore.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/Core/SpecCore.cs#L71) (`SablonaSluzba.NactiCustomSablony` řádek 71)
  - **Dopad:** Při chybě v `sablony.json` (např. špatná syntaxe JSON) se výjimka tiše zachytí a ignoruje. Uživatel se nedozví, proč se šablony nenačetly.
  - **Řešení:** Zobrazit uživateli/vývojáři varování nebo chybu zalogovat.

---

## 2. Provázanost funkcí
*Komunikace modulů, cyklické závislosti, duplicity, uvíznuté prostředky.*

### Silné stránky
- Jasně definovaný a přímočarý tok dat od nápadu přes otázky ke specifikaci a backlogu.
- Verzovací log spolehlivě dokumentuje historii změn specifikace.

### Nálezy

#### 🔴 Kritické
- **Riziko pádu aplikace při exportu z UserStoriesForm**
  - **Soubor:** [MainForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/MainForm.cs#L3122) (řádky 3122 a 3146)
  - **Dopad:** Kód se pokouší exportovat Markdown/CSV s názvem souboru odvozeným přímo z `_projekt.Nazev.Replace(" ", "_")`. Pokud název projektu obsahuje nepovolené znaky (např. `/`, `\`, `:`), dialog pro uložení selže a aplikace spadne.
  - **Řešení:** Použít stávající metodu `BezpecnyNazevSouboru` k odstranění neplatných znaků.

#### 🟡 Důležité
- **Obří monolitické soubory (porušení SRP)**
  - **Soubor:** [MainForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/MainForm.cs) (3575 řádků) a [SpecCore.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/Core/SpecCore.cs) (1373 řádků)
  - **Dopad:** `MainForm.cs` obsahuje čtyři samostatné formuláře a tiskový exportér. `SpecCore.cs` obsahuje 15+ různých datových modelů a pomocných tříd. Kód je extrémně nepřehledný a těžko udržovatelný.
  - **Řešení:** Rozdělit soubory tak, aby platilo pravidlo "jedna třída = jeden soubor".
- **Průnik prezentační (UI) logiky do jádra (Core)**
  - **Soubor:** [SpecCore.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/Core/SpecCore.cs#L812) (`SpecSluzba.RenderHtml`)
  - **Dopad:** Core služba přímo generuje kompletní HTML stránku včetně inline CSS témat a JavaScriptu pro dark mode. Jádro by mělo být čistě doménové.
  - **Řešení:** Přesunout generování HTML do samostatného exportního modulu mimo složku `Core`.

#### 🟢 Kosmetické
- **Čtyřnásobná duplicita kódu pro očištění JSON**
  - **Soubor:** [GeminiService.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/Core/GeminiService.cs) (řádky 371, 542, 645, 828)
  - **Dopad:** Logika pro ořezání ` ```json ` tagů z odpovědí Gemini je zkopírována čtyřikrát na různých místech.
  - **Řešení:** Vytvořit jednu privátní pomocnou metodu `CleanJson` v `GeminiService`.

---

## 3. Stabilita
*Race conditions, asynchronní chování, paměťové úniky, chování při netypických vstupech, determinismus.*

### Silné stránky
- Nezávislost na externích NuGet knihovnách zjednodušuje build a minimalizuje konflikty verzí.
- 128 automatických testů jádra garantuje správnost chování datového modelu.

### Nálezy

#### 🔴 Kritické
- **Riziko přepsání dat při souběžných akcích (Ctrl+O během AI analýzy)**
  - **Soubor:** [MainForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/MainForm.cs#L1611) (`BtnAiAnalyza_Click`)
  - **Dopad:** Během spuštěné AI analýzy na pozadí není zablokován toolbar ani klávesové zkratky. Uživatel může stisknout `Ctrl+O` a načíst jiný projekt. Jakmile AI analýza doběhne, přepíše nově načtená data výsledky staré analýzy, což vede k poškození dat.
  - **Řešení:** Během asynchronních operací dočasně zakázat klávesové zkratky a celý hlavní formulář (`Enabled = false`).
- **Race Condition v odesílání zpráv chatu**
  - **Soubor:** [MainForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/MainForm.cs#L2283) (`OdeslatChat`)
  - **Dopad:** Pokud uživatel rychle stiskne Enter v chatu několikrát za sebou, metoda se spustí souběžně a paralelně modifikuje vláknově nebezpečný `List<ChatMessage>`, což může poškodit data v paměti a vyvolat duplicitní API dotazy.
  - **Řešení:** Zavést stavovou proměnnou (např. `_chatBusy`) a blokovat vstup po dobu čekání na API.

#### 🟡 Důležité
- **Únik paměti a systémových prostředků v PDF exportu**
  - **Soubor:** [MainForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/MainForm.cs#L2537) (`PdfExporter.Export`)
  - **Dopad:** Instance `PrintDocument` alokuje GDI kontexty v systému Windows, ale není uvolňována pomocí `Dispose()`.
  - **Řešení:** Zabalit instanci `PrintDocument` do bloku `using`.
- **Únik paměti a GDI handlů při prohlížení mockupů**
  - **Soubor:** [MainForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/MainForm.cs#L2109) (`ZobrazitMockup`)
  - **Dopad:** Dialog `dlg` se zobrazuje přes `ShowDialog`, ale po zavření se nevolá `Dispose()`. Stejně tak se neuvolňuje GDI objekt `Image` načtený z Base64 streamu. Opakované prohlížení mockupů vede k postupnému zaplňování GDI paměti.
  - **Řešení:** Použít `using` blok pro formulář `dlg` a obrázek `img` po zavření okna explicitně uvolnit.
- **Race conditions a kolize při nahrávání audia (MCI)**
  - **Soubor:** [HlasovyVstup.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/Core/HlasovyVstup.cs) (celý soubor)
  - **Dopad:** Statický stav `_nahravam` je přistupován bez synchronizačních zámků (`lock`). Nahrávka se ukládá do jediné pevně definované cesty `voice_input.wav` v tempu. Souběžné nahrávání z více oken selže. Návratové kódy z `mciSendString` jsou ignorovány, což vede k tichému selhání nahrávání při chybějícím mikrofonu.
  - **Řešení:** Použít zámek na stav nahrávání, generovat unikátní názvy souborů (např. pomocí GUID) a kontrolovat návratové kódy z MCI driveru.

#### 🟢 Kosmetické
- **Chybějící timeouty a stornování u HttpClient**
  - **Soubor:** [GeminiService.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/Core/GeminiService.cs#L221)
  - **Dopad:** Statický `HttpClient` nemá nastavený timeout. Pokud se síťové připojení zasekne, aplikace může viset až 100 sekund bez jakékoliv reakce.
  - **Řešení:** Nastavit timeout (např. 30s) a propagovat `CancellationToken` do asynchronních metod.

---

## 4. Čistota a přehlednost kódu
*Struktura repozitáře, pojmenování, dead code, magická čísla, konzistence stylu, dokumentace, TODO/FIXME.*

### Silné stránky
- Kód je velmi dobře okomentován a vysvětluje byznys logiku i P/Invoke volání.
- Struktura projektu je minimalistická a snadno se kompiluje.

### Nálezy

#### 🟡 Důležité
- **Tiché potlačování výjimek bez jakéhokoliv logování**
  - **Soubory:** [SpecCore.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/Core/SpecCore.cs#L71), [HlasovyVstup.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/Core/HlasovyVstup.cs#L45)
  - **Dopad:** Použití prázdných bloků `catch {}` ztěžuje diagnostiku chyb při selhání nahrávání nebo parsování šablon.
  - **Řešení:** Nahradit prázdné catch bloky alespoň zápisem do debugovací konzole nebo jednoduchým logováním.

#### 🟢 Kosmetické
- **Jazyková nekonzistence ("Czenglish")**
  - **Soubor:** Všechny soubory v projektu
  - **Dopad:** Nekonzistentní míchání češtiny a angličtiny v názvech (`GeminiAnalizaVysledek` s překlepem, `PrepisAudioAsync`, `UserStory.Titulek` vs `UserStory.Id`).
  - **Řešení:** Sjednotit programové identifikátory do jednoho jazyka (doporučeno do angličtiny).
- **Hardkodovaný seznam Gemini modelů v UI**
  - **Soubor:** [SettingsForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/SettingsForm.cs#L106)
  - **Dopad:** Seznam modelů je zapsán přímo v UI formuláři. Přidání nového modelu vyžaduje úpravu kódu a rekompilaci.
  - **Řešení:** Přesunout seznam modelů do konfigurace.
- **Nadbytečný nahrazovací kód v promptu pro User Stories**
  - **Soubor:** [GeminiService.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/Core/GeminiService.cs#L600)
  - **Dopad:** StringBuilder zbytečně zapisuje placeholder `{0}` a následně jej nahrazuje znakem `}`, což je neefektivní.
  - **Řešení:** Zapsat znak `}` přímo a odstranit volání `.Replace()`.
- **Problémy s formátováním data kvůli InvariantGlobalization**
  - **Soubor:** [SpecCore.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/Core/SpecCore.cs#L674)
  - **Dopad:** V invariantním režimu se tečka formátuje jako `/`. Datum se v exportu zobrazuje jako `11/ 7/ 2026` místo `11. 7. 2026`.
  - **Řešení:** Escapovat tečky ve formátu: `d'.' M'.' yyyy H':'mm`.
- **Mrtvý kód v MainForm**
  - **Soubor:** [MainForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/MainForm.cs#L1789) (`BtnDiktovat_Click`)
  - **Dopad:** Prázdný event handler, který je registrován na tlačítka pro diktování, přičemž veškerá nahrávací logika je v MouseDown/MouseUp.
  - **Řešení:** Metodu odstranit a zrušit její registraci.

---

## 5. UX (User Experience)
*Uživatelské toky, počet kroků k cíli, srozumitelnost stavů, zpětná vazba, přístupnost.*

### Silné stránky
- Diktování držením tlačítka (Push-to-Talk) i přepínáním (Toggle-to-Talk) je velmi intuitivní.
- Rychlé nápovědy (Quick Options) výrazně urychlují práci uživatele.
- Integrace chatu s kontextem specifikace umožňuje přirozenou konverzaci o projektu.

### Nálezy

#### 🔴 Kritické
- **UI Thread Blocking při psaní (Keystroke Lag)**
  - **Soubor:** [MainForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/MainForm.cs#L302)
  - **Dopad:** Události `TextChanged` na polích nápadu a názvu spouštějí kompletní překreslování RTF specifikace a offline kontrolu konzistence na každém stisku klávesy. Při delším textu se psaní citelně seká a aplikace nereaguje na vstupy plynule.
  - **Řešení:** Spouštět překreslování a validaci s debouncingem (např. 500ms po domluvení/dopsání) nebo až při opuštění pole (`Leave` event).

#### 🟡 Důležité
- **Chybějící možnost zrušení asynchronních operací**
  - **Soubor:** [MainForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner) (AI operace)
  - **Dopad:** Uživatel nemá možnost stornovat probíhající AI analýzu nebo odesílání chatu, pokud síťová komunikace visí.
  - **Řešení:** Zobrazit indikátor průběhu (který je již přítomen) a přidat možnost stornování API požadavku.

#### 🟢 Kosmetické
- **Absence klávesových zkratek a tab indexů na hlavních tlačítkách**
  - **Soubor:** [MainForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/MainForm.cs#L756)
  - **Dopad:** Tlačítka "Uložit odpověď" a "Diktovat" nemají definované klávesové zkratky (mnemonics) ani logický tab index, což ztěžuje čistě klávesnicové ovládání.
  - **Řešení:** Přiřadit klávesové zkratky (např. `&Uložit`) a nastavit logický tab index.

---

## 6. UI (User Interface)
*Vizuální konzistence, responsivita, vykreslování vrstev, DPI, mobilní/desktop zážitek, polish issues.*

### Silné stránky
- Přehledné barevné odlišení stavů otázek a zaoblený progress bar dávají skvělou okamžitou odezvu.
- Náhled specifikace s barevným zvýrazněním předpokladů (RTF) vypadá velmi čistě.
- HTML export je moderní responzivní micro-site s tmavým režimem a plynulými přechody.

### Nálezy

#### 🟡 Důležité
- **Nefunkční High-DPI scaling u subdialogů (chybějící AutoScaleMode)**
  - **Soubory:** [SettingsForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/SettingsForm.cs#L33), [MainForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner) (subdialogy)
  - **Dopad:** Vedlejší formuláře (`SettingsForm`, `UserStoriesForm`, `MetrikyForm`, `NalezyForm`) nepoužívají `AutoScaleMode = Font`. Na monitorech se 150%+ škálováním dochází k ořezání textu a překrývání ovládacích prvků.
  - **Řešení:** Nastavit `AutoScaleMode` a `AutoScaleDimensions` v konstruktorech všech subdialogů.
- **Pevné (hardkodované) souřadnice v owner-draw seznamu otázek (`lstOtazky`)**
  - **Soubor:** [MainForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/MainForm.cs#L861)
  - **Dopad:** Badge a chipy se vykreslují na pevných pixelových pozicích. Na High-DPI obrazovkách zůstávají tyto prvky maličké a texty se kvůli zvětšenému písmu překrývají.
  - **Řešení:** Škálovat vykreslovací souřadnice dynamicky podle `DeviceDpi` formuláře.
- **Překrývání prvků v SettingsForm (chyba Z-Order)**
  - **Soubor:** [SettingsForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/SettingsForm.cs#L157)
  - **Dopad:** Panel s `Dock = Fill` je přidán před panel tlačítek s `Dock = Bottom`. To způsobí, že spodní tlačítka překrývají a skrývají rozbalovací seznam modelů na spodku hlavního panelu.
  - **Řešení:** Přidat panel s `Dock = Bottom` jako první, až poté panel s `Dock = Fill`.
- **Chybné stránkování v PDF exportu (ořezávání textu)**
  - **Soubor:** [MainForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/MainForm.cs#L2706)
  - **Dopad:** Pokud je odstavec delší než celá výška stránky, PDF exportér jej nezačne dělit na řádky napříč stránkami, ale vykreslí jej na novou stránku, kde přeteče spodní okraj a zbytek textu se nenávratně ztratí.
  - **Řešení:** Implementovat line-by-line renderování s rozdělováním odstavců na jednotlivé řádky.

#### 🟢 Kosmetické
- **Oříznutá výška panelu Quick Options**
  - **Soubor:** [MainForm.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/MainForm.cs#L697)
  - **Dopad:** Panel `pnlQuickOptions` má pevnou výšku 26px. Pokud se delší nápovědy zalomí, jsou skryté.
  - **Řešení:** Nastavit `AutoSize = true` a odebrat pevnou výšku.
- **Diakritika v CSS třídách v HTML exportu**
  - **Soubor:** [SpecCore.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/Core/SpecCore.cs#L872)
  - **Dopad:** Použití `.badge-předpoklad` může v prostředích s jiným než UTF-8 kódováním způsobit rozpad vzhledu.
  - **Řešení:** Přejmenovat třídu na `.badge-predpoklad` (bez diakritiky).
- **Hrubý vyhledávací filtr v HTML exportu**
  - **Soubor:** [SpecCore.cs](file:///c:/Users/McNeg/Desktop/VoiceCoderWin/CodePlanner/Core/SpecCore.cs#L1032)
  - **Dopad:** Vyhledávání skrývá celé sekce (karty) namísto konkrétních položek. Pokud vyhledáte slovo v backlogu, zůstane zobrazen celý backlog se všemi úkoly.
  - **Řešení:** Skrývat konkrétní nepárované položky a schovat kartu pouze tehdy, pokud jsou skryty všechny její prvky.

---

## 🚀 Doporučené další kroky

### Co vyřešit nejdříve (Vysoká priorita)
1. **Oprava asynchronních pádů oken (ObjectDisposedException):** Přidat kontroly `IsDisposed` po asynchronních voláních. To vyřeší 90 % neočekávaných pádů aplikace.
2. **Vyřešení sekání při psaní (Keystroke Lag):** Odstranit překreslování specifikace z `TextChanged` a nahradit ho debouncingem (např. 500ms zpožděním) nebo spouštěním při `Leave` události.
3. **Zabezpečení exportu (SaveFileDialog):** Ošetřit názvy souborů u exportu backlogu (User Stories) proti nepovoleným znakům.
4. **Oprava High-DPI a Z-Order rozvržení:** Nastavit `AutoScaleMode` na všech subdialogách a opravit pořadí přidávání ovládacích prvků v nastavení, aby se zamezilo překrývání prvků na moderních monitorech.

### Co odložit (Nízká priorita)
1. **Refaktorizace monolitických souborů:** Rozdělení `MainForm.cs` a `SpecCore.cs` do menších souborů (SRP) – kód funguje, refaktoring sice usnadní údržbu, ale nepřidává novou hodnotu pro uživatele.
2. **Přepis identifikátorů ("Czenglish"):** Přejmenování proměnných do čisté angličtiny. Estetická záležitost, která neovlivňuje stabilitu ani chování aplikace.
3. **Hrubý vyhledávací filtr v HTML:** Zjemnění vyhledávání v HTML exportu lze vyřešit až při dalším velkém update exportéru.

---

## ❓ Otevřené otázky pro Jendu

1. **Směřování k víceuživatelskému prostředí / Cloudu:** Chceš do budoucna zachovat čistě offline C# .EXE desktopový model, nebo zvažuješ přechod na webovou aplikaci? (Některé architektonické problémy, jako je generování HTML přímo v jádru, by se při přechodu na web vyřešily samy).
2. **Zpracování chyb pro koncové uživatele:** Jak moc detailní chybové hlášky chceš uživateli zobrazovat při selhání Gemini API (např. zobrazení celé surové chyby ze sítě vs. obecná zpráva typu *"Nepodařilo se spojit s AI, zkontrolujte internetové připojení"*)?
3. **Hlasový vstup a nahrávání:** Stačí ti stávající chování, kdy nahrávání funguje lokálně a přepisuje se přes Gemini API, nebo plánuješ podporu pro jiné specializované Whisper/STT služby bez nutnosti posílat audio data do Gemini?
