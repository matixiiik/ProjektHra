# Recepty na typické úpravy

Konkrétní postupy pro věci, které se v téhle hře přidávají nejčastěji.
Vždy platí: české komentáře, ověřit kompilaci, napsat uživateli kroky pro Unity.

---

## 1. Nový typ dlaždice (např. „Whirlpool")

1. `TileData.cs` — přidej hodnotu do `TileType` **na konec** (další volné číslo).
   Nikdy nevkládej doprostřed a nepřečíslovávej — čísla jsou v save.
2. `GridManager.cs`:
   - `GetPrefabForType()` — přidej `case` s novým prefabem
   - přidej `public GameObject xxxPrefab;` k ostatním prefabům nahoře
   - pokud se generuje náhodně: `GenerateRandomSeaType()` nebo `CheckAndGenerateArea()`
   - pokud je „důležitá" (nemá se maza z save): `CleanupWorldData()` + `IsIslandTile()` dle potřeby
3. `PlayerController.cs` — `CanEnter()` pokud se po ní dá / nedá plout;
   `TryInteract()` pokud se s ní dá něco dělat.
4. `MinimapUIRenderer.cs` — `GetTileColor()` + nová `public Color xxxColor`.
5. **Unity:** vytvořit prefab dlaždice (klidně kopií existující), přetáhnout ho
   do nového políčka na objektu **GridManager** v Inspektoru.

---

## 2. Nový upgrade v obchodě s vylepšeními

1. `GameData.cs` — `public bool hasXxxUpgrade;` **a** `public bool player2HasXxxUpgrade;`
2. `UpgradeShopManager.cs`:
   - `public int xxxUpgradeCost = ...;`
   - rozšiř `GetUpgrade(int t)` / `SetUpgrade(int t, bool v)` o nový index `t == 3`
   - v `OnGUI()` přidej `DrawRow("popis", xxxUpgradeCost, GetUpgrade(3), () => TryBuyUpgrade(3, xxxUpgradeCost));`
3. `PlayerController.cs` — přidej property `bool PHasXxxUpgrade => playerIndex == 0 ? …hasXxxUpgrade : …player2HasXxxUpgrade;`
   a použij ji tam, kde má upgrade efekt.
4. `GameConsole.cs` — `HandleUpgrade()` přidej `case "xxx":` (ať se dá testovat).
5. **Unity:** nic (cena se dá přenastavit v Inspektoru na objektu s UpgradeShopManager, ale default v kódu stačí).

---

## 3. Nový příkaz konzole

1. `GameConsole.cs`:
   - `ExecuteCommand()` switch — přidej `case "xxx": HandleXxx(p); break;`
   - napiš metodu `void HandleXxx(string[] p)` (vzor: `HandleGet`, ověřuj `p.Length`)
   - do `help` textu přidej řádek
   - na konci úspěšné akce zavolej `grid.Save(); grid.NotifyWorldChanged();`
2. Kompilace, hotovo (konzole nic ve scéně nepotřebuje).

---

## 4. Nová klávesa / ovládání

1. `PlayerController.cs` `Update()`:
   - **napřed zkontroluj vstupní brány** — ten `if (… || GameConsole.IsOpen || MainMenuManager.IsVisible) return;` už tam je, drž se nad ním / pod ním konzistentně
   - pro akci obou hráčů použij `KeyDown(KeyCode.KlávesaP1, KeyCode.KlávesaP2)`
   - P2 klávesy jsou šipky + numpad (Numpad0/1, NumpadEnter)
2. Když klávesa ovládá UI (ne hráče), dej ji do toho konkrétního skriptu
   (obchod, pauza, konzole) a respektuj jeho `isOpen`.
3. **Unity:** nic. (Projekt používá starý `Input.GetKey`, `activeInputHandler` je „Both".)

---

## 5. Nový prvek v HUD

1. `HUDCounter.cs`:
   - v `BuildHUD()` vytvoř Text/panel (vzor: `MakeRow`, `BuildQuestPanel`)
   - v `Refresh()` mu nastav text z `grid.gameData` (nezapomeň P1/P2 přes `playerIndex`)
   - pokud se má prvek posouvat při split-screenu, přidej jeho `RectTransform` do
     `UpdateLayout()`
2. HUD se překresluje na `OnWorldChanged` — když nový prvek závisí na hodnotě,
   která se mění jinde, ověř, že se tam volá `NotifyWorldChanged()`.
3. **Unity:** nic (HUD se staví celý kódem).

---

## 6. Nový typ questu

1. `GameData.cs` `ActiveQuest.questType` — dnes 0 = ryby, 1 = poklady. Přidej 2 = …
2. `QuestShopManager.cs`:
   - `Templates` — přidej řádky s `type = 2`
   - `GenerateOffers()` — `desc` pro nový typ
3. `PlayerController.cs` — tam, kde se plní pokrok questu
   (`FishingRoutine`, `MineRoutine` nebo nová akce), přidej větev
   `if (q.hasQuest && q.questType == 2) q.progress = Mathf.Min(q.progress + …, q.target);`
4. **Unity:** nic.

---

## 7. Nové ukládané pole (obecně)

1. `GameData.cs` — `public <typ> xxx;` (+ `player2Xxx` když je to per-hráč).
   Jen typy, co zvládne `JsonUtility` (int, float, bool, string, `[Serializable]` třída, `List` z nich).
2. Napoj čtení/zápis všude, kde se to má projevit.
3. Staré savy pole nemají → načte se výchozí hodnota. Pokud to vadí, ošetři
   „migraci" při načtení (vzor: `FishingRoutine` má `if (tile.fishRemaining <= 0) tile.fishRemaining = 3;`).
4. **Unity:** nic — ale řekni uživateli, ať otestuje i **načtení staré hry**,
   ne jen novou.

---

## Když si nejsi jistý, jestli je zásah „bezpečný pro hratelnost"

Zeptej se uživatele. Typicky bezpečné: komentáře, přejmenování lokálních
proměnných, vytažení pomocné metody, mazání `private` nepoužitých polí.
Typicky NENÍ bezpečné bez souhlasu: čísla (ceny, časy, pravděpodobnosti,
rychlosti), pořadí podmínek v pohybu, `TileType` hodnoty, formát save.
