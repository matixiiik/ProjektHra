# ProjektHra — Lodní dobrodružství

2D/3D Unity hra: hráč pluje po nekonečném oceánu v lodi, rybaří, těží poklady,
kupuje vylepšení a plní questy na ostrovech. Podporuje lokální split-screen pro
dva hráče.

## Technické základy

- **Unity 6000.3.10f1**, Universal Render Pipeline 17.3.0
- Balíček `com.unity.inputsystem` je nainstalovaný, ale **kód používá starý
  `UnityEngine.Input`** (`Input.GetKey…`). Projekt má Input Handling = *Both*.
- Jediná scéna: `Assets/Scenes/SampleScene.unity`
- Veškerý herní kód: `Assets/TutorialInfo/Scripts/*.cs` (název složky je pozůstatek
  z Unity šablony — není to tutoriál)
- **Žádný namespace** — všechny třídy jsou globální
- **Žádné testy** (balíček test-framework je, ale suita neexistuje)
- Komentáře a herní texty jsou **česky**

## Jak spustit

Otevřít projekt v Unity Hubu (verze 6000.3.10f1) → otevřít `SampleScene` → **Play**.
Build ani CI zatím nejsou nastavené.

## Architektura

### Svět = mřížka dlaždic
- `GridManager` je srdce hry. Svět je nekonečný, dlaždice se generují líně kolem
  hráče (`ACTIVE_GRID_SIZE = 15` na každou stranu) a daleké se uklízejí.
- Dlaždice jsou v `Dictionary<string, TileStatus>` s klíčem `"x,y"` (viz `GridKey`).
- `TileType` (enum v `TileData.cs`): `Empty=0, Water=1, Water_Fish=2, Treasure=3,
  Harbor=4, Pier=5, UpgradeShop=6, QuestShop=7`.
  **⚠️ Hodnoty se ukládají do save jako `int` — nikdy nepřečíslovat ani nepřeházet.**
- Ostrovy: bloky 10×10 `Harbor`, generují se na souřadnicích dělitelných 20 s 10%
  šancí, min. vzdálenost mezi ostrovy 50. Každý ostrov má 2 dlaždice `Pier` (molo)
  a dva 2×2 obchody (`UpgradeShop`, `QuestShop`) na protější straně než molo.
- `OnWorldChanged` event → překreslení HUD, minimapy atd.

### Hráč (`PlayerController`)
- Pohyb po celých políčkách; `moveSpeed` je jen rychlost plynulé animace mezi nimi.
- **Loď vs. pěšky**: `E` (P1) / `Numpad1` (P2) u mola přepíná. V lodi se pluje po
  vodě, pěšky se chodí po `Harbor`/`Pier`. Pozice lodě se pamatuje zvlášť
  (`boatGridX/Y`), aby se hráč mohl vrátit.
- Speed upgrade v lodi = krok 2 políčka (viz `step` v `Update`).
- `Space` / `Numpad0` = interakce: rybaření na `Water_Fish`, těžba na `Treasure`
  (coroutiny `FishingRoutine` / `MineRoutine`).
- Všechny per-hráč hodnoty jdou přes property (`GridX`, `PCoins`, `PFishCount`…),
  které routují do `gameData.xxx` nebo `gameData.player2Xxx` podle `playerIndex`.

### Ukládání (`SaveManager`, `GameData`)
- Statická třída, JSON přes `JsonUtility` do `Application.persistentDataPath`.
- **3 nezávislé sloty**: `save_0.json`, `save_1.json`, `save_2.json`.
  `SaveManager.CurrentSlot` říká, se kterým se pracuje; `PlayerPrefs["LastSlot"]`
  pamatuje poslední.
- `GameData` drží obě ekonomiky (P1 pole + `player2*` pole) a `tileData`.
- `tileData` je `SerializableDictionary<K,V>` — vlastní třída, co přes
  `ISerializationCallbackReceiver` serializuje slovník do dvou `List`ů.
- `JsonUtility` neumí `Dictionary`, `null` kolekce ani polymorfismus — na to pozor
  při přidávání polí do save.

### Multiplayer (`MultiplayerManager`) — lokální split-screen
- `MultiplayerManager.IsMultiplayer` (static) — všude se podle toho větví.
- P2 hráč vzniká jako `Instantiate` kopie P1 GameObjectu; pak se z něj mažou
  komponenty, co mají být ve scéně jen jednou (Camera, CameraFollow, HUDCounter,
  MinimapUIRenderer, GameConsole, PauseMenu, MainMenuManager, AudioListener).
  `ShipModelSwitcher` na P2 **zůstává** (řídí jeho loď).
- P1 kamera → levá půlka obrazovky, P2 kamera (nově vytvořená) → pravá.
- Ekonomiky jsou oddělené; převod peněz mezi hráči je v `PauseMenu`.
- Ovládání: P1 = WASD + E + Space + Esc, P2 = šipky + Numpad1 + Numpad0 + NumpadEnter.

### UI
- **IMGUI (`OnGUI` / `GUILayout`)**: `MainMenuManager`, `GameConsole`,
  `UpgradeShopManager`, `QuestShopManager`, `PauseMenu`. Styly se staví lazy
  v `InitStyles()` a barevné pozadí tlačítek se dělá 1×1 texturou (`MakeTex`).
- **uGUI (`UnityEngine.UI`) stavěné za běhu kódem**: `HUDCounter`, `MinimapUIRenderer`
  (když nemají přiřazený canvas/image, vytvoří si vlastní).
- Minimapa = `Texture2D` překreslovaná po políčkách z `tileData`, s mlhou
  (`isExplored`).

### Vstup je blokovaný přes statické flagy
Než přidáš nové klávesové ovládání, respektuj tyhle brány (viz `PlayerController.Update`):
`GameConsole.IsOpen`, `MainMenuManager.IsVisible`, `UpgradeShopManager.AnyShopOpen`
/ `.IsOpen`, `QuestShopManager.IsOpen`.

### Herní konzole (cheaty)
`GameConsole` — klávesa `` ` `` (BackQuote). Příkazy: `get money/fish/treasure/boat`,
`upgrade speed/rod/mining`, `tp <x> <y>`, `explore [radius]`, `reset money`, `clear`.

### Lodě
`ShipModelSwitcher` na objektu hráče — podle `shipLevel` (0/1/2) zapne
`shipSmall/shipMedium/shipLarge` a ostatní vypne; pěšky vypne všechny. Po změně
`shipLevel` nebo stavu „pěšky" volej `Apply()`.

## Konvence v kódu

- Wiring komponent přes `FindFirstObjectByType<T>()` / `FindObjectsByType<T>()`
  v `Start()` (ne `GetComponent` řetězce, ne singletony kromě statických flagů).
- Hlavičky souborů a sekcí jako komentář s čárou `─────`.
- Nové skripty psát ve stejném stylu (české komentáře, žádný namespace).
- Po smysluplné změně gameplay logiky ověř, že se projekt **zkompiluje v Unity**
  (Console bez chyb) — CLI kompilace tu není.

## Git

- Remote `origin` = `https://github.com/matixiiik/ProjektHra` (větev `main`).
- `gh` CLI je nainstalované a přihlášené (účet `matixiiik`).
- **UnityYAMLMerge** je nastavený v lokálním git configu — konflikty ve scénách
  a prefabech (`.unity`, `.prefab`, `.asset`) Git slévá chytře. Kdyby se config
  ztratil (nové naklonování repa), obnovit:
  ```
  git config merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.3.10f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p --force --fallback none %O %B %A %A'
  git config merge.unityyamlmerge.name "Unity SmartMerge (YAML)"
  git config merge.unityyamlmerge.recursive binary
  ```
- **Git LFS** (zatím vypnuto): až přibudou velké binárky, přidat do `.gitattributes`
  řádky typu `*.png filter=lfs diff=lfs merge=lfs -text`, pak **jednou**
  `git lfs migrate import --include="*.png,*.fbx,*.wav" --everything` a force-push.
- Složka `Assets/_Recovery/` = automatické zálohy Unity, ne skutečná práce.
