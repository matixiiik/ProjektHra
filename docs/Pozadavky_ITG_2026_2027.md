# Požadavky 1. IT Gymnázia na maturitní projekt 2026/2027

Výtah ze dvou školních dokumentů ze sdílené složky *maturita_obecne → projekt_obhajoba*
(odkaz „Info ke zkoušce" na <https://www.itg.cz/pro-studenty/>):

- **Maturitní práce – Projekt 2026/2027: Obecné pokyny a informace** (6. 10. 2026)
- **Maturitní práce – Projekt 2026/2027: IT projekt** (6. 10. 2026)

U každého bodu je, jak ho The Last Beacon splňuje (✅ hotovo, 🔧 doplněno v této verzi, ⏳ zbývá).

## Termíny

| Co | Kdy |
|---|---|
| Předběžný návrh tématu / předkonzultace | do 15. 10. 2026 |
| Výběr a schválení tématu | do 15. 11. 2026 |
| Průběžné konzultace (min. **5**, vede se záznam) | podle harmonogramu |
| **Předfinální kontrolní milník** — Release Candidate, stav testování, kompletní draft dokumentace ke korektuře, připravený způsob distribuce | **31. 3. 2027** |
| **Odevzdání** — finální Release, zdrojové soubory, dokumentace v PDF, uživatelská příručka | **14. 4. 2027** |
| Příprava obhajoby (prezentace, nácvik demonstrace, odborné otázky, reakce na posudky) | do 28. 4. 2027 |

Posudky vedoucího a oponenta dostane žák nejpozději 14 dní před obhajobou.

## Obhajoba

- **Příprava 5 minut, obhajoba 25 minut** před zkušební maturitní komisí.
- Žák: představí zadání a cíl → **prakticky předvede** hlavní výstup → vysvětlí postup,
  architekturu a technologická rozhodnutí → svůj vlastní přínos → problémy a jejich řešení →
  testování → vyhodnotí splnění cíle → limity a další rozvoj → reaguje na posudky → odpovídá na otázky.
- Komise může chtít: spustit aplikaci, ukázat konkrétní funkci, **otevřít zdrojový kód a vysvětlit
  konkrétní část**, orientaci v repozitáři, architekturu / datový model, testování a deployment.
- Hodnocení obhajoby: srozumitelné představení cíle, řešení a výsledku 20 % · praktická demonstrace 20 % ·
  odborné porozumění vlastnímu projektu 25 % · reflexe problémů, výsledků a limitů 10 % ·
  reakce na posudky a odpovědi 25 %.
- Výsledná známka = aritmetický průměr návrhu vedoucího, oponenta a hodnocení obhajoby
  (90–100 % = 1, 75–89 % = 2, 60–74 % = 3, 45–59 % = 4).

→ Prezentace `Prezentace_The_Last_Beacon.pptx` je stavěná na tento průběh a 25 minut (viz poznámky řečníka).

## Zdrojový kód a verzování

- Repozitář na školním GitHubu nebo jiném schváleném úložišti; **průběžné** commity se smysluplnými
  zprávami (nahrání kódu na poslední chvíli se projeví v hodnocení), přehledná struktura, **README**,
  dokumentace významných změn, **finální verze jako Release**. ✅ (GitHub, 20+ PR, README)
- Forma výstupu u hry: stand-alone aplikace (.exe / binárka pro Windows) nebo WebGL build;
  jinak archiv zdrojů / GitHub Release s návodem k sestavení. ⏳ GitHub Release s Windows buildem.

## Hodnocení softwarového projektu

**Vedoucí:** funkčnost 30 % · kvalita kódu (struktura, čitelnost, modularita) 20 % · jazykové a vývojové
konvence 5 % · dokumentace zdrojového kódu (komentáře, API dokumentace, README) 5 % · dokumentace –
formální a technická úroveň 10 % · dokumentace – obsah a odbornost 20 % · průběžná práce 10 %.

**Oponent:** funkčnost a naplnění zadání 40 % · návrh a architektura 20 % · kvalita zdrojového kódu 20 % ·
technická úroveň a technologie 10 % · dokumentace a reprodukovatelnost (README, jde sestavit a spustit) 10 %.

Posuzuje se mj. struktura a čitelnost kódu, konvence, ošetření chybových stavů, validace vstupů,
**testování**, výkon, uživatelská použitelnost, vlastní odborný přínos.

## Dokumentace (= „seminárka")

Dokumentace **není samostatná seminární práce**, ale součást projektu. Ukládá se do repozitáře
(zpravidla `/docs`) a odevzdává v **PDF**. Minimální počet normostran není stanoven.
Struktura podle dokumentu IT projekt:

1. **Úvod** — představení díla, řešený problém, cíl, cílová skupina / využití
2. **Analýza problému a odborná východiska** — současný stav, existující řešení, odborné a technologické
   principy, možné technologie a jejich porovnání, důvody výběru
3. **Návrh řešení** — architektura, moduly a jejich propojení, datový model, UI/UX, use cases, diagramy
4. **Implementace** — hlavní funkce, použité technologie, významné části vlastní implementace, knihovny
   třetích stran, technické problémy a jejich řešení, změny oproti návrhu
5. **Testování** — způsob, konkrétní testovací scénáře, výsledky, známé chyby a omezení
6. **Nasazení** — prostředí, deployment, způsob instalace / spuštění
7. **Vyhodnocení** — míra naplnění cíle, silné a slabé stránky, omezení, možnosti rozvoje
8. **Závěr**
9. **Seznam použitých zdrojů** — jednotně podle **ČSN ISO 690**
+ přílohy (diagramy, screenshoty, výsledky testů); uživatelská příručka je samostatný dokument.

## Licence, cizí zdroje a AI

- Uvést všechny knihovny, assety a zdroje třetích stran, respektovat licence, odlišit vlastní a převzaté. ✅
  (`LICENSE`, `THIRD_PARTY_LICENSES.md`)
- AI smí být pracovní nástroj (rešerše, návrh variant, vysvětlování, hledání chyb, úpravy kódu, testy,
  dokumentace). **Pokud AI významně zasahovala do kódu nebo dokumentace, musí to být uvedeno
  v dokumentaci** a žák musí umět **vysvětlit a obhájit celý odevzdaný projekt**.
- Nepřípustné: vydávat AI výstup za vlastní, odevzdat kód, kterému žák nerozumí, smyšlené zdroje,
  zatajení významného využití AI.
