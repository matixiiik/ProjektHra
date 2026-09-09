# Pokračování příběhu — mega ostrovy

> Návrh SCHVÁLEN uživatelem 2026-09-09. **NIC se zatím nestaví ani nepřidává.**
> Bude se dělat po malých kouskách do maturity (~6 měsíců). Souvisí s
> `.claude/handoff.md` (aktuální stav hry).

## Context

Hráč doplaje na příběhový mega ostrov (`storyStep 2 → 3`) a dostane jen hlášku
"někdo tu už kopal". Na ostrově se nedá nic dělat — je to jen země + obelisk
(`MegaIslandMarker`). Cíl: udělat z toho **reálný úkol** a rozvést příběh do
**krátkého uzavřeného oblouku přes 3 tematicky odlišné mega ostrovy**, s událostmi
na moři mezi nimi, aby to nebylo pořád stejné.

Rozhodnutí uživatele:
- **Úkol na ostrově:** probojovat se přes obránce → uprostřed **trezor**, který
  se otevře přes **puzzle** (5–20 min čistého hraní). **Konkrétní puzzle TBD.**
- **V trezoru není poklad**, ale **vzkaz od soka**.
- **Sok = SYN starého námořníka**, který kdysi dávno zmizel na moři. Sedí to
  s tím, že mu o původním pokladu vyprávěl otec → syn věděl, kde je. Identita se
  hráči **odhaluje postupně** přes 3 ostrovy; finále = **setkání otce a syna po
  letech**, teprve tam padne, kdo to je a proč zmizel.
- **Rozsah:** všechny 3 ostrovy + finále, po malých kouskách.
- **Strážci ostrova 1:** nový **stacionární typ nepřítele** (zbraně — mušketa
  apod. — se doladí později).
- **Místo přepadu piráty:** na moři **mořská obluda (megalodon styl)** —
  uhýbáš výpadům, pak se vyčerpaně zastaví na hladině a v tom okně do ní střílíš.
  Doladí se, ať je to top.

---

## 1. Příběhový oblouk

### Ostrov 1 — "Pevnost staré posádky"
Opevněný úkryt sokovy staré posádky. Zarostlé, děla, uzamčený trezor.

1. **Připlutí** → 2–3 ostrovní děla (`HostileIslandCannon`) + 1 hlídkující
   pirátská loď po tobě pálí. Sejmeš je z lodě → doplaješ k molu.
2. **Pěšky** → nový typ nepřítele: **stacionární strážci** (`LandGuard`).
   (v1: koule jako dělo; v2: mušketa)
3. **Trezor** uprostřed, zamčený. **Puzzle — TBD**, cíl 5–20 min, řešitelné,
   férový feedback. Náčrt: po ostrově jsou vodítka, hráč je nasbírá a vyluští.
4. Trezor otevřen → **prázdný**, jen **vzkaz soka**: *"Přišel jsi pozdě. Našel
   jsem to první. Hledej mě dál."* — podepsáno iniciálou, náznak vzteku
   a znalosti námořníka jako kapitána.
5. **Doma** (dialog, `storyStep 4`): pozná rukopis, zmlkne, dá souřadnice
   ostrova 2 — ale neřekne kdo to je. → `megaIndex++`, nový ostrov, waypoint.

### Mezi 1 a 2 — mořská obluda
1. **Výpady** — pluje pod hladinou (stín + brázda), telegrafovaně se rozjede,
   uhýbáš kormidlem. Zásah = velké poškození trupu.
2. **Vyčerpání** — po pár výpadech se vynoří, stojí ~8–10 s, střílíš do ní.
   Boss health bar (reuse).
3. 2–3 cykly. Po zabití odměna + trofej (zub?). Jednorázově (`ambush1Done`).

### Ostrov 2 — "Hřbitov lodí"
Vraky, mělčiny, jiná atmosféra.
1. Hlídač — "Bludný Holanďan" styl (prokletá loď, mechanika **TBD**, zatím
   ship-ghost model).
2. Puzzle **(líbí se)**: najdeš **3 kusy roztržené mapy** ve 3 vracích (kopání
   jako mega quest dig), složíš je → poloha/kód do podpalubí.
3. Uvnitř sokův **nedávno opuštěný tábor**. 2. vzkaz — pisatel viní otce, že ho
   **nechal na moři**. Hráč začíná tušit, že je to jeho syn.
4. **Doma:** námořník přizná "syn se mnou jel na tu poslední plavbu… loď šla ke
   dnu… já se dostal domů, on ne. Nevrátil jsem se pro něj." Souřadnice ostrova 3.

### Ostrov 3 — "Kde to začalo" (syn je tam). **Pořadí kroků TBD.**
1. Synova loď hlídá příjezd — **boss fight**.
2. Vylodíš se. **Syn jako NPC** (`RivalNpc`, mladší než otec, ~50).
3. Konfrontace: "poklad" syn dávno zakopal/zničil — pro něj nešlo o zlato, ale
   o to, že si otec vybral poklad / vlastní záchranu místo něj.
4. **Setkání otce a syna** — přivedeš syna, nebo přijede sám. Smíření — **volba**
   (pomoct usmířit / nechat být / vzít si zbytek pro sebe).
5. Odměna pro hráče (unikátní bonus) + `storyDone`.

---

## 2. Sdílená infrastruktura (postavit první)

### `GameData` — nová pole POUZE NA KONEC (serializují se; sdílená P1/P2)
```csharp
public int  megaIndex;      // 0/1/2 = který mega ostrov
public int  megaTask;       // 0=přijel, 1=obrana padla, 2=trezor otevřen, 3=hotovo
public int  megaCode;       // rozdělaný stav puzzlu (zakódováno)
public int  megaCluesMask;  // přečtené tabulky s vodítky (bity)
public bool ambush1Done;
public bool ambush2Done;
public bool storyDone;
```

### `MegaIslandMarker` = "mozek ostrova"
- `public static MegaIslandMarker Instance` (Awake) — jeden na hru, dosažitelný z `PlayerController`.
- `Start()`: obelisk vždy + podle `megaIndex` → `BuildFortress()` /
  `BuildWreckGraveyard()` / `BuildConfrontation()`. Deterministické pozice
  (hash z `storyIslandX/Y`, **žádný `Random` za běhu**).
- `Update()`: hlídá `megaTask` — děla + strážci zničení → `megaTask 0→1` + toast.
- `public bool TryInteract(int x, int y, int playerIndex)` — zná pozice
  obelisku/tabulek/trezoru/vzkazu, testuje `Chebyshev(hráč, cíl) ≤ 1`.

### Hák interakce
V `PlayerController.TryInteractAdjacentBuilding()` přidat na začátek jeden řádek:
`if (MegaIslandMarker.Instance != null && MegaIslandMarker.Instance.TryInteract(tx, ty, playerIndex)) return true;`

### Umístění dalšího ostrova
`StoryNpc.GiveNextIslandCoords()` (jako `GiveToSailor`): `megaIndex++`,
`megaTask=0`, `megaCluesMask=0`, `megaCode=0`, `PlaceMegaIsland(sx,sy)` daleko
deterministicky, `hasWaypoint=true`, `storyStep=2`.

### Dialog / HUD
- `StoryNpc.BuildDialogForStep()` case 3: text podle `megaTask` (dokud <3 jen
  "vrať se"; při 3 → reakce na vzkaz podle `megaIndex` → `GiveNextIslandCoords()`
  nebo finále u `megaIndex==2`).
- `HUDCounter.RefreshStory()` case 3: cíl podle `megaTask`.

### `MegaIsland` render — beze změny (`EnsureIslandTerrain` už umí, minimapa fialově).

---

## 3. Ostrov 1 — detail

`MegaIslandMarker.BuildFortress()`:
- **Děla:** 2–3× `HostileIslandCannon.Spawn(tile, "mega")` na okrajových
  dlaždicích u vody (deterministicky).
- **Strážci:** nový `LandGuard.cs` (primitiva jako sokův panáček). `hp`,
  `TakeHit(float)`, `Update()`: hráč do `RANGE` + "vidí" → `CannonBall.Fire(...,
  Side.Enemy)` každých `RELOAD` s. Na smrt → řekne markeru. 2–3 kusy u trezoru.
- **Trezor:** kamenná bedna + dveře (primitiva) vedle obelisku.
- **~6 tabulek s vodítky:** ploché kameny s textem, rozseté po ostrově.
- **Vzkaz:** svitek uvnitř trezoru, aktivní až `megaTask == 2`.

Tok: přijedeš (`storyStep 3`, `megaTask 0`) → sejmeš děla + loď z lodě
→ `megaTask 1` → pěšky → tabulky → kód na trezoru → `megaTask 2` → E na vzkaz
→ `megaTask 3` → domů → dialog → `GiveNextIslandCoords()`.

### Puzzle — 3 varianty (TBD, rozhodne se později)
1. **Kód z hádanek** (doporučeno): ~6 tabulek, 4 tvoří 4-symbolový kód + 2 návnady.
   Zadání na trezoru (cyklíš symboly E), feedback "X ze 4 správně".
2. **Sekvence aktivace:** 4–5 ohňů ve správném pořadí (z obelisku), špatně → reset.
3. **Kombinace z prostředí:** počet děl + směr stínu obelisku + datum na hrobu…

---

## 4. Mořská obluda (`SeaMonster.cs`)

Nová `SeaMonster.cs` + `StoryEvents.CheckMonster(grid)` z
`PlayerController.OnEnteredTile` (když `storyStep==2 && megaIndex>=1 &&
!ambush1Done` a hráč ~30 políček od trasy start→`storyIslandX/Y`). Stavy
`Submerged` / `Surfaced` / `Sinking`. HP + boss bar (pattern z `PirateShip.Engaged`).
Zásah = `CannonBall Side.Player`; výpad = `DamageBoat`. Po zabití `ambush1Done=true`,
odměna. **Čísla / feel TBD.**

---

## 5. Ostrov 2 & 3 — náčrt (pozdější kousky)

**Ostrov 2** (`BuildWreckGraveyard`): Holanďan hlídač (přesunout `ship-ghost.fbx`
do `Assets/Resources/`). 3 kopací místa v mělčině → `DigRoutine`-styl z lodě.
Po 3 kusech → kód do podpalubí. Jiné hádanky.

**Ostrov 3** (`BuildConfrontation`): boss loď na příjezd. Po potopení → vylodění
→ `RivalNpc.cs` (jako `StoryNpc`, syn ne stařec). Finální dialog + volba,
`storyDone=true`, odměna.

---

## 6. Rozdělení na kousky

1. **Infra** (`GameData` pole, `MegaIslandMarker` singleton + `TryInteract` hák,
   `storyStep 3` podle `megaTask`, HUD/dialog cases; ostrov zatím prázdný,
   `megaTask` jen konzolí).
2. **Ostrov 1 — obrana** (děla + `LandGuard` + `megaTask 0→1`).
3. **Ostrov 1 — trezor + puzzle** (až domyslíme puzzle; `megaTask 1→2`).
4. **Ostrov 1 — vzkaz + další souřadnice** (svitek dá waypoint na ostrov 2;
   reakce táty volitelná; `megaTask 2→3`).
5. **Mořská obluda** (`SeaMonster`).
6. **Ostrov 2** (Holanďan + dig 3 kusy + puzzle + vzkaz).
7. **Ostrov 3** (boss + `RivalNpc` syn + finále + volba + `storyDone`).
8. **Doladění** (texty, tempo, dekorace ostrovů, odměna hráči).

---

## 7. Co zapojit v editoru
- **Nic** pro ostrov 1 (vše primitiva v kódu jako `LighthouseInteriorDecor`).
- Ostrov 2: přesunout `Assets/Kenney/PirateKit/Models/ship-ghost.fbx` do
  `Assets/Resources/GhostShip/`. Řekne se uživateli krok za krokem.

## 8. Ověření (Unity MCP)
- Rozšířit konzoli: `story megatask <n>`, `story nextisland`.
- Play → `story island` → dopluj / `tp` k `storyIslandX/Y` → zkontroluj, že
  `MegaIslandMarker` postaví děla/strážce/trezor/tabulky.
- Sejmi děla (nebo `story megatask 1`) → `megaTask 1`, toast.
- Pěšky → E na tabulky / obelisk / trezor → kód → `megaTask 2` → E na vzkaz
  → `megaTask 3` → `tp` domů → dialog → další ostrov + waypoint.
- 0 chyb v konzoli po každém kroku.

## 9. Rizika / kde neztloustnout
- **Nepřidávat nový `TileType`** pro trezor/tabulky (enum se serializuje) —
  marker si je drží pozičně.
- `MegaIslandMarker` držet tenký, části do vlastních metod/souborů.
- Puzzle musí mít **férový feedback**, jinak je to hádání.
- Deterministické pozice (hash, ne `Random`).
- **Ověřit, že příběhová pole přežijí smrt/respawn**
  (`RespawnPlayerAtNearestIsland` maže jen konkrétní pole).

---

## 10. Můj názor + co ještě zvážit (k diskuzi)

Souhlasím s celkovým směrem. Syn místo obecného soka je silné (vyřeší plot-hole).

1. **Hráč je jen posel cizího dramatu** — dát mu vlastní stake: pořádná odměna
   + volba na konci, která mění koncovku i odměnu.
2. **Backtracking k tátovi** (3× přes celý oceán) — další souřadnice dát **rovnou
   do vzkazu** z trezoru; dialog s tátou udělat volitelný, ne bránu. Nebo ostrovy
   2/3 blíž / na jedné přímce.
3. **Puzzle 20 min je hodně** — mířit na 8–12 min včetně chození, většina času
   ať je zábavný průzkum, ne frustrující dedukce.
4. **Rozsah vs. maturita** — ostrov 1 vypilovat jako první milník (nový biom,
   nepřítel, puzzle, 3 beaty = super feature sám o sobě). Ostrovy 2–3 podle času;
   "ostrov 1 + pokračování příště" je obhajitelný konec.
5. **Táta ví od 1. vzkazu, že je to syn, a tají to** (popírání) — hráč si to
   skládá kolem ostrova 2. Aktivnější než když to táta odvypráví.
6. **Obluda jako opakující se hrozba** v hluboké vodě i mimo příběh? Pro teď
   nechat jen příběhovou, rozhodnout později.
