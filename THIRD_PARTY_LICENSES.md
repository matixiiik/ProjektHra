# Podklady třetích stran

Vlastní zdrojový kód hry (`Assets/Scripts`, `Assets/Tests`) je pod licencí MIT
(viz [LICENSE](LICENSE)). Následující cizí podklady mají vlastní licence.
Všechny jsou **CC0 1.0** (Public Domain) — použití včetně komerčního je
povoleno bez uvedení autora; autoři jsou přesto uvedeni z úcty.

| Podklad | Autor / zdroj | Licence | Kde v projektu | K čemu ve hře |
|---|---|---|---|---|
| Pirate Kit 2.1 | Kenney — https://kenney.nl | CC0 1.0 | `Assets/Kenney/PirateKit`, `Assets/KenneyBoat`, `Assets/Resources/PirateKit`, `Assets/Resources/IslandDecor`, `Assets/Resources/PirateShips`, `Assets/Resources/GhostShip` | lodě, děla, hradby, věže, dekorace ostrovů, pirátské lodě, Bludný Holanďan |
| Mini Characters 1.0 | Kenney — https://kenney.nl | CC0 1.0 | `Assets/Resources/Characters` | model hráče a postav |
| Animated Fish Pack (žralok) | Quaternius — https://quaternius.com | CC0 1.0 | `Assets/Resources/SeaMonster` | mořská obluda |
| Zvuky | vlastní | vlastní dílo (MIT) | `Assets/Scripts/SoundManager.cs` | zvuky generované kódem (sinusovky a šum), žádné audio soubory |

Ve hře se nepoužívají žádné cizí fonty, textury (kromě `colormap.png` z kitů
výše) ani hudba.

## Nástroje a balíčky (nejsou součástí hry)

| Balíček | Licence | Poznámka |
|---|---|---|
| Unity 6 (6000.3.10f1), URP, Input System, Test Framework, uGUI, Timeline, AI Navigation | Unity (licence editoru a balíčků Unity) | engine a jeho balíčky |
| MCP for Unity (CoplayDev) | MIT | pouze vývojový nástroj v editoru, do buildu nejde |

## Poznámka k původu modelů

Modely v `Resources/GhostShip` a `Resources/PirateShips` používají texturu
`colormap.png` ve stylu Kenney Pirate Kit, proto jsou zařazeny pod Kenney.
Licenční soubory jsou přiloženy u modelů (`License*.txt`).
