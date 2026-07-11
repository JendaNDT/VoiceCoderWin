# VoiceCoder Brief – Project Status
*Naposled aktualizováno: 11. 7. 2026*

## 🎯 Co to je
Windows .exe demonstrátor projektu VoiceCoder AI (dle PDF návrhu, kap. 18): z volně popsaného nápadu vytvoří řízenými otázkami verzovanou specifikaci s exportem pro kódovacího agenta. Bez AI, plně offline.
Stack: C# / .NET 8, WinForms, self-contained single-file exe (win-x64), kompilováno ze sandboxu přes EnableWindowsTargeting.

## ⏭️ Příští krok
**Jenda otestuje v0.2 na Windows (hlavně nový formátovaný náhled) a nahlásí dojmy.**
ZIP `VoiceCoderBrief_v0.2_Windows.zip` je ve složce projektu. Po odsouhlasení: nahrát release v0.2.0 na GitHub (push ze sandboxu nejde bez přihlášení – vyřešit).

## ✅ Hotovo
- v0.1 kompletní a **ověřená Jendou na reálných Windows** (spuštění, okno, diktování, exporty – vše OK)
- Jádro: model specifikace se 7 bloky, 10 řízených otázek, verzování, log, export MD/JSON, .vcbrief
- WinForms GUI dle kap. 11 + 36 automatických testů jádra
- **v0.2 zkompilováno (63 MB exe, ZIP ve složce) + všech 36 testů jádra prošlo:**
  - Formátovaný náhled specifikace (RTF: nadpisy, odrážky, tučné, oranžové [PŘEDPOKLAD])
  - Barevný seznam otázek (✔ zelená / ≈ oranžová / ○ šedá + štítek dopadu V/S)
  - Progress bar postupu (teal, zaoblený)
  - Klávesové zkratky: Ctrl+N/O/S, Ctrl+M (export MD), Ctrl+J (export JSON), Ctrl+Enter (uložit odpověď)
  - Hvězdička v titulku při neuložených změnách + název projektu v titulku
  - Roztahovatelný log (Splitter)
  - Ikonky a tooltipy v toolbaru, hover efekty tlačítek, PerMonitorV2 DPI

## 📝 TODO
### MVP (nutné pro v1)
- Jenda otestuje nový vzhled v0.2 na Windows
- Commitnout v0.2 na GitHub + release (chybí přihlášení ke GitHubu ze sandboxu)

### Backlog (později)
- Napojení na Claude API (AI otázky a generování specifikace) – vlastní API klíč v nastavení
- Skutečný hlasový vstup přes STT API místo Win+H (push-to-talk dle kap. 6)
- Consistency checker (hlídání rozporů ve specifikaci, kap. 7)
- Vlastní ikona aplikace a podepsání exe (odstraní SmartScreen varování)
- Šablony otázek podle typu aplikace (hra / evidence / nástroj)

## 🐛 Známé bugy
- Zatím žádné hlášené. Riziko k ověření ve v0.2: RTF náhled (nová věc) – při chybě má fallback na syrový markdown.

## 🏗️ Klíčová rozhodnutí
- **Rozsah v1:** demonstrátor bez AI dle kap. 18 PDF – ověřit workflow otázky→specifikace dřív, než se přidá AI a agenti.
- **Stack:** C# WinForms místo webové appky, protože Jenda chtěl vyloženě .exe; self-contained single file, aby nebyla potřeba instalace .NET.
- **Hlas v v1:** přes diktování Windows (Win+H) do textových polí – nula kódu, žádné API klíče; skutečné STT až později.
- **Verzování:** každé rozhodnutí zvyšuje číslo verze specifikace a zapisuje se do logu (dle kap. 7 „živý kontrakt").
- **JSON export:** stabilní struktura (sekce → položky → predpoklad flag), aby ji později mohl číst orchestrátor beze změn.
- **Náhled specifikace (v0.2):** vlastní mini-převod markdown→RTF přímo v MainForm (žádná externí knihovna – jednodušší build, žádné závislosti); při chybě fallback na plain text.
- **Git na ploše:** připojená složka nepodporuje mazání/zámky souborů → commit+push se dělá ze sandboxu, ne přímo ze složky.

## 📁 Stav souborů
- `VoiceCoderBrief/Core/SpecCore.cs` – jádro: model, otázky, verzování, render MD/JSON, ukládání (v0.2: jen bump verze nástroje)
- `VoiceCoderBrief/MainForm.cs` – celé GUI (v0.2: velký facelift – RTF náhled, owner-draw seznam, progress bar, zkratky)
- `VoiceCoderBrief/Program.cs` – vstupní bod (v0.2: PerMonitorV2 DPI)
- `VoiceCoderBrief/VoiceCoderBrief.csproj` – konfigurace buildu (v0.2.0)
- `CoreTests/` – automatické testy jádra (spustitelné i na Linuxu)
- `VoiceCoderBrief_v0.2_Windows.zip` – hotová aplikace v0.2 + návod CTI_ME.txt
