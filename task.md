# Úkoly pro novou fázi (Boby 2, 3, 4)

## Bod 2: Možnost zrušení AI operací (storno)
- [x] Přidat CancellationTokenSource a cancel logiku do `MainForm.cs` (AI Analýza)
- [x] Přidat CancellationTokenSource a cancel logiku do `NalezyForm.cs` (AI Konzistence)
- [x] Přidat CancellationTokenSource a cancel logiku do `UserStoriesForm.cs` (AI Stories)
- [x] Přidat CancellationTokenSource a cancel logiku do `MetrikyForm.cs` (AI Metriky)

## Bod 3: Vylepšení vyhledávání v náhledu
- [x] Upravit `HledatText` v `MainForm.cs`, aby odscrollovala na první nalezený výskyt

## Bod 4: Perzistence a detekce tmavého režimu v HTML
- [x] Upravit `RenderHtml` v `SpecCore.cs` pro inline detekci motivu v body
- [x] Upravit JavaScript v HTML exportu pro ukládání a obnovení motivu přes localStorage

## Verifikace
- [x] Ověřit kompilaci a spustit CoreTests
- [x] Vyzkoušet funkčnost storna AI operací
- [x] Vyzkoušet vyhledávání a automatické scrollování v náhledu
- [x] Vyzkoušet přetrvávání tmavého režimu v HTML exportu
