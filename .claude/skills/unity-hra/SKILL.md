---
name: unity-hra
description: >-
  Pracovní postup pro Game1 — Unity hru "Lodní dobrodružství" (maturitní projekt).
  POUŽIJ VŽDY, když se v tomhle repu přidává nebo mění herní funkce, opravuje bug,
  edituje jakýkoli skript v Assets/TutorialInfo/Scripts, řeší se chování ve scéně,
  save systém, multiplayer, obchody, minimapa, konzole nebo když si uživatel není
  jistý, co má naklikat v Unity. Skill drží maturitní pravidla (české komentáře,
  jednoduchý obhajitelný kód, opatrnost s referencemi ve scéně), umí ověřit
  kompilaci bez otevřeného Unity a diktuje uživateli krok-za-krokem, co udělat
  v editoru.
---

# unity-hra — vývoj hry Game1

Hra: 2D/3D Unity, hráč pluje po nekonečném oceánu, rybaří, těží poklady, kupuje
vylepšení, plní questy na ostrovech. Má lokální split-screen pro dva hráče,
hlavní menu se 3 save sloty a vývojářskou konzoli.

**Architektura, konvence a „nepřečíslovávat enum" jsou v [`CLAUDE.md`](../../../CLAUDE.md)
v kořeni repa — ten se načítá automaticky. Tento skill přidává _pracovní postup_.**

Podrobnosti, na které SKILL.md odkazuje:
- [`references/architecture.md`](references/architecture.md) — mapa skriptů, kdo s kým mluví, záludnosti
- [`references/adding-features.md`](references/adding-features.md) — recepty na typické úpravy (nový typ políčka, upgrade, příkaz konzole, klávesa, HUD prvek)
- [`references/unity-mcp.md`](references/unity-mcp.md) — které UnityMCP nástroje na co použít

---

## 5 pravidel, která se neporušují (je to maturita)

1. **Nulový dopad na hratelnost, když se dělá „úklid".** Refactor a komentáře ano;
   měnit čísla, časování, pravidla pohybu ne — pokud to uživatel vysloveně nechce.
2. **České komentáře.** Každá nová metoda/pole má krátký komentář „k čemu to je".
   Styl: viz už okomentované soubory (hlavička s čárou `─────`, věcně, s diakritikou).
3. **Kód musí být obhajitelný u zkoušky.** Radši delší jasný kód než chytrý
   jednořádkový trik. Bez nových návrhových vzorů, bez namespace, bez singletonů
   (kromě už existujících statických flagů).
4. **Opatrně s referencemi ve scéně.** `SampleScene.unity` a prefaby odkazují na
   skripty přes GUID a na `public`/`[SerializeField]` pole přes jméno.
   - **Nepřejmenovávej** `public` třídy, `public`/`[SerializeField]` pole ani
     `public` metody volané z UI tlačítek (`PauseMenu.ExitGame/NewGame/ContinueGame`).
   - Nové `public` pole = uživatel ho musí zapojit v Inspektoru → řekni mu to (viz níže).
   - Mazat `private` nepoužité pole je OK.
5. **Před „hotovo" ověř kompilaci.** Buď přes UnityMCP (`validate_script` / `read_console`),
   nebo skriptem `bash .claude/skills/unity-hra/scripts/compile-check.sh`.
   Nikdy netvrď „přeloží se", aniž bys to spustil.

---

## Postup A — Unity je otevřené (UnityMCP funguje)

Poznáš to tak, že `mcp__UnityMCP__read_console` něco vrátí (ne „No Unity Editor
instances found").

1. **Před úpravou** přečti stav: `read_console` (typy `error`, `warning`) — ať víš,
   z čeho vycházíš. U změn ve scéně mrkni na resource `mcpforunity://editor/state`.
2. **Uprav kód** běžně (Edit/Write). Drobné cílené zásahy do jedné metody jde dělat
   i přes `mcp__UnityMCP__script_apply_edits` (`replace_method` apod.), ale obyčejný
   Edit je většinou přehlednější.
3. **Nech Unity přeložit:** `mcp__UnityMCP__refresh_unity` s `compile: request`,
   `wait_for_ready: true`.
4. **Zkontroluj výsledek:** `read_console` (`types: ["error"]`). Nula chyb = OK.
   Když chyba → oprav a opakuj od bodu 3.
5. **Otestuj chování:** `mcp__UnityMCP__manage_editor` `action: play` → chvíli počkej
   → `read_console` na runtime chyby → `action: stop`.
   Pozn.: hru pořádně proklikat (obchody, split-screen, save) musí člověk — viz Postup B, krok „řekni uživateli".
6. **Testy** (jen když existují): `mcp__UnityMCP__run_tests` `mode: EditMode`.

Detailní tabulka nástrojů: [`references/unity-mcp.md`](references/unity-mcp.md).

---

## Postup B — Unity je zavřené

1. **Uprav kód** (Edit/Write).
2. **Ověř kompilaci:**
   ```bash
   bash .claude/skills/unity-hra/scripts/compile-check.sh
   ```
   Vrátí `✅` + kód 0, nebo `❌` se seznamem chyb (soubor:řádek).
   - **Omezení:** používá seznam souborů z poslední kompilace Unity. Když PŘIDÁŠ
     nový `.cs` soubor, skript ho neuvidí, dokud Unity projekt jednou nepřegeneruje
     (stačí, aby uživatel klikl do editoru). U nových souborů si projdi syntaxi
     ručně a řekni uživateli, že finální kontrola je v Unity.
3. **Řekni uživateli krok-za-krokem, co udělat v Unity** — viz šablona níže.

---

## Jak psát „co udělat v Unity" uživateli

Uživatel je student, ne Unity expert. Instrukce piš jako číslovaný seznam,
konkrétně, s názvy objektů. Vždy zahrň i **jak pozná, že to klaplo**.

**Šablona:**

> **Co udělat v Unity:**
> 1. Přepni se do Unity (jen kliknout do okna) — nahoře uvidíš „Reloading…",
>    počkej, až zmizí.
> 2. Otevři **Console** (Window → General → Console nebo `Ctrl+Shift+C`).
>    Musí být **bez červených chyb**. Když nějaká je, pošli mi její text.
> 3. *(jen když jsem přidal `public` pole)* Ve **Hierarchy** klikni na objekt
>    **`<jméno>`**, v **Inspectoru** u komponenty **`<Skript>`** najdi políčko
>    **`<pole>`** a přetáhni do něj **`<co>`**.
> 4. Zmáčkni **Play** (▶ nahoře).
> 5. Vyzkoušej: `<konkrétní krok — např. dopluj k ostrovu, zmáčkni E u obchodu>`.
>    Mělo by se stát: `<očekávaný výsledek>`.
> 6. Zmáčkni **Stop**. Kdyby něco nešlo, napiš mi, co přesně.

Když je připojený UnityMCP, kroky 1–2 (a případně 4) udělej sám a uživateli
nech jen „proklikej to a řekni, jestli sedí X".

---

## Přidání nové funkce — rychlý postup

1. Mrkni do [`references/adding-features.md`](references/adding-features.md), jestli tam
   není recept pro tenhle typ úpravy.
2. Zjisti, kdo je „vlastník" dané oblasti (mapa v [`references/architecture.md`](references/architecture.md)).
3. Když funkce potřebuje ukládat stav → přidej pole do `GameData`
   (a v multiplayeru i `player2…` variantu). Pozor na limity `JsonUtility`
   (žádné `Dictionary`, `null` kolekce, polymorfismus).
4. Nová klávesa → respektuj vstupní brány (`GameConsole.IsOpen`,
   `MainMenuManager.IsVisible`, `UpgradeShopManager.AnyShopOpen`, `…IsOpen`).
5. Přidej české komentáře.
6. Ověř kompilaci (Postup A bod 3–4, nebo Postup B bod 2).
7. Napiš uživateli kroky pro Unity.
8. Až uživatel potvrdí, že to funguje → shrň změnu. Commituj jen když si řekne.

---

## Časté záludnosti

- **P1/P2 routing:** `PlayerController` i oba obchody čtou/zapisují přes pomocné
  metody/property (`GridX`, `PCoins`, `GetCoins()`…), které podle `playerIndex`
  míří buď do polí P1, nebo `player2…`. Nová per-hráč hodnota musí projít stejně.
- **Statické flagy přežijí „Nová hra"** v buildu (ne v editoru — tam je domain reload).
  `GameConsole.IsOpen`, `UpgradeShopManager.AnyShopOpen`, `MainMenuManager.IsVisible`.
- **`OnWorldChanged`** se musí zavolat po každé změně, co má vidět HUD/minimapa
  (`grid.NotifyWorldChanged()` nebo `grid.Save()` který ho volá taky).
- **`GenerateWorld` běží při každém kroku** — nedávej do něj drahé věci.
- **Save po každé mikro-akci** (prodej, nákup) — je to tak schválně, neřeš výkon,
  pokud si uživatel nestěžuje.
- Nový `.cs` soubor musí mít i `.meta` (Unity ho vygeneruje) a **oboje se commituje**.
