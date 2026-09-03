# Mapa skriptů — kdo za co odpovídá

Plný popis architektury je v `CLAUDE.md`. Tady je rychlá tabulka „chci sáhnout
na X → jdi do Y".

| Oblast | Soubor | Poznámka |
|---|---|---|
| Svět, generování ostrovů, dlaždice, mlha, ukládání | `GridManager.cs` | srdce hry; `OnWorldChanged` event |
| Data uložené hry | `GameData.cs` | P1 pole + `player2…` pole; `ActiveQuest` |
| Typy dlaždic | `TileData.cs` | `TileType` enum — **hodnoty nikdy nepřečíslovávat** (jsou v save) |
| Čtení/zápis save souboru | `SaveManager.cs` | 3 sloty `save_0/1/2.json`, `CurrentSlot` |
| Pohyb hráče, rybaření, těžba, přesedání loď↔pěšky | `PlayerController.cs` | jeden skript pro P1 i P2 (`playerIndex`) |
| Přepínání 3D modelu lodě | `ShipModelSwitcher.cs` | volej `Apply()` po změně `shipLevel` nebo stavu „pěšky" |
| Kamera za hráčem | `CameraFollow.cs` | drží fixní offset |
| Split-screen, klonování P2 | `MultiplayerManager.cs` | `IsMultiplayer` static; `StartMultiplayer()`/`Stop()` |
| Hlavní menu, výběr slotu | `MainMenuManager.cs` | `IsVisible` static; IMGUI |
| Pauza, převod peněz mezi hráči | `PauseMenu.cs` | IMGUI; `ExitGame/NewGame/ContinueGame` volají UI tlačítka — nepřejmenovat |
| Obchod s vylepšeními | `UpgradeShopManager.cs` | `AnyShopOpen` static; per-buyer metody |
| Obchod s questy + výkup | `QuestShopManager.cs` | šablony questů v `Templates` |
| HUD (ryby/poklady/mince/quest) | `HUDCounter.cs` | staví UI kódem; poslouchá `OnWorldChanged` |
| Minimapa | `MinimapUIRenderer.cs` | `Texture2D` po pixelech; poslouchá `OnWorldChanged` |
| Kroužek postupu práce | `WorkIndicator.cs` | čte `PlayerController.WorkProgress` |
| Vývojářská konzole (cheaty) | `GameConsole.cs` | klávesa `` ` ``; `IsOpen` static |
| ⚠️ Starý obchod — mrtvý kód | `HarborManager.cs` | nechat, visí na objektu ve scéně |

## Jak spolu skripty mluví

- **Wiring:** skoro všechno se hledá přes `FindFirstObjectByType<T>()` v `Start()`.
  Nová komponenta, kterou chce někdo najít, musí být na objektu ve scéně od začátku.
- **Signál „něco se změnilo":** `GridManager.OnWorldChanged`. Volá se z `GridManager`
  a přes `NotifyWorldChanged()` z obchodů, pauzy, konzole. Poslouchá HUD a minimapa.
  Po JAKÉKOLI změně mincí/ryb/questu/dlaždic ho zavolej.
- **Vstupní brány** (než přidáš klávesu, zkontroluj je v `PlayerController.Update`):
  `GameConsole.IsOpen`, `MainMenuManager.IsVisible`,
  `UpgradeShopManager.AnyShopOpen`, `upgradeShopManager.IsOpen`, `questShopManager.IsOpen`.
- **Pauza vs obchod:** když je `AnyShopOpen`, Esc patří obchodu, ne pauze.

## Souřadnice a mřížka

- Svět je nekonečná mřížka. Pozice = celá čísla `(x, y)`.
- 3D pozice objektu = `new Vector3(gridX, y, gridY)` (herní Y = Unity Z).
- Klíč do `tileData` = `"x,y"` jako text (`GridManager.GridKey`).
- `ACTIVE_GRID_SIZE = 15` → kolem hráče žije mřížka 31×31 dlaždic.

## Save formát — pravidla

- `JsonUtility` — jen `[Serializable]` třídy a `public` pole.
- Neumí `Dictionary` (proto `SerializableDictionary`), `null` kolekce, dědičnost.
- Nové pole v `GameData` = staré savy ho načtou jako `0`/`false`/`null` → počítej s tím.
- V multiplayeru přidej i `player2XXX` variantu a zapoj ji do routingu
  (`PlayerController` property, `HUDCounter.Refresh`, oba obchody).
