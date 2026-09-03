# UnityMCP — které nástroje na co

UnityMCP je připojené přes stdio, ale **funguje jen když je Unity Editor
otevřený a v okně „MCP For Unity" je kliknuté „Start Session"**. Chyba
„Claude CLI not found" v tom okně je kosmetická, session i tak jede.

Rychlý test připojení: `mcp__UnityMCP__read_console` s `action: get`.
Když vrátí „No Unity Editor instances found" → Unity není připojené, jeď Postupem B.

## Ověření kódu / chyby

| Chci | Nástroj | Pozn. |
|---|---|---|
| Přeložit po úpravě | `refresh_unity` `compile: request`, `wait_for_ready: true` | počká, než je editor „ready" |
| Chyby a varování | `read_console` `types: ["error"]` / `["warning"]` `format: plain` | `count` posílej jako text `"20"` |
| Rychlá validace jednoho skriptu | `validate_script` `uri: "Assets/TutorialInfo/Scripts/X.cs"` `level: standard` | nespoléhej jen na tohle, nedělá cross-file kontrolu |
| Vyčistit konzoli před testem | `read_console` `action: clear` | jen UI stav, nic v projektu |

## Testování za běhu

| Chci | Nástroj |
|---|---|
| Spustit hru | `manage_editor` `action: play` |
| Zastavit | `manage_editor` `action: stop` |
| Pauza | `manage_editor` `action: pause` |
| Runtime chyby | po `play` chvíli počkej, pak `read_console` `types: ["error"]` |
| Spustit testy | `run_tests` `mode: EditMode` → poll `get_test_job` |

`play` v editoru NEDÁ vědět, jestli hra „funguje" — jen jestli nespadla.
Skutečné proklikání (obchody, split-screen, save/load, questy) nech na uživateli.

## Scéna a objekty (používej opatrně — mění projekt)

| Chci | Nástroj |
|---|---|
| Najít objekt ve scéně | `find_gameobjects` / `manage_gameobject` |
| Přidat/nastavit komponentu | `manage_components` |
| Zapojit serializované pole | `manage_gameobject` (set property) |
| Vrstvy / tagy | `manage_editor` `add_layer` / `add_tag` |

**Před každou změnou scény** si přečti aktuální stav (`mcpforunity://editor/state`,
`find_gameobjects`) a **řekni uživateli, co chceš změnit, než to uděláš** — zásah
do scény je hůř vratný než zásah do kódu. U maturitního projektu radši uživateli
naklikání popiš (Postup B) a scénu měň přes MCP jen když si to výslovně přeje.

## Co PŘES MCP nedělat

- Neinstaluj balíčky (`manage_packages`) bez souhlasu — mění `manifest.json`.
- Nespouštěj `manage_editor deploy_package` / `restore_package`.
- Negeneruj assety (`generate_image/model/audio`) bez zadání od uživatele.
