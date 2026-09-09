# Pokračování příběhu — mega ostrovy

> Návrh SCHVÁLEN + upřesněn uživatelem 2026-09-09/10. **NIC se zatím nestaví ani
> nepřidává.** Bude se dělat po malých kouskách do maturity. Souvisí s
> `.claude/handoff.md` (aktuální stav hry).

## Context

Hráč doplaje na příběhový mega ostrov (`storyStep 2 → 3`) a dostane jen hlášku
"někdo tu už kopal". Na ostrově se nedá nic dělat — je to jen země + obelisk
(`MegaIslandMarker`). Cíl: udělat z toho **reálný úkol** a rozvést příběh do
**krátkého uzavřeného oblouku přes 3 tematicky odlišné mega ostrovy**, s událostmi
na moři mezi nimi.

### Kdo je kdo (upřesněno 2026-09-10)
- **Zadavatel = DĚDA hráče** (dnešní `StoryNpc` "starý námořník" → přerámovat na
  dědu; text dialogů se upraví, třída zůstane `StoryNpc`). Hráč jede za pokladem,
  protože je to podle dědy **rodinné dědictví**.
- **Sok = DĚDŮV STARŠÍ BRATR, o kterém děda ani nevěděl, že existuje** (jejich
  otec ho zatajil / bratr zmizel dřív). Zloba bratra je vlastně vůči otci/rodině
  — poklad je pro něj to, co mu bylo upřeno. Vzkazy = testuje, jestli si pro něj
  rodina fakt přijde, nebo chce jen ten poklad.
- **Vedlejší linka — dědův syn:** děda kdysi ztratil syna na moři (žal, který
  nese). Bratr ho v jednom z finálních monologů zmíní (potkal ho / věděl o něm).
  V endingu B děda pak hráči vypráví celý příběh o synovi (hořká kóda).

### Klíčová rozhodnutí
- **Úkol na ostrově:** probojovat se přes obránce → uprostřed **trezor** s **puzzlem**
  (~10 min čistého hraní, řešitelné, férový feedback). **Konkrétní puzzle TBD.**
- **V trezoru je vzkaz od bratra + rovnou souřadnice dalšího ostrova** (žádný
  backtracking k dědovi kvůli bráně; dialog s dědou je jen volitelný komentář).
- Identita bratra se hráči **odhaluje postupně** přes 3 ostrovy.
- **Finále = setkání s bratrem, kde HRÁČ VOLÍ**, jestli ho nechá žít:
  - **Ending A (ušetří / přivede domů):** bratr jede k dědovi + poklad. Setkání
    dvou starých bratrů. Teplejší konec.
  - **Ending B (zabije):** hráč vezme poklad, dojede k dědovi sám. Děda s dědictvím
    v ruce začne vyprávět příběh o svém ztraceném synovi. Melancholický konec.
- **Rozsah:** zkusit **celé** (3 ostrovy + obluda + finále). Kdyby se nestíhalo,
  uživatel dá vědět včas a vymyslí se řez.
- **Strážci ostrova 1:** nový **stacionární typ nepřítele** (zbraně — mušketa
  apod. — se doladí později).
- **Místo přepadu piráty:** na moři **mořská obluda (megalodon styl)** —
  uhýbáš výpadům, pak se vyčerpaně zastaví na hladině a střílíš do ní.
  **Feel prototypovat brzo** (úhyb s dnešním ovládáním lodě může být nefér —
  výpad ~1.5 s telegrafovaný, velkorysé okno).

---

## 1. Příběhový oblouk

### Ostrov 1 — "Pevnost staré posádky"
Opevněný úkryt bratrovy staré posádky. Zarostlé, děla, uzamčený trezor.

1. **Připlutí** → 2–3 ostrovní děla (`HostileIslandCannon`) + 1 hlídkující
   pirátská loď po tobě pálí. Sejmeš je z lodě → doplaješ k molu.
2. **Pěšky** → nový typ nepřítele: **stacionární strážci** (`LandGuard`).
   (v1: koule jako dělo; v2: mušketa)
3. **Trezor** uprostřed, zamčený. **Puzzle — TBD**, cíl ~10 min, řešitelné,
   férový feedback. Náčrt: po ostrově jsou vodítka, hráč je nasbírá a vyluští.
4. Trezor otevřen → místo pokladu **vzkaz bratra + rovnou souřadnice ostrova 2**
   → nastaví waypoint. Vzkaz: *"Přišel jsi pozdě. Našel jsem to první. Jestli
   chceš, co je vaše, hledej mě dál."* — podepsáno iniciálou, zloba, náznak,
   že zná dědu jako kapitána. `megaTask 2 → 3`; ve save `megaIndex++`, nový ostrov.
5. **Volitelně doma:** děda pozná rukopis, zmlkne (denial — možná už tuší).
   Není to brána — hráč může jet rovnou dál.

### Mezi 1 a 2 — mořská obluda
1. **Výpady** — pluje pod hladinou (stín + brázda), telegrafovaně (~1.5 s) se
   rozjede, uhýbáš kormidlem. Zásah = velké poškození trupu.
2. **Vyčerpání** — po pár výpadech se vynoří, stojí ~8–10 s, střílíš do ní.
   Boss health bar (reuse).
3. 2–3 cykly. Po zabití odměna + trofej (zub?). Jednorázově (`ambush1Done`).

### Ostrov 2 — "Hřbitov lodí"
Vraky, mělčiny, jiná atmosféra.
1. Hlídač — "Bludný Holanďan" styl (prokletá loď, mechanika **TBD**, zatím
   ship-ghost model).
2. Puzzle **(líbí se)**: najdeš **3 kusy roztržené mapy** ve 3 vracích (kopání
   jako mega quest dig), složíš je → poloha/kód do podpalubí.
3. Uvnitř bratrův **nedávno opuštěný tábor**. 2. vzkaz — teď osobnější:
   pisatel viní **otce (= dědova a bratrova otce)**, že staršího syna zatajil /
   nechal být. Hráč začíná tušit, že je to **dědův bratr**.
4. **Volitelně doma:** děda otřesený — přizná, že o žádném starším bratrovi
   nevěděl, ale rukopis a "věci, co ví" tomu odpovídají. Souřadnice ostrova 3
   jsou už ve 2. vzkazu.

### Ostrov 3 — "Kde to začalo" (bratr je tam). **Pořadí kroků TBD.**
1. Bratrova loď hlídá příjezd — **boss fight**.
2. Vylodíš se. **Bratr jako NPC** (`RivalNpc` — starý muž, vrstevník dědy).
3. Konfrontace: monology bratra — jak ho otec zatajil, jak celý život žil na
   moři, jak sledoval rodinu z dálky. **Zmíní dědova ztraceného syna** (potkal
   ho / věděl o něm — nit do endingu B). "Poklad" = rodinné dědictví, které
   bratr měl u sebe celou dobu.
4. **Hráč VOLÍ** (nová klávesa / IMGUI dvě tlačítka):
   - **A — ušetřit:** bratr jede s tebou + poklad → setkání dvou bratrů u dědy,
     teplejší konec.
   - **B — zabít:** vezmeš poklad, dojedeš k dědovi sám → děda s dědictvím
     v ruce začne vyprávět příběh o svém ztraceném synovi, melancholický konec.
5. `storyDone = true`, `storyEnding` (1/2), odměna hráči (jiná podle endingu).

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
public int  storyEnding;    // 0=nevybráno, 1=bratr ušetřen, 2=bratr zabit
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
**Souřadnice dává TREZOR, ne děda** (žádný backtracking). `MegaIslandMarker`
při přečtení vzkazu (`megaTask 2→3`) zavolá `GridManager` metodu
`GiveNextMegaIsland()`: `megaIndex++`, `megaTask=0`, `megaCluesMask=0`,
`megaCode=0`, `PlaceMegaIsland(sx,sy)` daleko deterministicky, `hasWaypoint=true`,
`storyStep` zpět na 2 (= "pluješ k dalšímu"). U `megaIndex==2` (byl to ostrov 3)
místo toho `storyDone` větev.

### Dialog / HUD
- `StoryNpc.BuildDialogForStep()` — přerámovat "starý námořník" → **děda**.
  Case 3: text podle `megaTask` (dokud <3 jen "dokonči to, co jsi začal"; při 3
  a `megaIndex>0` → volitelný komentář k předchozímu vzkazu). Není to brána.
- `HUDCounter.RefreshStory()` case 3: cíl podle `megaTask` + `megaIndex`.

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
→ `megaTask 3` + vzkaz rovnou nastaví waypoint na ostrov 2 (`GiveNextMegaIsland()`)
→ pluješ dál (dialog s dědou je jen volitelná zastávka, ne nutná).

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
Po 3 kusech → kód do podpalubí → 2. vzkaz (viní otce, že staršího syna zatajil)
+ souřadnice ostrova 3. Jiné hádanky než ostrov 1.

**Ostrov 3** (`BuildConfrontation`): boss loď na příjezd (= bratrova loď). Po
potopení → vylodění → `RivalNpc.cs` (jako `StoryNpc`, **starý muž — vrstevník
dědy**, ne stařec-námořník vzhledově totožný). Monology (mj. zmíní dědova
ztraceného syna) → **volba hráče A ušetřit / B zabít** (nová klávesa nebo IMGUI
dvě tlačítka) → `storyDone=true`, `storyEnding = 1|2`, odměna podle endingu.
Ending A: bratr "jede s tebou" (flavor, žádná escort AI) — u dědy scéna setkání.
Ending B: bratr zmizí, u dědy monolog o ztraceném synovi.

---

## 6. Rozdělení na kousky

1. **Infra** (`GameData` pole, `MegaIslandMarker` singleton + `TryInteract` hák,
   `storyStep 3` podle `megaTask`, HUD/dialog cases; ostrov zatím prázdný,
   `megaTask` jen konzolí).
2. **Ostrov 1 — obrana** (děla + `LandGuard` + `megaTask 0→1`).
3. **Ostrov 1 — trezor + puzzle** (až domyslíme puzzle; `megaTask 1→2`).
4. **Ostrov 1 — vzkaz + další souřadnice** (svitek dá waypoint na ostrov 2;
   reakce dědy volitelná; `megaTask 2→3`).
5. **Mořská obluda** (`SeaMonster`) — feel prototypovat co nejdřív.
6. **Ostrov 2** (Holanďan + dig 3 kusy + puzzle + 2. vzkaz).
7. **Ostrov 3** (boss + `RivalNpc` bratr + monology + **volba A/B** →
   `storyDone` + `storyEnding`).
8. **Obě koncovky u dědy** (`storyEnding` větve v `StoryNpc` dialogu + odměna).
9. **Doladění** (texty, tempo, dekorace ostrovů, odměna hráči).

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
  → `megaTask 3` + waypoint na ostrov 2 se objeví hned (`GiveNextMegaIsland`),
  bez nutnosti plout domů. `tp` domů → volitelný dialog s dědou.
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

## 10. Rozhodnuté body (dřív "k diskuzi", teď odsouhlaseno 2026-09-10)

1. **Hráč má vlastní stake** — na konci **volí** (A ušetřit / B zabít), volba
   mění koncovku i odměnu. ✅ přijato.
2. **Žádný backtracking** — souřadnice dalšího ostrova jsou **rovnou ve vzkazu**
   z trezoru; dialog s dědou je jen volitelný komentář, ne brána. ✅ přijato.
3. **Puzzle ~10 min** čistého hraní, většina času zábavný průzkum (hledání
   tabulek), férový feedback. Konkrétní puzzle se **domyslí později**. ✅ přijato.
4. **Rozsah = zkusit celé** (3 ostrovy + obluda + obě koncovky). Kdyby se
   nestíhalo, uživatel dá včas vědět a udělá se řez (ostrov 1 jako první milník
   je i tak obhajitelný). ✅ přijato.
5. **Děda popírá** — od 1. vzkazu tuší / nechce věřit, že má staršího bratra;
   hráč si to skládá sám kolem ostrova 2. ✅ přijato.
6. **Vedlejší linka — dědův ztracený syn:** bratr ho zmíní v monologu na
   ostrově 3; v endingu B ji děda rozvine (kóda). ✅ přijato.

### Stále otevřené (TBD — vyřeší se při implementaci)
- Konkrétní puzzle trezoru (ostrov 1) + puzzle na ostrově 2.
- Mechanika "Bludného Holanďana" (ostrov 2).
- Přesná čísla / feel mořské obludy — prototypovat brzo, úhyb s dnešním
  ovládáním lodě může být nefér.
- Přesné pořadí kroků na ostrově 3.
- Forma volby A/B (klávesa vs. IMGUI tlačítka) a konkrétní odměny obou endingů.
- Jestli se obluda později objeví i mimo příběh (deep water = její teritorium).
