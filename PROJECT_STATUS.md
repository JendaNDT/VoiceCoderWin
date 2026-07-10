# VoiceCoder Brief – Project Status
*Naposled aktualizováno: 10. 7. 2026*

## 🎯 Co to je
Windows .exe demonstrátor projektu VoiceCoder AI (dle PDF návrhu, kap. 18): z volně popsaného nápadu vytvoří řízenými otázkami verzovanou specifikaci s exportem pro kódovacího agenta. Bez AI, plně offline.
Stack: C# / .NET 8, WinForms, self-contained single-file exe (win-x64), kompilováno ze sandboxu přes EnableWindowsTargeting.

## ⏭️ Příští krok
**Jenda otestuje v0.1 na Windows počítači a nahlásí, co funguje/nefunguje.**
Hlavně: spuštění přes SmartScreen, čitelnost okna, diktování Win+H v češtině, export MD/JSON.

## ✅ Hotovo
- Jádro: model specifikace se 7 bloky dle PDF (Cíl a uživatelé, Rozsah, UX, Data, Technika, Akceptace, Rizika)
- 10 řízených otázek seřazených podle dopadu (question planner light) + označené předpoklady [PŘEDPOKLAD]
- Živá specifikace s verzováním a logem každého rozhodnutí
- Export Markdown (pro člověka) + JSON (stabilní struktura pro agenta)
- Ukládání/otevírání projektu (.vcbrief)
- WinForms GUI dle kap. 11: vlevo nápad+otázky, uprostřed živá specifikace, dole log
- 36 automatických testů jádra prošlo (na Linuxu v sandboxu)
- Zkompilované VoiceCoderBrief.exe (~63 MB, nic se neinstaluje) + CTI_ME.txt, zabaleno v ZIP

## 📝 TODO
### MVP (nutné pro v1)
- Ověřit v0.1 na reálných Windows (GUI jsem nemohl spustit – sandbox je Linux)
- Doladit vzhled podle zpětné vazby (velikosti, DPI, barvy)

### Backlog (později)
- Napojení na Claude API (AI otázky a generování specifikace) – vlastní API klíč v nastavení
- Skutečný hlasový vstup přes STT API místo Win+H (push-to-talk dle kap. 6)
- Consistency checker (hlídání rozporů ve specifikaci, kap. 7)
- Vlastní ikona aplikace a podepsání exe (odstraní SmartScreen varování)
- Šablony otázek podle typu aplikace (hra / evidence / nástroj)

## 🐛 Známé bugy
- Zatím žádné hlášené – GUI čeká na první reálný test na Windows.

## 🏗️ Klíčová rozhodnutí
- **Rozsah v1:** demonstrátor bez AI dle kap. 18 PDF – ověřit workflow otázky→specifikace dřív, než se přidá AI a agenti.
- **Stack:** C# WinForms místo webové appky, protože Jenda chtěl vyloženě .exe; self-contained single file, aby nebyla potřeba instalace .NET.
- **Hlas v v1:** přes diktování Windows (Win+H) do textových polí – nula kódu, žádné API klíče; skutečné STT až později.
- **Verzování:** každé rozhodnutí zvyšuje číslo verze specifikace a zapisuje se do logu (dle kap. 7 „živý kontrakt“).
- **JSON export:** stabilní struktura (sekce → položky → predpoklad flag), aby ji později mohl číst orchestrátor beze změn.

## 📁 Stav souborů
- `VoiceCoderBrief/Core/SpecCore.cs` – jádro: model, otázky, verzování, render MD/JSON, ukládání
- `VoiceCoderBrief/MainForm.cs` – celé GUI
- `VoiceCoderBrief/Program.cs` – vstupní bod
- `VoiceCoderBrief/VoiceCoderBrief.csproj` – konfigurace buildu (win-x64, single file)
- `CoreTests/` – automatické testy jádra (spustitelné i na Linuxu)
- `VoiceCoderBrief_v0.1_Windows.zip` – hotová aplikace + návod CTI_ME.txt
