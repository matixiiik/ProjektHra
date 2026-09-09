# Handoff — kde jsme skončili (2026-09-08 večer)

Tenhle soubor je most mezi počítači. Claude paměť se nesyncuje přes git, tak si
sem Claude píše, kde se přestalo, aby se dalo pokračovat i z notebooku.

> **Claude: přečti si tohle na začátku session a rovnou navaž. Až se kus práce
> udělá, tenhle soubor aktualizuj a zacommituj.**

---

## STAV 2026-09-09 — vše commitnuté a pushnuté, working tree čistý
(kromě `Napady.txt`, který si edituje uživatel — necommitovat za něj)

### DÁVKA: SYMETRICKÝ MAJÁK + ZAČÁTEK PŘÍBĚHU (2026-09-09) — HOTOVO, OTESTOVÁNO přes MCP

**Vizuál:**
- `LighthouseInteriorDecor` — kompletně přepsané na **symetrický** layout (vše
  zrcadleno podle osy Z): 3 pulty (prostřední ve středu), 2 ohřívadla, 2 lucerny,
  2 květiny, 2 bedny, kulatý kobereček + lampa uprostřed. Náhodné bedny/sudy/rug
  ze scény se schovají (`HideSceneClutter`).
- `MinimapUIRenderer` — **HP bary** přepsané na `Image.Type.Filled`, oba přesně
  stejné rozměry → 100% zarovnané, užší než minimapa. `HudSkin.White()`.
- Ostrovy: jádro **7–10** (min 7×7, dřív 5×5), `ISLAND_CANVAS 16→20`, guard 25→49.
- `IslandDecor` — dekorace jen **šachovnicově** (každá druhá dlaždice) → nikdy dvě
  u sebe; `decorChance 0.30`.

**Příběh (starý námořník) — ZAČÁTEK, pokračování přidá uživatel:**
- `GameData`: `storyStep` (0–4+), `hasHistoricalTreasure`, `storyIslandActive/X/Y`
  (sdílené pro oba hráče). `MegaQuest.grantsHistoricalTreasure`.
- `TileType.MegaIsland = 10` (na KONEC enumu). `IsMeshLandTile` + `IsIslandTile`
  + `PlayerController.CanEnter` ho berou jako pevninu. Minimapa/mapa fialově.
- `GridManager.PlaceMegaIsland(x,y)` — velká kruhová plocha (~R13, ~520 dlaždic)
  + 2 dlaždice mola na kraji přivráceném ke světu + obelisk uprostřed
  (`MegaIslandMarker` — hák pro budoucí obsah). Terén se staví přes IslandTerrain.
  Obnova markeru po loadu v `GridManager.Awake`. `CleanupIslandTerrains` upraven,
  ať mega ostrov (bez per-dlaždicových objektů) nemizí.
- **`StoryNpc`** — state machine podle `storyStep`:
  - 0: nemáš loď → "kup si aspoň malou"; máš loď → chce se prokázat → step 1
  - 1: přines **1000 mincí + historický poklad**; když máš obojí → tlačítko
    "Dát mu…" → `GiveToSailor()` odečte, `PlaceMegaIsland` na daleké deterministické
    místo (340–467 políček), nastaví `storyStep=2` + waypoint na minimapě
  - 2: připomíná souřadnice. Dopluješ do 16 políček od středu → `OnReachedStoryIsland()`
    → `storyStep=3`, waypoint zmizí, toast "někdo tu už kopal, na obelisku vzkaz"
  - 3: "poklad je pryč, nechal ti stopu" → `storyStep=4` (čeká na další obsah)
- **Historický poklad**: `ChestManager` — 20 % mega questů má `grantsHistoricalTreasure`;
  `QuestShopManager.ClaimMega` ho pak dá (`hasHistoricalTreasure=true`). Ve výkupně
  se píše "Neses: HISTORICKY POKLAD".
- **HUD**: `HUDCounter` nový příběhový panel nahoře uprostřed pod quest panelem
  (`storyStep` 1/2/3 → text cíle; v kroku 2 souřadnice ostrova).
- **Konzole**: `story` (stav), `story <0-9>`, `story island`, `story histtreasure`.

### DALŠÍ KROK PŘÍBĚHU (čeká na zadání)
- Co přesně musí hráč na mega ostrově splnit (teď je to jen země + obelisk).
- Rozluštění "stopy" a kam vede dál. `MegaIslandMarker` je připravený hák.
- Mega ostrovy mají být "každý jiný" — zatím je jeden, generovaný stejně.

---

### DÁVKA UI/UX #2 (2026-09-09) — HOTOVO, OTESTOVÁNO přes MCP

1. **Maják: 3 pulty + větší prodavači.** `LighthouseInteriorDecor` teď dělá TŘI
   pulty vedle sebe podél zadní stěny + prodavače v tričku barvy pultu:
   modrý (vylepšení), oranžový (questy), **nový ZELENÝ (výkupna)**. Prodavači
   jsou větší a stojí ZA pultem (u zdi), pult je otočený čelem do místnosti a
   posunutý víc do místnosti. Krb se kvůli tomu přesunul k levé stěně.
   `WALK_RADIUS 2.3`.
2. **Prodej má vlastní obchod.** `QuestShopManager` řídí OBA pulty jedním
   skriptem přes `sellMode[2]`:
   - `Open(idx, sellMode: true)` → **VÝKUPNA** (zelený proužek): prodej ryb/
     pokladů + vyplacení mega questu.
   - `Open(idx, sellMode: false)` → **OBCHOD S QUESTY** (oranžový): jen questy.
   - `InteriorInteractable` má novou akci `QuestShopSell` (přidaná NA KONEC enumu
     — pozor, `Exit` se serializuje jako číslo!). `Counter_Sell` je nový objekt
     s touhle akcí, `LighthouseInteriorDecor` ho vytvoří (+ MoveGameObjectToScene
     kvůli coopu).
   - Starý QuestShop dílek ve SampleScene (staré savy) → `Open(idx)` = quest mód.
3. **Minimapa: S/J/V/Z** — čtyři písmena při okraji zevnitř (`CreateCompassLabels`).
4. **Minimapa: bílá tečka → šipka lodě.** `MinimapUIRenderer.playerArrowRT` +
   `Update()` ji otáčí podle `PlayerController.HeadingDegrees` (natočení modelu
   lodě / panáčka). Dá se podle ní řídit.
5. **Postava ještě níž.** `HeadDot` ve SampleScene `localPosition.y -0.54`
   (nohy trochu v zemi, ať nelítá).
6. **Rybí dlaždice = hejno + kruhy.** NOVÝ `FishSpot.cs` na `WaterFishPrefab`
   (komponenta přidaná do .prefab). Placatý čtverec → sotva znatelný kulatý
   "hlubší" flek + 3 stříbřité rybky kroužící pod hladinou + 2 rozšiřující se
   kruhy (LineRenderer). Vyrovnává zploštění prefabu přes pomocný `content`.
   Vše procedurální, žádný externí soubor.

### PŘÍPADNÉ DOLADĚNÍ
- Krb u levé stěny je částečně schovaný za květinou/lucernou.
- Rybí flek: kulatý disc je hodně jemný — dá se zvýraznit.

---

### VELKÁ DÁVKA UI/UX (2026-09-09) — HOTOVO, OTESTOVÁNO přes MCP

Uživatelův seznam po kouskách. Vše ověřeno v play mode (screenshoty), 0 chyb.

1. **Prodavači v majáku.** `LighthouseInteriorDecor` teď z modré/oranžové kostky
   pultu (`Counter_Upgrade`/`Counter_Quest`) udělá dřevěný pult + stojícího
   panáčka v tričku barvy obchodu (modrá = vylepšení, oranžová = questy).
   Původní kostka se jen schová (`MeshRenderer.enabled=false`), `InteriorInteractable`
   zůstává → interakce beze změny. `CounterDressing` se srovná na podlahu
   (`root.position.y = 0`, pult měl počátek ~0.5 nad zemí).
2. **Chození jen po koberci.** `LighthouseInteriorDecor.ClampWalkArea()` nastaví
   všem `InteriorPlayer.areaRadius = 2.6` (kobereček) a bodům zájmu `range = 2.2`,
   ať na ně hráč z kraje koberce dosáhne. Nedá se vejít do krbu ani do pultu.
3. **Scroll v obchodech.** `UpgradeShopManager`/`QuestShopManager` — sortiment je
   v `GUILayout.BeginScrollView` (kolečko funguje samo). `ShopUI.HandleDragScroll`
   navíc: podrž levé tlačítko a táhni myší = scroll. Panel se navíc zmenší, když
   je obrazovka nízká (`h = Min(…, Screen.height - 24)`), takže spodek nabídky
   půjde vždycky vidět.
4. **Mrtvá zóna kolem ostrova 50 → 25** (`GridManager.SPAWN_ISLAND_CLEARANCE`).
5. **Konzole: `locate`** [fish/treasure/chest/island/quest] — vypíše směr
   (S/J/V/Z) + vzdálenost + souřadnice nejbližší věci. Bez argumentu vypíše
   nejbližší od každého druhu. **`respawn`** — jako tlačítko na obrazovce smrti
   (veslice u nejbližšího ostrova, kořist pryč, mince zůstanou).
6. **Mapa přes M.** NOVÝ `MapScreen.cs`. V lodi (`!isOnFoot && !boatWrecked`)
   s koupenou mapou (`hasMap`): **M** (P1) / **Numpad 2** (P2) otevře velkou mapu
   přes celou obrazovku (ve split screenu půlku). Uprostřed jsi ty, kreslí se
   prozkoumané okolí jako na minimapě (mlha = šedá). Táhnutí myší = posun,
   kolečko = zoom, klik = waypoint (klik na stávající waypoint ho zruší). Mapa
   NEpauzuje hru (kvůli coopu) — `MapScreen.IsOpenFor(idx)` mrazí jen toho hráče.
   - `GameData`: `hasWaypoint/waypointX/waypointY` (+ player2). Ukládá se.
   - `MinimapUIRenderer`: azurová šipka (dřív "k nejbližšímu ostrovu") teď vede
     **k waypointu**; waypoint se navíc kreslí jako azurový bod na minimapě.
   - `PlayerController.ClearWaypointIfReached` — u waypointu (±1) se cíl splní/zmizí.
   - `PauseMenu` má guard `if (MapScreen.IsOpen) return;` (Esc zavírá mapu).
   - Obchod: řádek "Mapa" má nový popis.
7. **Postava menší.** `HeadDot` ve `SampleScene` má `localScale 0.82` (P2 klon to
   zdědí), `localPosition.y -0.44` (nohy na zemi). Jen hlavní postava, ne děda.
8. **Kulatá minimapa.** `MinimapUIRenderer.MaskCircle()` — rohy textury
   zprůhlední, po obvodu nakreslí kruhový rámeček. `borderColor` teplá mosaz.
9. **Hezčí HUD.** NOVÝ `HudSkin.cs` — procedurálně (Texture2D v kódu, jako
   SoundManager u zvuků) generuje: zaoblený 9-slice panel (tmavé dřevo + mosazný
   lem) a ikonky (mince/ryba/poklad/náboj/srdce/kotva). `HUDCounter` a health bary
   v `MinimapUIRenderer` je používají. Styl laděný ke Kenney grafice hry.
   Žádné externí soubory.

### PŘÍPADNÉ DOLADĚNÍ
- Prodavači v majáku jsou trochu schovaní za pultem — dalo by se je zvednout.
- Minimapa má nízké rozlišení (`viewRadius 25` → 51px), kruh je "kostičkovaný".
- Mapa přes M: waypoint se dá dát i do neprozkoumané mlhy (schválně — plánuješ
  trasu). Bez zoomu na celý svět (jen okolí + posun).

---

### ZÚTULNĚNÍ MAJÁKU + VZHLED OSTROVA + DĚDOVO POLÍČKO (2026-09-09) — HOTOVO, OTESTOVÁNO přes MCP

Uživatel: "zútulni interiér majáku a i jak vypadá ostrov, na políčku kde je děda
nic nesmí být." Vše čistě vizuál + drobná logika, nulový dopad na hratelnost.

1. **Ostrov má trávu.** `IslandTerrain.BuildGrass()` — druhý mesh navrch písku,
   jen ve vnitřku ostrova (u pláže se plynule zaryje pod písek, žádná hrana).
   `GridManager.EnsureIslandTerrain` k pískovému meshi přidá dítě "IslandGrass"
   se zeleným materiálem (odvozený jednou z `islandTerrainMaterial`, jen zelený —
   `IslandGrassMaterial()`). Ladí se `GRASS_INSET 0.5` / `GRASS_FEATHER 1.1` /
   `GRASS_LIFT 0.05` v `IslandTerrain.cs`.
2. **Dědovo políčko je holé.** `GridManager.npcClearTile` + `ReserveNpcTile(x,y)` +
   `StripTileDecor()` (vypne `Decor_*`, zničí `DecorExtra`). `StoryNpc.TryPlace`
   po usazení zavolá `ReserveNpcTile` — platí i po opětovném vygenerování dlaždice.
   Navíc: `StoryNpc` čeká 12 snímků, než dědu poprvé postaví, a pak 3 s po startu
   6× kontroluje (`TileStillGood`), že mu políčko nezůstalo na vodě — jinak ho
   přesadí (`ClearFigure` + nový `TryPlace`). Řešilo to, že se děda občas usadil
   na okraji, který se dogeneroval na vodu.
3. **Interiér majáku zútulněn.** NOVÝ `LighthouseInteriorDecor.cs` — sedí na
   objektu "InteriorManagers" ve scéně `LighthouseInterior` (přidán + scéna
   uložena). V `Start()` staví z primitivů: krb s mihotavým ohněm (u zadní stěny
   mezi pulty), závěsná lampa nad středem, 2 nástěnné lucerny, 2 květiny
   v květináči, kobereček u vchodu. Taky zteplí studené modré výplňové světlo
   scény + přidá teplý přísvit. `Flicker` (vnořená třída) = Perlin mihotání ohně.
   V coopu se propíše sám (děti posouvaného rootu, staví se až v Start()).
   Pozn.: Unity při uložení scény vyhodil staré serializované `…Cost` pole
   z obou shopů (zbytek po EconomyConfig dávce — pole už v kódu nejsou).

---

### PLAY-TEST + OPRAVY dávek 1 a 2 (2026-09-09, MCP zase jede) — HOTOVO, OTESTOVÁNO
Projeto přes MCP (reflexe + screenshoty). Kompilace 0 chyb/varování.

**Ověřeno funkční:** ekonomika (ceny/výkup přes EconomyConfig), smrt→DeathScreen→
respawn (mince + mega quest zůstanou, zbytek pryč, ship→0, veslice u nejbližšího
mola), rozbitá loď→plavání 0.28×→doplavat na molo→auto-výlov, oprava v obchodě
(`FixBoat`→`boatNeedsRehome`→loď se přemístí k molu).

**Opraveno (6 věcí, jen vizuál + čísla, nulový dopad na pravidla):**
1. `IslandDecor.EXTRA_PALM_SCALE = 0.38` — Kenney `palm-bend` byl 9 j (větší než
   ostrov), teď ~1.5 j. Vestavěná `Decor_Palm` má dál `PALM_SCALE 2.1`.
2. `PirateShip.BuildKenneyModel` — materiály zvlášť pro trup (tmavé dřevo) /
   plachty (plátno) / vlajky (rudá), sdílené staticky. Dřív jeden tmavý → šmouha.
3. Pirátské lodě menší (0.30/0.38/0.48 místo 0.42/0.55/0.7) + Model child
   `localPosition.y = -0.10` → kus trupu pod hladinou, neplave nad vodou.
4. `IslandDecor` — vyhozeny `grass`, `grass-plant`, `patch-grass-foliage`,
   `patch-sand-foliage` (jiný Kenney balíček, špatné UV vůči pirátskému atlasu +
   6-7 j velké). Zůstávají kameny + `palm-bend`.
5. `PlayerController.TryToggleBoatFoot` — E pěšky u rozbité lodě teď hodí toast
   "oprav ji v obchode s vylepsenimi (v majaku)" (dřív ticho).
6. `EconomyConfig` — `PriceMultiplier` teď `0.8–1.2` symetricky kolem 1.0
   (základní ceny = průměr, ne minimum). `SellBonusPerItem 3→2` (u ryby za 1 minci
   byl +3 moc; teď ryba 3 / poklad 7 po mega questu).

Pozn.: červený „artefakt" u majáku ve starších screenech byl `MapIcon` quad —
je na vrstvě `MinimapOnly`, hlavní kamera ho NEvidí, jen můj debug snímek s vlastní
kamerou. Není to bug, nic se neměnilo.

---

> ⚠️ **Předchozí dávky (níže) byly dělané při ODPOJENÉM Unity MCP.** Play-test výše
> pokryl hlavní věci; zbytek (split-screen coop dávek 1-2, staré savy) chce ruční
> proklik člověkem.

Poslední práce (nejnovější nahoře):
- **Dávka 2 (2026-09-09)**: rozbitá loď → panáček plave (pomalu, zranitelný,
  opraví v obchodě); 25% šance že zásah do lodě trefí i panáčka; extra Kenney
  dekorace ostrovů z Resources. Viz "DÁVKA 2026-09-09 #2" níže.
- **Velká dávka (2026-09-09)**: ekonomika (EconomyConfig — 1 ryba/1, poklad/5,
  velká loď 1000, ceny per ostrov), křížky na obchodech, mapa → šipka k ostrovu,
  obrazovka smrti + respawn, ostrovy vzácnější (200) + větší (5×5), palmy 2×,
  Kenney pirátské lodě, děda musí sedět na ostrově, startovní ostrov bez bedny.
  Viz sekce "DÁVKA 2026-09-09" níže.
- **Soubojový systém** (`7b0adaf`): střelba z lodě (LMB / Numpad *), munice
  v obchodě + HUD, nepřátelské ostrovy (~20 %) s dělem, piráti (malá/střední/velká
  loď) s boss health barem, odměny, potopení lodě → obnova na 30. Viz sekce
  "SOUBOJOVÝ SYSTÉM" níže.
- **Veslice na hladinu** (`ROW_EXTRA_SINK 0.08`), **health bary přesunuty do
  MinimapUIRenderer** (děti minimapy → vycentrované nad ní), **mola = výběžky
  do vody** (`83d4e3b`).
- **Molo: lodí se nevjede + loď zůstane plavat + oprava lodě + health bary**
  (`97500f2`).
- **Veslice jako startovní loď + progrese lodí + NPC děda (náčrt)** (`e9826a9`).
- **Moře**: jedna velká voda + dno + obloha (`d10fc2d`), dno mělčí + vraky na
  přírodní mělčině + tmavší hladina + pěna za lodí (`96c79a4`, `d79a9b4`).

Předtím: série coop/maják úprav — vrak místo truhly, kulatý maják, oba hráči
v jedné místnosti majáku (modrý+červený), per-hráč obchody, souřadnice per-hráč,
oprava nasedání do lodě.

**Uživatel dává další úkoly PO KOUSKÁCH přímo do chatu.** Backlog v `Napady.txt`.
Větší backlog nápadů je v **`Napady.txt`** v kořeni repa (jedna velká voda místo
dlaždic + vlny, loď víc do vody, nižší spawn rate pokladů/ryb, bedna mega questu
jen 1× a jen jeden hráč, NPC děda + příběh, start s "boat row small",
opravování lodí + health bary, děla na ostrovech + nepřátelské ostrovy,
střílení z lodě + munice v shopu, piráti / boss fighty, víc decoru na ostrov).
Nedělat proaktivně — počkat, až to uživatel zadá.

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
| `fce7557` | MP: `MultiplayerManager` rušil jen komponentu Camera z P2 klonu → URP varování "Can't remove Camera…". Teď ruší celé objekty kamer + CameraOrbit z P2. Split-screen = 3 kamery, 0 varování. |
| `596b059` | **Volná jízda podle kamery.** `PlayerController.Move()` počítá směr z `Camera.main.forward/right` (jen vodorovně) místo pevných os W/A/S/D → otoč kameru (RMB), `W` jede tam, kam se kamera dívá. Jde i diagonálně (`W`+`D`), model se natáčí plynule (`Quaternion.Slerp`, pole `turnSpeed`). Kolize u pobřeží řeší sklouznutí po jedné ose (`TryMoveBy`) místo zaseknutí. `GridX/GridY` (a tedy generování světa, mlha, save) se aktualizují při každém přechodu na jinou dlaždici (`OnEnteredTile`), ne jen jednou za stisk klávesy. Nastupování do lodě teď funguje i z diagonální pozice (Chebyshev vzdálenost ≤1, dřív jen přesně 1 pole rovně). Ověřeno přes UnityMCP (reflexe do `Move`/`TryToggleBoatFoot`): jízda podle kamery, diagonála, plynulé otáčení, diagonální nástup — vše sedí, 0 chyb v konzoli. |
| *(polish)* | **4 vylepšení z backlogu, na žádost uživatele.** Viz sekce níže. |
| *(coop 1)* | **6 coop/gen úprav (2026-09-07).** Viz sekce "COOP + GENEROVÁNÍ" níže. |
| *(coop 2)* | **Coop maják = pochozí interiér navíc + 3 další (2026-09-07/08).** |
| *(coop 3)* | **Vrak místo truhly na moři + P1 se schová v majáku + per-hráč obchod (2026-09-08).** Viz níže. |
| *(coop 4)* | **Coop maják pro OBA hráče + kulatá místnost + šipky nikdy P1 (2026-09-08).** Viz níže. |

## MOLO + LOĎ ZŮSTÁVÁ + HEALTH + OPRAVA (2026-09-09) — HOTOVO, OTESTOVÁNO přes MCP

Uživatel po kouskách. Hotovo (`<hash tohoto commitu>`):
1. **Veslice níž** — `ShipModelSwitcher.ROW_EXTRA_SINK 0.15` (model ~-0.58 místo -0.43).
   `Apply()` teď posazuje model do hladiny VŽDY (i schovaný) a volá se z
   `ShowBoatOrFoot()` → nasednutí má loď hned správně.
2. **NPC dialog fix** — `StoryNpc.reopenAllowedAt` (0.35 s po `EndTalk`): tentýž
   stisk E po dokončení monologu už dialog hned neotevře znovu (šlo se zaseknout).
3. **Molo — lodí se NEvjede.** `PlayerController.CanEnter` v lodi = jen voda
   (`IsBoatWater`), ne Pier/Harbor. Vystoupit jde jen když loď plave HNED VEDLE
   mola → panáček přeskočí na molo. Nasednout: hráč na molu / vedle + loď vedle
   (jinak "připluje" `WaterNextToPierNear`). `GenerateInitialWorld` parkuje loď
   do vody vedle mola (`FindWaterNextTo`), ne na molo.
4. **Loď po vystoupení zůstane plavat** — `parkedBoatGO` = kopie modelu lodě na
   místě vystoupení (`SpawnParkedBoat`/`DespawnParkedBoat`), `SyncParkedBoat()`
   v Update to drží (i po loadu save). Zničí se při nasednutí + `OnDestroy`.
5. **Health bary** — `GameData.boatHealth/playerHealth` (+ player2), max 100
   (`BoatStats.MaxHealth`). `HUDCounter` kreslí 2 pruhy HNED NAD minimapou
   (P1 vlevo, P2 vpravo), pod 30 zčervenají.
6. **Oprava lodě v přístavu** — pěšky u svého člunu na molu → `R` (P1) /
   `Numpad /` (P2), 2 mince/bod (`REPAIR_COST_PER_HP`). Nápověda přes `OnGUI`.
7. **Příprava na souboje** (zatím nevyužité): `BoatStats.CannonDamage`
   (veslice 0, malá 0.5, střední 1, velká 2) + `HasCannon`. `GameData.ammo`
   (+ player2). Obchod: řádek "Munice do děla" (opakovaný nákup, `ammoPackCost`
   60 / `ammoPackSize` 10). Perk texty zmiňují dělo.

## DÁVKA 2026-09-09 #2 — HOTOVO, jen offline compile-check (Unity MCP pořád odpojený)

1. **Rozbitá loď → panáček plave.** `GameData.boatWrecked` (+player2). Loď na
   0 HP se **rozbije** (dřív reset na 30): panáček je ve vodě, plave
   `moveSpeed * BoatStats.SwimSpeedMultiplier (0.28)`. Dělo do něj pořád může
   střílet (`CannonBall`/`CombatDirector` cílí i na `IsSwimming`; `DamageBoat`
   při rozbité lodi jde rovnou do panáčka). Panáček na 0 → obrazovka smrti.
   - `CanEnter` při plavání povolí i molo/pevninu → doplaveš k ostrovu a
     `OnEnteredTile` tě automaticky vyloví (nebo E). Nedá se rybařit/těžit/střílet.
   - `ShipModelSwitcher` schová loď i při `boatWrecked`. Panáček plave o 0.28 níž.
   - Noví piráti se nespawnují, když hráč jen plave (`TrySpawnPirate` kouká na
     `IsSailing`, ne `IsSwimming`).
2. **Oprava v obchodě.** `UpgradeShopManager` řádek "Opravit ROZBITOU loď"
   (`EconomyConfig.WreckRepairCost 160`) / "Opravit loď (hp/100)" (2/bod).
   `FixBoat` nastaví `boatHealth 100`, `boatWrecked false` a `boatNeedsRehome true`.
   `PlayerController.SyncParkedBoat` pak loď přemístí k nejbližšímu molu
   (`GridManager.NearestPierTile`) a flag smaže.
3. **25 % zásah do panáčka.** Zásah do celé lodě má `BoatStats.CannonSplashChance`
   (0.25) šanci trefit i panáčka (splash = `max(3, dmg/2)`).
4. **Extra dekorace ostrovů.** 10 Kenney fbx do
   `Assets/TutorialInfo/Resources/IslandDecor/` (rocks-a/b/c, rocks-sand-b/c,
   grass, grass-plant, palm-bend, patch-grass-foliage, patch-sand-foliage).
   `IslandDecor` je načte staticky, přidá do fondu vedle vestavěných `Decor_*`,
   materiál (PirateColormap) vezme z existující `Decor_` dlaždice. **Měřítka
   0.5–0.9 + palma 2.1× — doladit v editoru.**

## DÁVKA 2026-09-09 (velký seznam po kouskách) — HOTOVO, jen offline compile-check

**Unity MCP byl odpojený → NEODZKOUŠENO v editoru. Projet ručně.**

1. **`EconomyConfig.cs` (NOVÝ) — všechna čísla ekonomiky na jednom místě.**
   - Výkup: `FishPrice 1`, `TreasurePrice 5`, `SellBonusPerItem 3` (mega quest).
   - Nákup: Speed 160, Rod 110, Mining 130, ShipSmall 180, ShipMedium 450,
     **ShipLarge 1000**, AmmoPack 45/12, MapItem 130.
   - Odměny: pirát 40/110/260, ostrovní dělo 90, bedna 40–120, mega quest 400–900.
   - `IslandPriceLevel(lx,ly)` = deterministický hash pozice majáku → 0–20;
     `PriceMultiplier` → 0.9×–1.3×. **Jen NÁKUP, ne výkup.**
   - `UpgradeShopManager`/`QuestShopManager`/`ChestManager`/`CombatDirector`/
     `PlayerController` (oprava) čtou z EconomyConfig. **Odebral jsem serializovaná
     `public int …Cost` pole z obou shopů** — Unity ta data při importu zahodí,
     hodnoty jsou teď v kódu (ověřit, že obchody dál fungují).
2. **`GameSession.ShopPriceLevel` (static)** — nastaví se v
   `PlayerController.TryInteractAdjacentBuilding` při vstupu do majáku / obchodu
   z pozice dlaždice. Obchod píše "( ceny +N% na tomhle ostrove )".
3. **Zavírací křížek** vpravo nahoře v obou obchodech (`ShopUI.cs` → `CloseButton`).
4. **Mapa** — nákup v upgrade shopu (`GameData.hasMap` + player2). Na minimapě
   **azurová šipka** k nejbližší Harbor dlaždici (`GridManager.NearestHarborTile`),
   odlišená od zlatého mega-quest kompasu. `MinimapUIRenderer.CreateIslandArrow`.
5. **`DeathScreen.cs` (NOVÝ)** — panáček na 0 zdraví → freeze + 2 tlačítka:
   - **Respawn** → `GridManager.RespawnPlayerAtNearestIsland(idx)`: veslice na
     nejbližším ostrově od místa smrti (nouzově `ForceIslandNear`), **mince a
     rozdělaný mega quest zůstávají**, ale ryby/poklady/náboje/všechna vylepšení/
     mapa/sellBonus se ztratí a `shipLevel → 0`, `activeQuest.Reset()`.
   - **Hlavní menu** → `MainMenuManager.Show()`.
   - Smrt nastává: loď při 0 HP bere panáčkovi **25 zdraví** (dřív nezničitelný,
     `Mathf.Max(1,…)` zrušeno). `DeathScreen.IsOpen` gate v `PlayerController.Update`.
6. **`StoryNpc`** — děda smí sedět jen na políčku obklopeném ze VŠECH 4 stran
   pevninou (`SurroundedByLand`), nouzově 3/4. Řeší "sedí mimo ostrov".
7. **Startovní ostrov BEZ bedny** (`GenerateInitialWorld` — `MaybePlaceChest` pryč).
8. **Generace ostrovů:** `MIN_ISLAND_DISTANCE 50→200`, kandidáti na mřížce `%40`
   s 30 %, jádro `Random.Range(5,8)` (min 5×5, dřív 4×4), `ISLAND_CANVAS 14→16`,
   guard `land.Count < 25`. Vraky vzácnější: `GenerateRandomSeaType` 0,15 %→**0,06 %**
   (ryby 0,35 %→0,35 %).
9. **Veslice** `ROW_EXTRA_SINK 0.08→0.05` (o kousek výš).
10. **`IslandDecor`** — `decorChance 0.28→0.5`, občas 2. dekorace, **palmy ×2,1**,
    ostatní ×0,85–1,25. (Nové Kenney varianty rocks-b/c, grass-plant, patche
    ZATÍM NE — chtělo by to přesun do Resources, netestovatelné bez editoru.)
11. **Pirátské Kenney lodě** — `ship-pirate-{small,medium,large}.fbx` přesunuty
    do `Assets/TutorialInfo/Resources/PirateShips/` (nic je neodkazovalo).
    `PirateShip.BuildKenneyModel` je `Resources.Load` + tmavý nátěr (fbx nemá
    materiál), fallback na kvádry. **Měřítko 0.42/0.55/0.7 + otočení + Y −0.35
    v editoru doladit.**

### ZBÝVÁ z toho seznamu / k doladění
- Kenney varianty dekorace (rocks-b/c, rocks-sand-b/c, grass-plant, palm-bend/
  straight, patch-*-foliage) — přesun fbx do Resources + `IslandDecor` je náhodně
  bere; materiál vzít z existující Decor_ dlaždice.
- Vyvážení čísel v `EconomyConfig` podle reálného hraní.
- Vizuální kontrola všeho výše v editoru.

## SOUBOJOVÝ SYSTÉM (2026-09-09) — HOTOVO, OTESTOVÁNO přes MCP (`7b0adaf`)

Modely jsou zatím z primitivů (koule/kvádry) — funkční, ne hezké. Kenney
`cannon.fbx` / `cannon-ball.fbx` existují, dají se doplnit.

- **`CannonBall.cs`** — dělová koule bez fyziky (posun + kontrola vzdálenosti
  k cílům, `HIT_RADIUS 1`). `Side.Player` zasahuje piráty + ostrovní děla,
  `Side.Enemy` zasahuje loď kteréhokoli plujícího hráče. Život 2.4 s.
- **Střelba z lodě** — `PlayerController.TryShoot()`: LMB (P1) / Numpad `*` (P2),
  `SHOOT_COOLDOWN 0.55`. Jen když `BoatStats.HasCannon(shipLevel)` (veslice ne)
  a `PAmmo > 0`. Munice: `GameData.ammo` (+ player2), kupuje se v `UpgradeShopManager`
  (řádek "Munice do děla", `ammoPackCost 60` / `ammoPackSize 10`), zobrazená
  jako 4. řádek v `HUDCounter` ("Naboje: N").
- **`HostileIslandCannon.cs`** — ~20 % ostrovů (`GameData.hostileIslands`, klíč =
  bod, kolem kterého ostrov vznikl, přidává `GridManager.GenerateIsland`).
  `CombatDirector` na aktivním nepřátelském ostrově poblíž hráče spawne dělo
  (`GridManager.TryGetHostileCannonSpot` — Harbor dlaždice u vody). Střílí na loď
  hráče na dostřel `RANGE 9.5` každých `RELOAD 2.6` za `DAMAGE 7`. `MAX_HP 3`
  poškození → zničeno → `+60` minci, klíč do `GameData.clearedIslands` (natrvalo).
- **`PirateShip.cs`** — `size` 0/1/2 (malá/střední/velká), HP 2.5/5/9. Spawnuje
  `CombatDirector` v otevřené vodě 14–20 políček od PLUJÍCÍHO hráče (jinak ne),
  `MAX_PIRATES 2`, interval 22–42 s. Chování: daleko bloumá (`Wander`), po
  přiblížení (`AGGRO_RANGE 11`, po prvním souboji +4) pronásleduje na `KEEP_DIST 3.2`,
  střílí (`RELOAD 2.9`, dmg 5/8/12) a naráží (`RAM_RANGE 1.4`, á 1.5 s, dmg 6/10/16).
  Po útěku za `GIVEUP_RANGE 20` na `GIVEUP_TIME 12` s → zmizí. Potopení =
  `+30/80/180` minci (`GameData.pirateKills++`). `Dt` = `Min(deltaTime, 0.05)` —
  ochrana proti škubání editoru mimo fokus.
- **`CombatDirector.cs`** — sám se vytvoří (`Ensure()` z `GridManager.Awake`).
  Drží listy pirátů + děl, spawnuje, uklízí (děla ostrovů dál než 40 od hráče).
  `OnGUI`: boss health bar prvního útočícího piráta nahoře uprostřed (červený,
  s popiskem velikosti) + krátké „toast" hlášky dole. `NearestSailingPlayer`,
  `AwayFromNearestThreat`.
- **Potopení lodě** (`PlayerController.DamageBoat`, HP ≤ 0): obnova na 30,
  panáček −20 (min. 1 — **žádné game-over**, maturita), `damageGraceUntil`
  3.5 s nezranitelnost, „odplavání" 9 políček od nejbližší hrozby.
- **`PlayerController.IsSailing`** = `!isOnFoot && enabled && aktivní` — cíl pro
  soubojový systém.

**Co ještě může chtít doladit:** hezčí modely (Kenney cannon/ship-pirate fbx),
vyvážení HP/dmg/odměn, zvuk výstřelu (teď jen splash placeholder), munice/piráti
pro P2 víc otestovat v reálném split screenu.

## LODĚ + PŘÍBĚHOVÉ NPC + MOLO (2026-09-08 pozdě večer) — HOTOVO, OTESTOVÁNO přes MCP

Uživatel po kouskách: loď o kousek víc z vody + níž molo; NPC "děda" na základní
ostrov (náčrt příběhu — schoval poklad, poslední přání ho najít); startovat
veslicí ne malou lodí; každá loď ať je lepší než předchozí.

1. **Startovní loď = veslice.** `shipLevel` nově: **0 = veslice** (`boat-row-small`,
   přidán jako child Playeru v SampleScene, materiál PirateColormap), 1 = malá
   plachetnice, 2 = střední, 3 = velká. `ShipModelSwitcher` má pole `shipRow`
   (zapojené). Staré savy: loď o stupeň "níž" (level 1 = dřív střední, teď malá) —
   WIP projekt, uživateli řečeno.
2. **`BoatStats.cs` (NOVÝ)** — na jednom místě co která úroveň lodě umí. KAŽDÁ
   vyšší je znatelně lepší: `SpeedMultiplier` 0.75 / 1.0 / 1.25 / 1.5 ×,
   `MiningMultiplier` 0.8 pro střední+, `FishBonus` +1 ryba/zátah pro velkou.
   Čte to `PlayerController.Move/FishingRoutine/MineRoutine`.
3. **Obchod** (`UpgradeShopManager`) — 3 řádky lodí (malá `shipSmallCost` 200,
   střední 300, velká 800), popis = `BoatStats.Perk(level)`. Panel vyšší (h 505).
   `get boat row/small/medium/large` v konzoli.
4. **`StoryNpc.cs` (NOVÝ) — NÁČRT příběhu.** Objekt "StoryNpc" v SampleScene.
   V `Start()` si přes `GridManager.GetStartIslandHarborTiles()` (nová public
   metoda — flood-fill pevniny nejblíž počátku, vrací Harbor dlaždice) najde
   políčko na **startovním ostrově** (ne u majáku, nejradši u mola), postaví
   sedícího panáčka v kódu (stejné díly jako hráč + šedé vlasy/vousy, sedí, kouká
   na moře). Hráč pěšky vedle + `E` (P1) / `Numpad1` (P2) → krátký dialog (IMGUI
   box dole, 6 replik, náčrt textu). `PlayerController`: nové pole `storyNpc`,
   gate `myTalkOpen` (mrazí jen toho hráče, co mluví), v
   `TryInteractAdjacentBuilding` kontrola `storyNpc.IsAt(tx,ty)`.
   **TODO příště:** navázat na skutečný úkol/odměnu (teď jen text).
5. **Molo níž** — `GridManager.PIER_TILE_Y = -0.25` (dřív default -0.1), deska
   těsně nad hladinou, ať nasedání nevypadá jako skok. `BOAT_SINK` 1.05 → 0.93
   (loď o kus víc nad hladinou).

Ověřeno přes MCP: nová hra → shipLevel 0, veslice se ukáže při nasednutí; NPC
placed na startovním ostrově (2 seedy), dialog jede 0→5 a zavře se; molo Y -0.25
(screenshot: deska u hladiny, ne díra); BoatStats hodnoty; 0 chyb/varování.
Commit `e9826a9`.

## MOŘE — DNO A VRAKY (2026-09-08 pozdě večer) — HOTOVO, OTESTOVÁNO přes MCP

Návaznost na `d10fc2d` (jedna velká voda místo dlaždic) a `96c79a4` (první verze
dna + kupky). Uživatel: *"udelej aby ty kupky nebyli kupky ale primo dno ktery se
random vygeneruje podle toho kde jsou ty vraky … prirozena generace sveta ne kupka
uprostred niceho … a ta lod kdyz jede … dej ji vic aby kus tou lodi je pod hladinou"*.

1. **`SeaFloor.cs` — přírodní mělčina v meshi dna.** Zrušené koule "Sandbar"
   (`GridManager.AddWreckSandbar` + `wreckSandMat` smazané). Dno = jedna síť,
   výška vrcholu = `Perlin(noise²)` mezi `FLOOR_MIN -10` a `FLOOR_MAX -3`. Pod
   políčky s vrakem (Treasure) se vrcholy PLYNULE zvednou (smoothstep, `SHOAL_RADIUS 9`)
   k `SHOAL_Y -2.7` → vrak sedí na mělčině, ne "lítá" ve vodě, ale je pořád vidět
   z hladiny (voda je průhledná, hladina −0.22). `STEP` 8→4 (jemnější, ať se
   mělčina vykreslí). Souřadnice vraků dodává `GridManager.CollectTreasureTilesNear(cx,cz,radius,outList)`
   (`SCAN_RADIUS 44` políček). `SeaFloor.Init` má teď 3. parametr `GridManager grid`.
   `Reshape()` se volá při posunu sítě (Snap) + navíc jednou za 1 s (`nextWreckRescan`)
   pro případ, že se vrak dogeneruje, když hráč stojí.
2. **`ShipModelSwitcher.BOAT_SINK` 0.62 → 1.05.** Model lodě se posadí níž →
   kus trupu je pod hladinou (objekt hráče Y 0.5, hladina −0.22, model ≈ −0.55).

Ověřeno přes MCP: dno u vraku max Y ≈ −2.9 vs. daleko průmer ≈ −8.3 (přírodní
přechod), `Wreck` objekt Y −2.9 (sedí na dně), boatModel Y −0.55 (trup pod
hladinou), 0 chyb. Screenshoty: vrak leží na světlejší mělčině, plynule přechází
do hloubky, žádná koule.

Pozn.: tmavší vrstva hladiny (`OceanSurface.BuildSkin` / "OceanSkin") a pěna za
lodí (`BoatWake.cs`) z `96c79a4` — funkčně hotové, stojí za kouknutí v Play naživo
při skutečné plavbě.

## COOP MAJÁK PRO OBA + KULATÁ MÍSTNOST (2026-09-08) — HOTOVO, OTESTOVÁNO přes MCP

1. **Do majáku v coopu může KTERÝKOLI hráč** (dřív jen P1). `LighthouseManager.Enter()`
   už neblokuje P2. `PendingPlayerIndex` (static) říká `LighthouseInterior`, kdo
   vešel. `MultiplayerManager.BeginLighthouseSplit(cam, playerIndex)` → interiér
   na LEVOU půlku (P1) nebo PRAVOU (P2), vypne herní kameru + zmrazí + schová
   toho hráče; druhý hráč hraje dál. Naráz jen jeden uvnitř
   (`if (InsidePlayerIndex >= 0) return`). Ověřeno: P1 vejde (levá), vyjde,
   P2 vejde (pravá), P1 se celou dobu hýbe.
2. **Interiér majáku je KULATÝ** (maják je válec). Scéna `LighthouseInterior`
   přestavěná přes `execute_code`: kulatá podlaha + koberec (Cylinder), zeď
   z 19 segmentů do kruhu (poloměr 4, mezera vepředu = dollhouse pohled),
   pulty + dveře + decor rozmístěné po obvodu. Kamera stažená
   (0, 8.6, -9.2, sklon 42°).
3. **`InteriorPlayer` per-hráč klávesy + kruhové omezení.** `ownerPlayerIndex`
   (0 = P1/sólo = WASD+E, 1 = P2 = šipky+Numpad1). **Šipky NIKDY neovládají
   hráče 1** (ani venku — `PlayerController.Key()` to už dělal, teď i uvnitř).
   Nové pole `areaRadius` (3.4) místo `areaHalfSize` — kruhový clamp
   `if (fromCenter.magnitude > areaRadius) …`.
4. `InteriorInteractable.Trigger(int playerIndex)` — obchod se otevře za
   správného hráče (P2 uvnitř → `Open(1)` → GUI na pravé půlce).
5. `LighthouseInterior` OnGUI mince: P2 uvnitř → mince P2, na pravé půlce.

Ověřeno přes MCP: sólo maják (plné přepnutí, kulatá místnost, nákup projde,
odchod OK), coop P1 i P2 vstup/výstup, 1 AudioListener, 0 chyb.

### Oprava: maják nesmí zablokovat nasednutí do lodě (2026-09-08)
Uživatel: jednou se stalo, že maják byl moc blízko přístavu a nešlo nasednout.
1. **Generování:** `GridManager.PlaceLighthouse` má nový filtr
   `LighthouseKeepsIslandWalkable(p, land)` — flood-fill zbylé pevniny (bez
   2×2 majáku), maják se položí jen když zůstane VŠE dosažitelné (ostrov se
   neroztne). Ověřeno na 21 ostrovech: 21/21 OK.
2. **Nasedání robustní:** `PlayerController.TryToggleBoatFoot`:
   - Vystup z lodě: pevnina vedle mola → druhé molo → v nejhorším zůstat stát
     na molu (`FindAdjacent(x,y,type)`). Vždycky se dá vylodit.
   - Nastup zpět: když je loď nedosažitelná (maják v cestě), ale hráč stojí
     na molu nebo hned vedle (`PierAtOrNextTo`), loď se k němu "připluje"
     (přehodí `boatGridX/Y`). Ověřeno: nasedne z mola i od mola i když je
     boatGrid daleko; nenasedne jen když fakt není žádné molo poblíž.
   `FindAdjacentHarbor` → obecné `FindAdjacent(x,y,TileType)`.

### COOP: souřadnice per-hráč + OBA v majáku naráz (2026-09-08)
1. **Souřadnice ve split screenu.** `HUDCounter.Start()` teď volá
   `UpdateLayout(true)`, když `IsMultiplayer` (P2 HUD vzniká až po zapnutí MP,
   tak si to musí udělat sám). Dřív P2 souřadnice zůstaly vlevo nahoře přes P1.
   Teď P1 = levý horní roh levé půlky, P2 = levý horní roh pravé půlky.
2. **Do majáku můžou oba hráči NARÁZ — v JEDNÉ místnosti** (jeden modrý, druhý
   červený, vidí na sebe). Scéna `LighthouseInterior` se načte ADITIVNĚ JEDNOU
   (offset +5000). `LighthouseInterior`:
   - `SetUpCoopFirstPlayer()` (Awake): posun rootů, první hráč použije
     postavičku + kameru přímo ze scény (`figures[who]`, `cams[who]`),
     obarví ji (P1 modrá, P2 červená přes MPB na dílech s `InteriorPlayerMat`),
     `BeginLighthouseSplit(cam, who)`.
   - `AddPlayer(idx)` (volá `LighthouseManager.Enter` když je scéna už načtená):
     naklonuje postavičku + kameru z prvního hráče, `MoveGameObjectToScene`
     do scény majáku (jinak spadnou do herní!), obarví, `BeginLighthouseSplit`.
   - `RemovePlayer(idx)`: `EndLighthouseSplit(idx)` + zničí figuru+kameru.
   `LighthouseManager`: `inside[2]`, jedna `Scene interiorScene`. `Enter` už
   neblokuje druhého. `ExitCoop(idx)` → `RemovePlayer(idx)`, a když je uvnitř
   0 hráčů → odečte scénu.
   **Obchody teď mají per-hráč stav** (`UpgradeShopManager`/`QuestShopManager`
   pole `openFor[2]`, `IsOpenForBuyer(i)`, `OnGUI` smyčka kreslí panel pro
   každého otevřeného hráče na jeho půlce). `AnyShopOpen` je teď počítaná
   property (projde všechny obchody). `Open(idx)` per-hráč, zavírání
   Escape (P1) / NumpadEnter (P2). `InteriorPlayer.MyShopOpen()` kouká na
   `IsOpenForBuyer(ownerPlayerIndex)`.
   Ověřeno přes MCP: oba vejdou do 1 místnosti (modrá+červená figura,
   screenshoty L+R), P1 má upgrade obchod otevřený + P2 quest obchod naráz,
   nezávisle nakupují, každý vyjde zvlášť (poslední zavře scénu), re-enter OK,
   sólo maják beze změny, 1 AudioListener, 0 chyb.

### PLNÝ COOP PRŮCHOD OTESTOVÁN (2026-09-08)
Nová MP hra → oba pěšky na pevnině vedle sebe (P2 červený) → oba nasednou na
loď (každý své molo) → plavba → P1 rybaří / P2 těží vrak (oddělené ekonomiky,
cross-check 0) → oba zakotví → P1 vejde do majáku (levá půlka, kulatý interiér),
koupí upgrade + quest, vyjde (P2 se celou dobu hýbe) → P2 vejde do majáku
(pravá půlka), koupí quest, vyjde (P1 se hýbe) → P2 otevře bednu → P2 mega
quest (per-hráč, P1 nemá) → P2 doplује + vykope → P2 vyplatí v majáku
(+1261 mincí, +trvalý sellBonus jen P2) → převod peněz v pauze (P1→P2 300 OK)
→ konzole (get money/fish/boat, upgrade, tp OK) → P2 kamera Numpad +/- OK →
teardown (Stop() → 1 hráč, 2 kamery, full-screen). **0 chyb/varování, split
screen screenshot potvrzuje P1 v majáku vlevo + P2 na ostrově vpravo.**

## VRAK / SKRÝVÁNÍ P1 / PER-HRÁČ OBCHOD (2026-09-08) — HOTOVO, OTESTOVÁNO přes MCP

1. **Treasure políčko na moři = VRAK LODĚ** místo malé truhly. `TreasurePrefab`
   přestavěn (přes `PrefabUtility.SaveAsPrefabAsset`, GUID zachován): Kenney
   `ship-wreck.fbx` (scale 0.24, localPos y -1.0, nahnutý ~Euler(10,34,22)) =
   napůl potopený nahnutý vrak, + malá `chest.fbx` na palubě. Materiál
   `PirateColormap.mat`. Těžba (`MineRoutine`) beze změny (jen mění tile na Water).
2. **Ve split screenu se P1 schová, když je v majáku.** `PlayerController.SetVisualHidden(bool)`
   (schová headDot i boatModel; `false` → `ShowBoatOrFoot()`). Volá
   `MultiplayerManager.DoBeginLighthouseSplit` (hidden) / `DoEndLighthouseSplit`
   (zpět). Takže P2 nevidí ducha P1 stát na ostrově.
3. **Per-hráč obchod ve split screenu.** `UpgradeShopManager`/`QuestShopManager`
   mají `IsOpenForBuyer(int playerIndex)` = `isOpen && buyerIndex == playerIndex`.
   `PlayerController.Update` gate teď kouká na `IsOpenForBuyer(playerIndex)` místo
   `IsOpen` → obchod jednoho hráče nemrazí druhého. (Sólo: stejné chování.)
   Bod 3 zadání ("sjednotit klávesy P2") = uživatel vybral "nechat numpad, jen
   sjednotit" — audit ukázal, že E/Space/Esc↔Numpad1/Numpad0/NumpadEnter už
   všechno mají protějšek; jediná reálná mezera byl ten shop-freeze, teď opravený.
   Pozn.: P2 do majáku v coopu nemůže (design — 1 kamera / additivní scéna).

Ověřeno přes MCP: vrak vypadá jako vrak (screenshoty), P1 headDot+boat se
schová/vrátí při vstup/výstup z majáku, P2 se hýbe i když má P1 otevřený
interiérový obchod. 0 chyb/varování.

## COOP + GENEROVÁNÍ ostrovů (2026-09-07) — ROZDĚLANÉ

Uživatel zadal 7 věcí. **6 hotových (offline compile OK, NEOTESTOVÁNO v editoru —
Unity MCP byl odpojený), 7. rozdělaná.**

Hotovo:
1. **Nová hra = spawn pěšky na pevnině.** `GridManager.GenerateInitialWorld()`:
   loď zaparkuje na 1. molo, hráč stojí PĚŠKY na `Harbor` políčku hned vedle
   (`FindHarborNextTo`), `gameData.isOnFoot = true`. Platí i pro sólo hru.
   `PlayerController.Start()` P2 větev: P2 startuje vedle P1 ve stejném režimu
   (pěšky/loď), loď P2 na druhé molo.
2. **Hráč 2 červené tričko.** `MultiplayerManager.Setup()` — na klonu P2 najde
   `headDot/Body` renderer a přes `MaterialPropertyBlock` nastaví `_BaseColor`
   načerveno (0.75, 0.16, 0.13). P1 zůstává modrý (`PlayerCoat.mat`).
3. **Coop kamera.** Myš = jen P1 (jeho `CameraOrbit` beze změny). P2 si točí
   kameru `Numpad + / -` (i horní `+`/`-`) — `MultiplayerManager` má vlastní
   `p2Yaw/p2Pitch/p2Distance`, kameru P2 staví `PositionP2Camera()` (přestala
   kopírovat rotaci P1). `PlayerController` má nové pole `viewCamera` —
   `Move()` počítá směr z něj (P2 dostane svou kameru, P1 = `Camera.main`).
4. **Maják na mapě ~2× větší** — `GridManager.InstantiateTile`, `tower.localScale *= 1.6f` → `*= 3.2f`.
5. **Minimální ostrov 4×4** — `StampOrganicLand` jádro `Random.Range(3,6)` → `Random.Range(4,7)`. `GenerateIsland` guard `< 9` → `< 16`.
6. **Maják nikdy hned vedle mola** — `PlaceLighthouse` filtruje 2×2 bloky přes
   nový `Any2x2TileTouchesPier(x,y)` (4-směrní sousedé nesmí být `Pier`).

**7. HOTOVO A OTESTOVÁNO přes UnityMCP (Run In Background zapnuté) — coop maják
jako pochozí interiér navíc:**
Ověřeno: nová coop hra → oba pěšky na pevnině vedle sebe, P2 červené tričko,
3 kamery. Maják: interiér aditivně (+5000 offset), P1 kamera vypnutá, interiér
na levé půlce, P1 zmrazený; P2 se celou dobu hýbe (i s otevřeným obchodem P1).
Interiérový obchod se otevře (ne ten herní), na půlce obrazovky. Návrat odečte
scénu, obnoví P1 kameru/CameraOrbit/ovládání. 2× za sebou, 1 AudioListener,
0 chyb/varování. Ostrovy dál od startu 8–9 políček, maják nikdy u mola.
Zbývá jen VIZUÁLNÍ kontrola člověkem (jak to vypadá, klávesy naživo).
V coopu P1 vejde do OPRAVDOVÉHO pochozího interiéru (scéna `LighthouseInterior`
načtená ADITIVNĚ vedle `SampleScene`), P2 hraje dál na své půlce.
- `LighthouseManager.Enter()`: sólo = `LoadScene` (beze změny). Coop =
  `LoadSceneAsync(Additive)` přes `EnterCoop()`. `ExitCoop()` scénu odečte.
  Nový `switching` guard.
- `LighthouseInterior.Awake()` → `SetUpCoopSplit()`: posune všechny root objekty
  interiéru o `+ (5000,0,0)` (daleko od oceánu), `InteriorPlayer.SetAreaCenter()`
  na ten posun (jinak by se ořezával zpět k počátku), a zavolá
  `MultiplayerManager.BeginLighthouseSplit(interiorCam)`.
- `MultiplayerManager.BeginLighthouseSplit`: interiér kamera → `rect (0,0,0.5,1)`
  (levá půlka), vypne `p1Camera` (+ její AudioListener + CameraOrbit), zmrazí
  `p1Player.enabled=false`. `EndLighthouseSplit` to vrátí.
- `LighthouseInterior.ExitToIsland()` — coop větev volá `LighthouseManager.ExitCoop()`
  místo `LoadScene`.
- `InteriorInteractable.Trigger()` → `FindInMyScene<T>()` — vezme obchod ze SVÉ
  scény (v coopu jsou 2 instance každého obchodu — herní + interiérová).
- Obchody (`UpgradeShopManager`/`QuestShopManager`) OnGUI: v coopu kreslí jen na
  půlku obrazovky podle `buyerIndex` (nezakryje druhému hru).
- `MultiplayerManager.LateUpdate`: P2 kamera už NEblokuje na `AnyShopOpen`
  (aby ji P1 v majáku / obchod P1 nemrazil).
- **Navíc: `MultiplayerManager.Setup()` má teď guard `if (IsMultiplayer) return;`**
  (pojistka proti dvojímu volání = víc kopií P2).
- Pozn.: P2 v coopu se NEfreezne, když P1 nakupuje/je v majáku, protože jeho
  `PlayerController` má referenci na HERNÍ obchod (ne interiérový), a ten zůstává
  zavřený. Sólo maják beze změny.

### CO OTESTOVAT RUČNĚ (bod 7 + celý coop) — viz seznam níže

## POLISH: zvuk, voda, kompas, post-processing (2026-09-04)

Uživatel vybral "všechno" z nepovinného polish backlogu. Hotovo:

1. **Zvuk** — nový `SoundManager.cs`. Klipy se **negenerují ze souborů, ale přímo
   v kódu** (`AudioClip.Create` + sinusovky/filtrovaný šum) — žádné externí
   soubory, žádná licence k řešení. Objekt se sám vytvoří, jakmile ho někdo
   poprvé potřebuje (`SoundManager.Ensure()`, stejný princip jako minimapa
   pro P2) — nic se neinstaluje ručně do scény. Zvuky: klik v UI (`Click()` —
   obaluje `GUILayout.Button(...)`, viz níže), cink mincí (prodej v questshopu,
   vyplacení questu/mega questu, otevření bedny), šplouchnutí při rybaření,
   vrznutí dveří majáku (vchod v `LighthouseInterior.Awake()`, východ v
   `GridManager.Awake()` přes `GameSession.ReturningFromLighthouse`), hukot
   moře na pozadí (smyčka, start v `GridManager.Awake()`, automaticky zmizí
   v majáku protože scéna má vlastní instanci). `generate_audio` (fal.ai) není
   nakonfigurované (žádný API klíč) — proto čistě procedurální přístup.
2. **Post-processing** — `Assets/Settings/SampleSceneProfile.asset` (Global
   Volume) měl už Bloom/Vignette/Tonemapping, jen na slabých hodnotách.
   Zesíleno (bloom 0.25→0.45, vignette 0.2→0.28) + přidán `ColorAdjustments`
   (saturace +10, kontrast +6, expozice +0.05).
3. **Water shader** — `WaterWave.cs` na `WaterPrefab`/`WaterFishPrefab`: jemné
   houpání nahoru/dolů (`Mathf.Sin`), fáze podle souřadnic dlaždice (sousední
   dlaždice se houpou mimo takt → vypadá to jako vlnění, ne jako poskakující
   deska). Bez shaderu/textur, čistě transform.
4. **Mega quest kompas** — zlatá šipka na okraji minimapy (`MinimapUIRenderer`),
   ukazuje směr k rozdělanému (nevykopanému) mega questu, otáčí se podle
   `Atan2(dx,dy)`. Po vykopání/bez questu zmizí. Matematika ověřená přes
   UnityMCP pro všechny 4 světové strany (sever/jih/východ/západ).

Ověřeno přes UnityMCP: kompilace 0 chyb, herní test (nová hra → hukot moře
běží → vstup do majáku → dveře → východ → dveře → hukot zase běží), kompas
otestován reflexí pro N/S/E/W i skrývání. Vizuální houpání vody nešlo přes
MCP ověřit snímek po snímku (editor mimo fokus tiká jen zřídka) — funkčně
je ale hotové a stojí za rychlé kouknutí v Play módu.

## PLNÝ PRŮCHOD HROU OTESTOVÁN (2026-09-04)
Nová hra → plavba → rybaření → těžba → zakotvit → pěšky → maják → nákup upgrade +
quest → ven → bedna → mega quest → doplout + vykopat → vyplatit v majáku (+bonus) →
prodej s bonusem → 3 save sloty nezávislé → konzole (get/upgrade/tp/explore) →
pauza → split-screen multiplayer. **Vše funguje, 0 chyb, ~127 fps.**

## STAV: CELÝ PŮVODNÍ PROJEKT HOTOVÝ ✅

Fáze 1 (Kenney grafika) + 2 (maják se scénou a obchody) + 3 (bedny + mega quest)
jsou všechny hotové, ověřené v Unity, na GitHubu.

### Možný polish / co dál (nic z toho není nutné)
- Interiér majáku: dá se ještě zútulnit.
- Postavička hráče: pořád jednoduchý low-poly panáček (tělo+hlava+klobouk+nos),
  ne pořádný model.
- Zvuk je čistě procedurální (syntetizovaný v kódu) — kdyby šlo sehnat/vygenerovat
  skutečné CC0 klipy (např. Kenney audio pack), mohly by znít lépe. Vyžaduje
  buď povolení stáhnout externí soubory, nebo nakonfigurovaný `generate_audio`
  (fal.ai API klíč v MCP for Unity → Asset Generation).

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
