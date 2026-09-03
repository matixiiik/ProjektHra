# Handoff — kde jsme skončili (2026-09-04)

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
| `82c844c` | **Fáze 2a** — `TileType.Lighthouse = 8`. `GridManager.PlaceLighthouse()` (nahradil `PlaceShops`) staví 1 maják na ostrov. `LighthousePrefab` = skládaná Kenney věž. `LighthouseManager.cs` singleton — pěšky `E` u majáku → `Enter()` (zatím jen `Debug.Log`). Minimapa maják červeně. Staré UpgradeShop/QuestShop dlaždice se pořád vykreslí (staré savy), jen se negenerují. |

## DALŠÍ KROK — Fáze 2b (odhad 4–6 h)

1. **`GameSession` singleton** (`DontDestroyOnLoad`) držící `GameData`. Přepsat tok dat:
   `GridManager`, `SaveManager`, `UpgradeShopManager`, `QuestShopManager` čtou z něj
   místo `gridManager.gameData`. Riziko: JsonUtility serializace, split-screen, save sloty.
2. **Přechod scén**: `E` u majáku → save → `SceneManager.LoadScene("LighthouseInterior")`
   → hráč u dveří. Dveře ven → zpět `SampleScene`, hráč pěšky u majáku, kamera/HUD/loď OK.
3. **Postavit interiér** (místnost, světlo, kamera, jednoduchá chůze).
4. Pak **Fáze 2c** = obchody uvnitř (napojit na GameSession + pulty + styl). ~1,5–2 h.
5. Pak **Fáze 3a/3b** = bedna + mega quest.

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
