# VoiceCoder Brief

Windows aplikace (.exe), která z volně popsaného nápadu udělá strukturovanou,
verzovanou specifikaci připravenou pro kódovacího agenta.

Demonstrátor projektu **VoiceCoder AI** – první krok podle kap. 18 návrhu
(„malý demonstrátor bez automatického kódování"). Bez AI, plně offline.

## Co umí (v0.1)

- Zápis nápadu textem, nebo diktováním Windows (Win+H)
- 10 řízených otázek seřazených podle dopadu (platforma, offline, osobní údaje, non-goals, akceptace…)
- „Nevím" → viditelně označený **[PŘEDPOKLAD]** místo tichého odhadu
- Živá specifikace se 7 bloky (Cíl a uživatelé, Rozsah, UX, Data, Technika, Akceptace, Rizika)
- Verzování a log každého rozhodnutí
- Export **Markdown** (pro člověka) a **JSON** (stabilní struktura pro agenta)
- Ukládání projektu jako `.vcbrief`

## Stažení a spuštění

Hotové `VoiceCoderBrief.exe` najdeš v [Releases](../../releases). Nic se neinstaluje –
stačí spustit (Windows 10/11, 64-bit). Při prvním spuštění projdi SmartScreen:
**Další informace → Přesto spustit** (aplikace zatím není podepsaná).

## Stavba ze zdrojáků

Vyžaduje .NET 8 SDK (na Windows, Linuxu i macOS):

```bash
cd VoiceCoderBrief
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Testy jádra (běží i mimo Windows):

```bash
cd CoreTests
dotnet run -c Release
```

## Struktura

| Cesta | Obsah |
|---|---|
| `VoiceCoderBrief/Core/SpecCore.cs` | Jádro: model, otázky, verzování, render MD/JSON |
| `VoiceCoderBrief/MainForm.cs` | WinForms GUI |
| `CoreTests/` | Automatické testy jádra (36 kontrol) |
| `PROJECT_STATUS.md` | Živý stav projektu (vibecoding tracker) |

## Roadmapa

Viz `PROJECT_STATUS.md` – backlog zahrnuje napojení na Claude API (AI otázky),
skutečné STT místo Win+H, consistency checker a podepsání exe.

---
*Vzniklo vibecodingem – nápad a směr: Jenda (JendaNDT), kód: Claude.*
