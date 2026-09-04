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
| `f54152f` | **Fáze 2b (2/3)** — scéna `LighthouseInterior.unity` (v Build Settings). `LighthouseManager.Enter()` = save + LoadScene. `LighthouseInterior.cs` (řídí scénu, ukazatel mincí, `ExitToIsland()`). `InteriorPlayer.cs` (plynulá chůze WASD/šipky, `E` = nejbližší bod). `InteriorInteractable.cs` (UpgradeShop/QuestShop/Exit). **Oba obchody fungují uvnitř přes GameSession.** |
| `0d78878` | **Fáze 2c** — vzhled interiéru: kamenné zdi + dřevěná podlaha + koberec, dollhouse pohled (přední zeď otevřená), 2 Kenney okenní díly v zadní zdi, barevné pulty (modrá/oranžová) s rekvizitami, rekvizity po místnosti (truhla/sudy/bedny/dělo), postavička hráče (tělo+hlava+klobouk), teplé světlo. Ověřeno: okruh dál funguje. |

| `099cd95` | **Fáze 3** — bedny + mega quest. `TileType.Chest = 9`, ~40 % ostrovů má bednu. `ChestPrefab` (Kenney truhla, odklopitelné víko). `ChestManager`: E u bedny → mince + mega quest ("poklad na mapě", cíl 35–70 políček daleko). Kopání: v lodi na cíli Space → `DigRoutine` → `dug`. QuestShop: "Vyplatit mega quest" → mince + trvalý `sellBonus` (+5 k výkupu). HUD 2. řádek s mapou. `GameData`: `MegaQuest`, `openedChests`, `sellBonus` (+ player2). Ověřeno end-to-end + persistence. |
| `efddd7d` | HUD: souřadnice hráče `X: n   Y: n` v levém horním rohu (aktualizuje se při pohybu, split-screen aware). |
| `6eaebda` | Ostrovy organický tvar (ne čtverce, jádro min. 3×3 + náhodné rozrůstání, plátno 14×14), 2 mola vedle sebe na kraji. Maják 2×2 dlaždice. |
| `0a00690` | `IslandTerrain.cs` = hladký generovaný mesh na celý ostrov místo písečných dlaždic (svah pláže pod hladinu, Perlin šum, flat shading). GridManager: flood-fill souše, spawn u prvního objektu ostrova, cleanup. Harbor/Lighthouse/Chest prefaby už bez vlastního písku. Zaplňovací průchod v StampOrganicLand → žádné díry uvnitř. Ostrov vycentrovaný na %20 políčko (oprava crashe). **Hlavní kamera nakloněná 52° (3/4 pohled)** místo kolmo shora. |
| `deff7a6` | Pěší hráč (`HeadDot`) = postavička (tělo+hlava+klobouk+nos, jako v majáku), otáčí se po směru chůze. `CameraOrbit.cs` na Main Camera: pravé tlačítko myši + tah = kamera obíhá hráče (yaw/pitch), blokuje se při UI. `ACTIVE_GRID_SIZE` 15→19. `RenderSettings.fog` ve scéně (Linear 15–32) schová okraj generování. |

## STAV: CELÝ PŮVODNÍ PROJEKT HOTOVÝ ✅

Fáze 1 (Kenney grafika) + 2 (maják se scénou a obchody) + 3 (bedny + mega quest)
jsou všechny hotové, ověřené v Unity, na GitHubu.

### Možný polish / co dál (nic z toho není nutné)
- Postavička hráče v majáku i venku (headDot = šedá kostka) = pořád placeholder.
- Mega quest: žádný kompas/šipka k cíli — jen souřadnice v HUD. Šlo by přidat
  směrovku na okraj minimapy.
- Interiér majáku: dá se ještě zútulnit.
- Zvuk: hra pořád nemá žádné audio (viz původní návrh — největší cheap win).
- Post-processing (URP Volume): pořád prázdný `Global Volume` ve scéně.
- Voda je plochá — žádný shader.

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
