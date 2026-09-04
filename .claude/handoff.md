# Handoff — kde jsme skončili (2026-09-04, aktualizováno večer)

Tenhle soubor je most mezi počítači. Claude paměť se nesyncuje přes git, tak si
sem Claude píše, kde se přestalo, aby se dalo pokračovat i z notebooku.

> **Claude: přečti si tohle na začátku session a rovnou navaž. Až se kus práce
> udělá, tenhle soubor aktualizuj a zacommituj.**

---

## Co se dělá

Velký vícefázový úkol:
1. Přegrafikovat hru do **Kenney low-poly stylu** (Pirate Kit, CC0, už v repu v `Assets/Kenney/PirateKit/`).
2. Udělat **maják**, do kterého se dá vejít — **samostatná scéna** `LighthouseInterior.unity`.
3. **Bedny** — `E` otevře → mega quest (hard) + mince.

## Rozhodnutí uživatele (držet se jich)

- Maják = **samostatná scéna**, ne fake overlay. Pošťačka chodí uvnitř, dveře ven
  vrátí na ostrov k majáku.
- **Oba obchody (upgrade + quest) jsou UVNITŘ majáku**, ne na mapě. Grafiku obchodů taky předělat.
- Bedna: `E` → **mega quest** (něco daleko / na jiném ostrově) + mince. Výplata mega
  questu jde v **kterémkoli** questshopu. Ukládat, které bedny jsou otevřené.
- Kód: jednoduchý, hodně českých komentářů (maturita, obhajuje se ústně). Neover-engineerovat.
- Pozn.: původní „zero gameplay impact" už neplatí — uživatel si tyhle featury vyžádal.

## Stav — HOTOVO (na GitHub main)

| Commit | Co |
|---|---|
| `c665826` | Kenney Pirate Kit v repu, sdílený materiál `Assets/Kenney/PirateKit/PirateColormap.mat`, loď přebarvená z šedé |
| `1a9943c` | **Fáze 1** — dlaždicové prefaby z Kenney dílů: HarborPrefab = písek + `IslandDecor.cs` (náhodně palma/kámen/tráva), PierPrefab = dřevěná plošina, TreasurePrefab = truhla, Shop1/Shop2 = písek + rekvizita. Voda hlubší tyrkys, měkčí světlo, ambient Trilight. |
| `82c844c` | **Fáze 2a** — `TileType.Lighthouse = 8`. `GridManager.PlaceLighthouse()` (nahradil `PlaceShops`) staví 1 maják na ostrov. `LighthousePrefab` = skládaná Kenney věž. Minimapa maják červeně. Staré UpgradeShop/QuestShop dlaždice se pořád vykreslí (staré savy), jen se negenerují. |
| `10ede05` | **Fáze 2b (1/3)** — `GameSession` singleton (DontDestroyOnLoad) drží `GameData`. `GridManager.gameData` je teď property → `GameSession.Instance.Data` (~85 externích čtení beze změny). Shopy čtou přes `GameData Data => GameSession.Instance.Data`, ukládají přes `Persist()`/`Save()` co fungují i bez GridManageru. `MainMenuManager` skip přes `GameSession.ReturningFromLighthouse`. |
| `f54152f` | **Fáze 2b (2/3) + většina 2c** — scéna `LighthouseInterior.unity` (místnost: podlaha/zdi/dveře/lampa/kamera, v Build Settings). `LighthouseManager.Enter()` = save + LoadScene. `LighthouseInterior.cs` (řídí scénu, ukazatel mincí, `ExitToIsland()`). `InteriorPlayer.cs` (plynulá chůze WASD/šipky, `E` = nejbližší bod). `InteriorInteractable.cs` (UpgradeShop/QuestShop/Exit). **Oba obchody fungují uvnitř přes GameSession.** Materiály interiéru v `Assets/TutorialInfo/Materials/Interior*.mat`. **Celý okruh ověřen: E u majáku → interiér → nákup → dveře ven → zpět na ostrov, stav zachován, menu nevyskočí.** |

## DALŠÍ KROK

**Fáze 2b je HOTOVÁ.** Zbývá:

### Fáze 2c – dodělat vzhled interiéru (~1 h, nepovinné teď)
- Pulty (`Counter_Upgrade`, `Counter_Quest` ve scéně) jsou jen barevné kostky —
  vyměnit za Kenney stůl/regál. Postavička hráče = kapsle → něco hezčího.
- Kulatější „věžní" pokoj místo hranatého, koberec, útulnější světlo.

### Fáze 3a – bedna (nová featura)
- `E` u bedny (pěšky) → otevře se, dá mince + **mega quest**.
- Ukládat otevřené bedny: nové pole `GameData` (např. `List<string> openedChests`
  s klíči "x,y" — JsonUtility-safe).
- Kde se bedny berou: buď nový TileType.Chest, nebo existující Treasure dlaždice
  na pevnině. Rozmyslet s uživatelem.

### Fáze 3b – mega quest
- Nové pole `GameData.megaQuest` (vlastní `ActiveQuest`, aby šel vedle běžného).
- Cíl: „dopluj daleko od ostrova" / „na jiný ostrov". Sledovat v pohybu hráče.
- Výplata: `QuestShopManager` dostane tlačítko „Vyplatit mega quest" když je splněn,
  funguje v kterémkoli questshopu.

## Setup na novém počítači (notebook)

- Unity **6000.3.10f1** přes Unity Hub, otevřít projekt.
- `gh auth login`.
- Unity MCP: nainstalovat Python 3.12 + `uv` (winget: `Python.Python.3.12`, `astral-sh.uv`),
  pak `claude mcp add --scope local --transport stdio UnityMCP -- "<uvx>" --prerelease explicit --from "mcpforunityserver>=0.0.0a0" mcp-for-unity --transport stdio`
  (uvx cesta z winget). V Unity: Window → MCP for Unity → Transport **Stdio** → **Start Session**.
- UnityYAMLMerge git config — příkazy v `CLAUDE.md` sekce Git.

## Testovací poznámky (Unity MCP, ušetří čas)

- Editace .cs za běhu play mode → Unity vypadne z play mode. Nejdřív kód, pak test.
- Po `manage_editor(play)` počkat ~3 s, jinak `FindFirstObjectByType<PlayerController>()`
  vrátí instanci před `Start()` → `gridManager` null → NRE.
- Spustit hru pro test: reflexí zavolat privátní `MainMenuManager.StartNewGame(int)`.
- `execute_code` = jen CodeDom (C# 6): žádné `using`, žádné lokální funkce, plně
  kvalifikované názvy. Herní typy načíst přes `MonoScript.GetClass()`, ne `Type.GetType`.
