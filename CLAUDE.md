# ProjektHra — Lodní dobrodružství

3D Unity hra: hráč pluje po nekonečném oceánu, rybaří, těží poklady z vraků,
bojuje s piráty a nepřátelskými ostrovy, kupuje vylepšení v majáku, plní questy
a mega questy a postupně odemyká krátký příběh (starý námořník na startovním
ostrově). Podporuje lokální split-screen pro dva hráče.

## Technické základy

- **Unity 6000.3.10f1**, Universal Render Pipeline 17.3.0
- Balíček `com.unity.inputsystem` je nainstalovaný, ale **kód používá starý
  `UnityEngine.Input`** (`Input.GetKey…`, `Input.GetMouseButton…`).
  Input Handling = *Both*.
- **Dvě scény:**
  - `Assets/Scenes/SampleScene.unity` — hlavní hra (oceán, ostrovy, souboje)
  - `Assets/Scenes/LighthouseInterior.unity` — vnitřek majáku (obchody, mapa na zdi)
  - Obě jsou v Build Settings. Přepínají se `SceneManager.LoadScene(<jméno>)`
    v `LighthouseManager` / `LighthouseInterior`.
- Veškerý herní kód: `Assets/Scripts/*.cs`. Materiály `Assets/Materials/`,
  runtime-loadované modely `Assets/Resources/{IslandDecor,PirateShips}/`.
- **Žádný namespace** — všechny třídy jsou globální.
- **Žádné testy** (balíček test-framework je, suita neexistuje).
- Komentáře a herní texty jsou **česky**. Hlavičky souborů/sekcí komentář s čárou `─────`.

## Jak spustit / ověřit

- Otevřít projekt v Unity Hubu (6000.3.10f1) → `SampleScene` → **Play**.
- **MCP for Unity** je napojené na Claude Code (stdio; potřebuje běžící Unity +
  „Start Session" v okně *Window → MCP for Unity*). Přes něj jde číst Console,
  kompilovat, editovat scénu.
- Kompilace bez Unity: `bash .claude/skills/unity-hra/scripts/compile-check.sh`
  (Unity Roslyn + Bee response file). Skill **`unity-hra`** drží celý pracovní postup.
- Build ani CI nejsou nastavené. `companyName` v Player Settings je zatím
  `DefaultCompany` — před odevzdávaným buildem nastavit.

## Architektura

### Stav hry žije v `GameSession` (jediný povolený singleton)
- `GameSession.Instance.Data` (`GameData`) = veškerý ukládaný stav. Objekt má
  `DontDestroyOnLoad` → **přežívá přechod mezi scénami**, takže maják a hlavní
  scéna sdílí ta samá data.
- `GridManager.Awake()` načte save (`SaveManager.LoadGame()`) a předá ho do
  `GameSession` (`Ensure().SetData(...)`).
- `GameSession.Save()` zapíše na disk a vyvolá `OnDataChanged`.
- `GameSession.ShopPriceLevel` (static) — cenový level (0–20) ostrova, jehož
  obchod je zrovna otevřený; obchody podle něj škálují nákupní ceny.

### Svět = mřížka dlaždic (`GridManager`, ~1400 řádků, srdce hry)
- Svět je nekonečný. Dlaždice v `gameData.tileData` (`SerializableDictionary
  <string, TileStatus>`, klíč `"x,y"`). Ukládají se jen vygenerované/navštívené.
- `TileType` (enum v `TileData.cs`):
  `Empty=0, Water=1, Water_Fish=2, Treasure=3, Harbor=4, Pier=5, UpgradeShop=6,
  QuestShop=7, Lighthouse=8, Chest=9, MegaIsland=10`.
  **⚠️ Hodnoty se ukládají do save jako `int` — nikdy nepřečíslovat/nepřeházet.**
  `UpgradeShop`/`QuestShop` jsou **legacy** (nové ostrovy je nestaví, obchody jsou
  v majáku) — drží se kvůli starým savům.
- **Moře není po dlaždicích.** Obyčejná voda 3D objekt nemá — celé moře je jedna
  poloprůhledná plocha (`OceanSurface`) + členité dno (`SeaFloor`) + obloha s
  mraky (`SkyClouds`), vše jede za hráčem. 3D objekty mají jen ostrovy, ryby, vraky.
  `ACTIVE_GRID_SIZE = 28`.
- **Ostrovy** jsou organické (ne čtverce): `StampOrganicLand` — pevné jádro
  ~7–10² + náhodné rozrůstání + zaplnění zálivů. Generují se na mřížce každých
  **40** políček s **30 %** šancí, min. rozestup **200**. Každý ostrov: molo
  (`PlaceEdgePier` — dvoupolíčkový výběžek do vody), maják 2×2 (`PlaceLighthouse`
  — nesmí ostrov rozdělit ani stát u mola), s 25 % šancí bedna, klidná zóna
  `SPAWN_ISLAND_CLEARANCE=25` (žádné ryby/vraky/piráti blízko). ~20 % ostrovů je
  **nepřátelských** (`gameData.hostileIslands`) — mají dělo (`HostileIslandCannon`).
- Hladký terén ostrova = generovaný mesh (`IslandTerrain.Build` + `BuildGrass`).
- `OnWorldChanged` event → překreslení HUD, minimapy.
- **Mega ostrovy** (`PlaceMegaIsland`, typ `MegaIsland`) — velká příběhová plocha,
  zatím jen země + obelisk (`MegaIslandMarker`). Vede k nim příběh.

### Hráč (`PlayerController`, ~850 řádků)
- **Volný pohyb podle kamery** (ne skákání po políčkách): `Move(h,v)` jede ve
  směru, kam se dívá `viewCamera` (P1 = `Camera.main`, P2 dostane svou od
  `MultiplayerManager`). `moveSpeed`, `turnSpeed`.
- Grid pozice (`GridX/GridY`) se dopočítává z `transform.position` a slouží
  generování světa, mlze a interakcím.
- **Loď vs. pěšky**: `E` (P1) / `Numpad1` (P2) u mola. Když hráč vystoupí, loď
  **nezmizí** — zůstane plavat na místě (`parkedBoatGO`, health bar) a zase se
  odstraní při nasednutí.
- **Zdraví**: `boatHealth` + `playerHealth` (0–100, HUD nad minimapou). Když
  `boatHealth` klesne na 0 → `boatWrecked` = hráč **plave** ve vodě (pomalý,
  zranitelný), dokud loď neopraví v obchodě. `playerHealth` na 0 → **smrt**
  (`DeathScreen`: Respawn s veslicí na nejbližším ostrově — mince zůstanou,
  kořist + náboje + upgrady zmizí — nebo Hlavní menu).
- **Souboj**: střelba **levým tlačítkem myši** (P1) / `Numpad *` (P2), spotřebuje
  `ammo` (kupuje se v obchodě). `IsSailing` / `IsSwimming` — terč pro děla.
- `Space` / `Numpad0` = rybaření (`Water_Fish`) / těžba vraku (`Treasure`).
- `M` (P1) / `Numpad 2` (P2) = velká **mapa** (jen v lodi, jen s koupenou `hasMap`) —
  klik nastaví waypoint, na minimapě pak jinobarevná šipka.
- `R` (P1) / `Numpad /` (P2) = oprava naplavané lodě u mola / v obchodě.
- Per-hráč hodnoty jdou přes property (`GridX`, `PCoins`, `PBoatHealth`,
  `PAmmo`, `PQuest`…), které routují do `gameData.xxx` / `gameData.player2Xxx`
  podle `playerIndex`. **Nová per-hráč hodnota musí projít stejně** (property +
  `HUDCounter` + obchody).

### Ukládání (`SaveManager`, `GameData`)
- Statická třída, JSON přes `JsonUtility` do `Application.persistentDataPath`.
- **3 sloty**: `save_0.json`…`save_2.json`. `SaveManager.CurrentSlot`,
  `PlayerPrefs["LastSlot"]`.
- `GameData` je velký — obě ekonomiky (P1 + `player2*`), souboj (`ammo`,
  `hostileIslands`, `clearedIslands`, `pirateKills`), příběh (`storyStep`,
  `hasHistoricalTreasure`, `storyIsland*`), `megaQuest`, `openedChests`,
  `tileData`.
- `JsonUtility` **neumí** `Dictionary`, `null` kolekce ani polymorfismus. Nové
  pole = staré savy ho načtou jako `0`/`false`/prázdný list → počítej s tím.

### Ekonomika — `EconomyConfig` (všechna čísla na jednom místě)
- 1 ryba = 1 mince, 1 poklad = 5 mincí (drobný jistý příjem). Questy, piráti,
  bedny, mega quest = velké peníze. Velká loď = 1000 (dlouhodobý cíl).
- `ShipSmall=180, ShipMedium=450, ShipLarge=1000` (veslice → malá → střední →
  velká; `shipLevel` 0–3, viz `ShipModelSwitcher` / `BoatStats`).
- Nákupní ceny × `PriceMultiplier(islandPriceLevel)` (0.8–1.2×, deterministicky
  z pozice majáku). Výkup je všude stejný.

### Multiplayer (`MultiplayerManager`) — lokální split-screen
- `MultiplayerManager.IsMultiplayer` (static) — všude se podle toho větví.
- P2 = `Instantiate` kopie P1; z kopie se mažou komponenty, co mají být jednou
  (Camera, CameraFollow, HUDCounter, MinimapUIRenderer, GameConsole, PauseMenu,
  MainMenuManager, AudioListener). `ShipModelSwitcher` na P2 **zůstává**.
- P1 kamera → levá půlka, P2 kamera (nová) → pravá. `p2Player.viewCamera` = P2 kamera.
- Ekonomiky oddělené; převod peněz mezi hráči v `PauseMenu`.
- Ovládání: **P1 = WASD, E, Space, M, R, LMB, Esc**; **P2 = šipky, Numpad 1/0/2,
  Numpad `/`, Numpad `*`, Numpad Enter**.

### Maják (`LighthouseManager` + `LighthouseInterior` + `InteriorPlayer` / `InteriorInteractable`)
- Pěšky u majáku `E` → `LighthouseManager.Enter(playerIndex)`.
  - **Sólo**: `SceneManager.LoadScene` — plné přepnutí do `LighthouseInterior`.
  - **Coop**: additivní načtení — oba hráči jsou v jedné kulaté místnosti.
- Uvnitř jsou pulty: **výkupna** (prodej + vyplacení mega questu), **obchod s
  questy**, **obchod s vylepšeními**. Data přes `GameSession` (GridManager tam
  není). `UpgradeShopManager` / `QuestShopManager` fungují v obou scénách.
- Návrat: `GameSession.ReturningFromLighthouse` (1 snímek) → hlavní menu se
  nezobrazí, hra pokračuje.

### Souboj (`CombatDirector`, `PirateShip`, `HostileIslandCannon`, `CannonBall`)
- `CombatDirector.Ensure()` z `GridManager.Awake()` — spravuje spawn pirátů
  (od malých po velké lodě, boss-fight health bar) a děl nepřátelských ostrovů.
- Odměny za potopení v `EconomyConfig`.

### Příběh (`StoryNpc` — „starý námořník" / děda)
- Sedí u startovního ostrova (nad hladinou, políčko bez dekorace —
  `GridManager.ReserveNpcTile`). `E` = rozhovor (`IsTalkingWith` mrazí ovládání).
- `gameData.storyStep` řídí repliky: 0 start → 1 „kup si loď a přines mi 1000
  mincí + historický poklad" → 2 dostal souřadnice mega ostrova → 3 našel stopu → …
- „Historický poklad" (`hasHistoricalTreasure`) padá z vyplacení mega questu (~20 %).

### UI
- **IMGUI (`OnGUI`)**: `MainMenuManager`, `PauseMenu`, `GameConsole`,
  `UpgradeShopManager`, `QuestShopManager`, `DeathScreen`, `MapScreen`,
  `StoryNpc` dialog. Styly lazy v `InitStyles()`, pozadí tlačítek 1×1 texturou.
  Sdílený vzhled: `HudSkin` / `ShopUI`.
- **Runtime uGUI**: `HUDCounter` (mince/ryby/poklady/quest + health bary),
  `MinimapUIRenderer` (`Texture2D` po políčkách, **kruhová** — `MaskCircle`,
  kompas + waypoint šipka).

### Vstup blokovaný přes flagy (respektuj v novém ovládání)
`GameConsole.IsOpen`, `MainMenuManager.IsVisible`, `DeathScreen.IsOpen`,
`MapScreen.IsOpenFor(playerIndex)`, `UpgradeShopManager.AnyShopOpen` (static,
scanuje `FindObjectsByType`), per-hráč `…ShopManager.IsOpenForBuyer(playerIndex)`,
`StoryNpc.IsTalkingWith(playerIndex)`. **Ve split screenu obchod/mapa/dialog
jednoho hráče nemrazí druhého.**

### Herní konzole (cheaty) — `GameConsole`, klávesa `` ` ``
`get money/fish/treasure`, `get boat row/small/medium/large`,
`get item map/ammo/histtreasure/sellbonus/megamap`, `upgrade speed/rod/mining`,
`tp <x> <y>`, `explore [radius]`, `locate [fish/treasure/chest/island/quest]`,
`respawn`, `story [krok/island/histtreasure]`, `reset money`, `clear`.

## Konvence v kódu

- Wiring přes `FindFirstObjectByType<T>()` / `FindObjectsByType<T>()` v `Start()`.
- **Singletony**: povolený je `GameSession` (+ `CombatDirector`, `ChestManager` mají
  `Instance`) a statické flagy (`…IsOpen`, `IsMultiplayer`, `IsVisible`). Jinak ne.
- Nové skripty ve stejném stylu (české komentáře, žádný namespace, `─────` hlavičky).
- **Je to maturitní projekt** — radši delší jasný kód než chytrý trik; kód musí být
  obhajitelný u zkoušky.
- Po smysluplné změně gameplay logiky ověř, že se **zkompiluje** (MCP Console /
  `compile-check.sh`) — a řekni uživateli, co proklikat v Unity.

## Git

- Remote `origin` = `https://github.com/matixiiik/ProjektHra` (větev `main`).
  `gh` CLI přihlášené (`matixiiik`). Uživatel používá PowerShell 5.1 — `&&`
  tam nefunguje, dávat příkazy na samostatné řádky (nebo commituje Claude přes Bash).
- **UnityYAMLMerge** je nastavený v lokálním git configu — konflikty ve scénách/
  prefabech Git slévá chytře. Obnova po naklonování:
  ```
  git config merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p --force --fallback none %O %B %A %A'
  git config merge.unityyamlmerge.name "Unity SmartMerge (YAML)"
  git config merge.unityyamlmerge.recursive binary
  ```
- **Git LFS** zatím vypnuto. `.gitignore` ignoruje `Library/`, `Temp/`,
  `Assets/Screenshots/`, IDE soubory a buildy.
- Feature backlog + příběhové nápady: `Napady.txt` v kořeni repa.
