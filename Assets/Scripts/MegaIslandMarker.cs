using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  MegaIslandMarker.cs
//  "Mozek" příběhového mega ostrova. Staví kamenný obelisk (vždy) a podle
//  gameData.megaIndex (0/1/2 = který ostrov v pořadí) jeho konkrétní obsah:
//    0 = BuildFortress       — obrana (děla+strážci+loď) → trezor s puzzlem → vzkaz
//    1 = BuildWreckGraveyard — Bludný Holanďan → 3 kusy mapy → podpalubí → vzkaz
//    2 = BuildConfrontation  — bratrova loď → RivalNpc → volba → konec příběhu
//  Detail viz .claude/story-plan.md. Vytváří ho GridManager.PlaceMegaIsland
//  (a znovu po načtení save, pokud je storyIslandActive).
//
//  Ve hře je aktivní vždy nejvýš jeden mega ostrov najednou, proto stačí
//  jednoduchý statický Instance — přes něj se s ostrovem dá interagovat
//  (PlayerController) i ho testovat (GameConsole).
// ─────────────────────────────────────────────────────────────────────────────

public class MegaIslandMarker : MonoBehaviour
{
    public static MegaIslandMarker Instance { get; private set; }

    private GridManager gridManager;
    private Vector2Int  tilePos;  // políčko obelisku (= střed ostrova)
    private string      signTextCs, signTextEn; // co cedule říká (česky / anglicky) — vrátí se jako toast při interakci

    // ── Obrana ostrova 1 (Krok 2) — obsazeno jen když megaIndex == 0 ────────
    private readonly List<HostileIslandCannon> myCannons = new List<HostileIslandCannon>();
    private readonly List<LandGuard>           myGuards  = new List<LandGuard>();
    private PirateShip myGuardShip;
    private bool        defenseSpawned; // ať Update() nezačne počítat dřív, než se obrana vůbec postaví

    // ── Trezor + puzzle (Krok 3) — vzniká, až padne obrana (megaTask >= 1) ──
    private VaultMechanism vault;
    private bool           vaultBuilt;

    // ── Obrana ostrova 2 (Krok 6) — obsazeno jen když megaIndex == 1 ────────
    private PirateShip myGhostShip;
    private bool        graveyardGuardSpawned;
    private bool        ghostShipSpawnedOk; // ať se "nespawnul jsem" nesplete se "je poražený"

    // ── Kopání kusů mapy + podpalubí (Krok 6) — ostrov 2 ─────────────────────
    private readonly List<Vector2Int> digSpots       = new List<Vector2Int>(); // 3 místa v mělčině
    private readonly List<GameObject> digSpotVisuals = new List<GameObject>();
    private Vector2Int holdTile;
    private bool        holdBuilt;

    // ── Konfrontace (Krok 7) — obsazeno jen když megaIndex == 2 ──────────────
    private PirateShip myBossShip;
    private bool        bossShipSpawned;
    private bool        bossShipSpawnedOk;
    private RivalNpc    rival;
    private bool        rivalBuilt;

    private bool hasObelisk; // stojí na ostrově obelisk? (Pirátský ostrov ho nemá)

    // ── Checklist v HUD (fáze B) — kolik obrany ostrova 1 bylo na začátku ────
    private int cannonsTotal, guardsTotal, shipsTotal;
    private int lastAliveSum = -1; // změna = překreslit HUD (NotifyWorldChanged)

    // ── Stránka bratrova deníku schovaná na ostrově (fáze C, DiaryPages) ────
    private int        pageIndex = -1;   // která stránka tu leží (0/2/4), -1 = žádná
    private Vector2Int pageTile;
    private GameObject pageVisual;

    // ── Hlášky bratra během bitvy s jeho lodí (ostrov 3) ────────────────────
    private int        tauntStage;       // kolik hlášek už zaznělo (0–4)
    private GameObject graveGo;          // hrob po konci "zabít" (storyEnding 2)

    /// <summary>Stojí na políčku obelisk? Pěšák do něj nesmí vejít (neviditelná bariéra);
    /// interakce (E) funguje z vedlejšího políčka.</summary>
    public static bool BlocksWalking(int tx, int ty)
        => Instance != null && Instance.hasObelisk && tx == Instance.tilePos.x && ty == Instance.tilePos.y;

    void Awake()     { Instance = this; }
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (pageVisual != null) Destroy(pageVisual); // stránka patří ostrovu — se starým markerem zmizí
    }

    void Start()
    {
        gridManager = FindFirstObjectByType<GridManager>();
        tilePos = new Vector2Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.z));

        int megaIndex = gridManager != null ? gridManager.gameData.megaIndex : 0;

        // Pirátský ostrov (0) je pevnost s trezorem — žádný obelisk. Ostatní mega
        // ostrovy si obelisk jako orientační bod ponechávají.
        if (megaIndex != 0) { BuildObelisk(); hasObelisk = true; }

        switch (megaIndex)
        {
            case 0: BuildFortress();       break;
            case 1: BuildWreckGraveyard(); break;
            default: BuildConfrontation(); break; // 2 (a jistota pro budoucí)
        }

        // Na každém ostrově leží jedna stránka bratrova deníku (0 / 2 / 4).
        BuildDiaryPage(megaIndex == 0 ? 0 : megaIndex == 1 ? 2 : 4);
    }

    // ── Stránka bratrova deníku (DiaryPages) ─────────────────────────────────
    // Svitek na sudu s teplým světýlkem, na políčku kousek od středu ostrova (nejvzdálenější
    // ze tří nejbližších vhodných políček — to první si berou trezor / podpalubí / bratr).
    private void BuildDiaryPage(int index)
    {
        if (gridManager == null || DiaryPages.Has(gridManager.gameData, index)) return;

        var spots = FindGuardTiles(3);
        if (spots.Count < 2) return; // mrňavý ostrov — stránka se tu prostě neschová
        pageIndex = index;
        pageTile  = spots[spots.Count - 1];

        pageVisual = new GameObject("DiaryPage");
        pageVisual.transform.position = new Vector3(pageTile.x, 0f, pageTile.y);

        var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(barrel.GetComponent<Collider>());
        barrel.transform.SetParent(pageVisual.transform, false);
        barrel.transform.localPosition = new Vector3(0f, 0.25f, 0f);
        barrel.transform.localScale    = new Vector3(0.42f, 0.25f, 0.42f);
        barrel.GetComponent<MeshRenderer>().sharedMaterial = MakeMat(new Color(0.36f, 0.24f, 0.14f));

        var scroll = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(scroll.GetComponent<Collider>());
        scroll.name = "Scroll";
        scroll.transform.SetParent(pageVisual.transform, false);
        scroll.transform.localPosition    = new Vector3(0f, 0.56f, 0f);
        scroll.transform.localScale       = new Vector3(0.09f, 0.2f, 0.09f);
        scroll.transform.localEulerAngles = new Vector3(0f, 30f, 90f); // svitek leží na boku
        Material paper = MakeMat(new Color(0.93f, 0.86f, 0.68f));
        if (paper.HasProperty("_EmissionColor"))
        {
            paper.EnableKeyword("_EMISSION");
            paper.SetColor("_EmissionColor", new Color(0.5f, 0.4f, 0.2f)); // lehce "svítí", ať jde najít
        }
        scroll.GetComponent<MeshRenderer>().sharedMaterial = paper;

        var lightGo = new GameObject("PageLight");
        lightGo.transform.SetParent(pageVisual.transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(1f, 0.8f, 0.45f); l.range = 4f; l.intensity = 1.6f;
    }

    // Sebrání stránky (E vedle sudu).
    private void CollectDiaryPage()
    {
        var d = gridManager.gameData;
        if (DiaryPages.Collect(d, pageIndex))
        {
            gridManager.Save();
            SoundManager.PlayChapterChord();
            if (CombatDirector.Instance != null) CombatDirector.Instance.Toast(DiaryPages.FoundToast(d), 5f);
        }
        if (pageVisual != null) Destroy(pageVisual);
        pageIndex = -1;
    }

    // Sečti zničenou obranu → megaTask 0→1. Ostrov pirátů (děla+strážci+loď) i
    // ostrov 2 (jen hlídkující Holanďan) mají svojí vlastní obranu, ale stejný
    // princip — obě větve běží nezávisle, akorát pro každý ostrov je vždy
    // aktivní jen jedna z nich (ta druhá nemá co spawnout).
    void Update()
    {
        if (gridManager == null) return;
        var d = gridManager.gameData;
        if (d.megaTask != 0) return;

        if (defenseSpawned)
        {
            myCannons.RemoveAll(c => c == null);
            myGuards.RemoveAll(g => g == null);

            // Něco z obrany padlo → překresli HUD (checklist "Děla 1/3 · Stráže 2/3").
            int alive = myCannons.Count + myGuards.Count + (myGuardShip != null ? 1 : 0);
            if (alive != lastAliveSum) { lastAliveSum = alive; gridManager.NotifyWorldChanged(); }

            if (myCannons.Count == 0 && myGuards.Count == 0 && myGuardShip == null)
            {
                d.megaTask = 1;
                gridManager.Save();
                gridManager.NotifyWorldChanged();
                if (CombatDirector.Instance != null) CombatDirector.Instance.Toast(Loc.T("Obrana ostrova padla. Prohledej ho dál.", "The island's defenses have fallen. Keep searching it."));
                BuildVaultIfNeeded();
            }
        }
        else if (graveyardGuardSpawned && ghostShipSpawnedOk && myGhostShip == null)
        {
            d.megaTask = 1;
            gridManager.Save();
            gridManager.NotifyWorldChanged();
            if (CombatDirector.Instance != null) CombatDirector.Instance.Toast(Loc.T("Bludný Holanďan je poražen. Mělčina teď skrývá kousky mapy.", "The Flying Dutchman is defeated. The shoals now hide pieces of the map."));
            SpawnDigSpots();
        }
        else if (bossShipSpawned && bossShipSpawnedOk && myBossShip != null)
        {
            UpdateBrotherTaunts();
        }
        else if (bossShipSpawned && bossShipSpawnedOk && myBossShip == null)
        {
            d.megaTask = 1;
            gridManager.Save();
            gridManager.NotifyWorldChanged();
            if (CombatDirector.Instance != null) CombatDirector.Instance.Toast(Loc.T("Bratrova loď je potopená. Bratr se vylodil na ostrov.", "The brother's ship is sunk. He has come ashore."));
            SpawnRival();
        }
    }

    // Postaví trezor. Volá se hned při vzniku ostrova (BuildFortress) —
    // nečeká už na megaTask >= 1 (padlou obranu), viz tamní komentář.
    private void BuildVaultIfNeeded()
    {
        if (vaultBuilt || gridManager == null) return;
        vaultBuilt = true;

        Vector2Int spot = tilePos; // nouzovka, kdyby se nenašlo nic lepšího
        var candidates = FindGuardTiles(1);
        if (candidates.Count > 0) spot = candidates[0];

        bool alreadySolved = gridManager.gameData.megaTask >= 2;
        vault = VaultMechanism.Spawn(spot, alreadySolved);
    }

    // ── Obsah ostrova podle megaIndex — viz rozpis v hlavičce souboru ─────
    private void BuildFortress()
    {
        BuildSign("Pirátský ostrov — pevnost staré posádky. Hlídá ji ozbrojená posádka a děla na věžích, trezor je někde uvnitř.",
                  "Pirate Island — the old crew's fortress. An armed crew and tower cannons guard it; the vault is somewhere inside.",
                   new Color(0.5f, 0.24f, 0.18f), true);

        if (gridManager == null) return;

        // Hradby + věže jsou čistě kosmetické (nezávisí na megaTask) — stavíme
        // je vždy, i po reloadu, kdy je obrana už dávno poražená. Když hradba
        // v týhle scéně už stojí (další "Nová hra" bez restartu Unity),
        // BuildWalls ji podruhé nepostaví, ale pozice věží (deterministické)
        // vrátí i tak — podle nich se dole rozmisťují děla.
        BuildWalls();
        BuildYard(); // chatrče, bedny, vlajky… (musí být před strážci — ti se vyhnou obsazeným políčkům)

        // Trezor je přístupný HNED, i když obrana ještě stojí (na žádost
        // playtestu — nemá smysl nutit hráče vyřídit VŠECHNA děla/strážce/
        // loď, než se vůbec dostane k puzzlu). BuildVaultIfNeeded si sám
        // hlídá, že se nepostaví dvakrát.
        BuildVaultIfNeeded();

        // Obrana se staví, jen když ještě nebyla vyřízená (staré savy po
        // reloadu ať znovu nespawnou už poražené hlídky).
        if (gridManager.gameData.megaTask > 0) return;

        // 2–3 strážci, deterministicky podle pozice ostrova. Děla teď sedí NA
        // věžích (viz PlaceCannonsOnTowers), ne na okraji ostrova jako dřív.
        int seed = Mathf.Abs(unchecked(tilePos.x * 73856093 ^ tilePos.y * 19349663));
        int guardCount = 2 + (seed / 2) % 2;

        PlaceCannonsOnTowers();

        Vector2Int? guardSpot = FindGuardWaterSpot();
        if (guardSpot != null)
        {
            var w = guardSpot.Value;
            myGuardShip = PirateShip.Spawn(new Vector3(w.x, 0f, w.y), 1); // střední loď
            myGuardShip.SetGuard(new Vector3(w.x, 0f, w.y));
            myGuardShip.guardIslandKey = "mega";
            if (CombatDirector.Instance != null) CombatDirector.Instance.RegisterGuardShip(myGuardShip);
        }

        // Strážci rozeseti po CELÉM ostrově (ne namačkaní u obelisku) — ať
        // nepůsobí "bugle" natlačení na hromadě. Trezor pro FindGuardTiles(1)
        // zůstává těsně u středu (viz BuildVaultIfNeeded), strážci teď hledají
        // v širším prstenci kolem celého ostrova.
        foreach (var spot in FindScatteredGuardTiles(guardCount))
            myGuards.Add(LandGuard.Spawn(spot, "mega"));

        // Kolik obrany bylo na začátku — HUD z toho skládá checklist (GetDefenseStatus).
        cannonsTotal   = myCannons.Count;
        guardsTotal    = myGuards.Count;
        shipsTotal     = myGuardShip != null ? 1 : 0;
        defenseSpawned = true;
    }

    /// <summary>Stav obrany Pirátského ostrova pro checklist v HUD: kolik čeho ještě
    /// žije (…Left) a kolik toho bylo na začátku (…All). Po reloadu s už padlou
    /// obranou jsou všechny nuly — HUD pak podle megaTask ukáže "hotovo".</summary>
    public void GetDefenseStatus(out int cannonsLeft, out int cannonsAll, out int guardsLeft, out int guardsAll,
                                 out int shipsLeft, out int shipsAll)
    {
        cannonsLeft = 0; guardsLeft = 0;
        foreach (var c in myCannons) if (c != null) cannonsLeft++;
        foreach (var g in myGuards)  if (g != null) guardsLeft++;
        shipsLeft  = myGuardShip != null ? 1 : 0;
        cannonsAll = cannonsTotal;
        guardsAll  = guardsTotal;
        shipsAll   = shipsTotal;
    }

    // Postaví děla na věže, které je mají mít (cannonTowerIdx — vybrané v BuildWalls,
    // deterministicky podle pozice ostrova). Na zbořenou věž se dělo nestaví.
    // HostileIslandCannon.IsNear počítá jen vodorovnou vzdálenost, takže i vysoko
    // posazené dělo jde trefit (AimAssist na něj navede náměr).
    private void PlaceCannonsOnTowers()
    {
        foreach (var t in towers)
        {
            if (t == null || !t.Standing || !cannonTowerIdx.Contains(t.Index)) continue;
            Vector3 p = t.transform.position;
            var cannon = HostileIslandCannon.Spawn(new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.z)), "mega");
            cannon.transform.position = new Vector3(p.x, FortressTower.CANNON_HEIGHT, p.z);
            t.Cannon = cannon;
            myCannons.Add(cannon);
            if (CombatDirector.Instance != null) CombatDirector.Instance.RegisterCannon(cannon);
        }
    }

    private static int TowerPickKey(int seed, int i) => unchecked((seed + i * 40503) * 73856093);

    // Pár políček pevniny blíž ke středu ostrova (u budoucího trezoru), kam
    // postavit trezor/podpalubí/bratra. Podobný postup jako u děl, jen blíž středu.
    private List<Vector2Int> FindGuardTiles(int maxCount)
    {
        var cand = new List<Vector2Int>();
        for (int x = tilePos.x - 6; x <= tilePos.x + 6; x++)
            for (int y = tilePos.y - 6; y <= tilePos.y + 6; y++)
            {
                // Obelisk má 3×3 j širokou základnu — políčko hned vedle (vzdálenost
                // 1) se s ní vizuálně překrývá, tak držet aspoň 2 políčka od středu.
                if (Mathf.Max(Mathf.Abs(x - tilePos.x), Mathf.Abs(y - tilePos.y)) < 2) continue;
                if (gridManager.GetTileType(x, y) != TileType.MegaIsland) continue;
                cand.Add(new Vector2Int(x, y));
            }
        cand.Sort((a, b) => DistSqToCenter(a) - DistSqToCenter(b));

        var pick = new List<Vector2Int>();
        foreach (var c in cand)
        {
            bool tooClose = false;
            foreach (var p in pick)
                if (Mathf.Abs(p.x - c.x) < 3 && Mathf.Abs(p.y - c.y) < 3) { tooClose = true; break; }
            if (tooClose) continue;
            pick.Add(c);
            if (pick.Count >= maxCount) break;
        }
        return pick;
    }

    // Strážci rozeseti po CELÉM ostrově místo namačkaní u obelisku — vezmi
    // všechna pevninová políčka aspoň 2 od středu (kvůli základně obelisku),
    // zamíchej je deterministicky (hash pozice ostrova, ŽÁDNÝ Random.value —
    // stejný ostrov musí po reloadu vypadat stejně) a vyber prvních maxCount
    // s dostatečným rozestupem, ať nestojí dva strážci na jednom políčku.
    private List<Vector2Int> FindScatteredGuardTiles(int maxCount)
    {
        var cand = new List<Vector2Int>();
        for (int x = tilePos.x - 15; x <= tilePos.x + 15; x++)
            for (int y = tilePos.y - 15; y <= tilePos.y + 15; y++)
            {
                if (Mathf.Max(Mathf.Abs(x - tilePos.x), Mathf.Abs(y - tilePos.y)) < 2) continue;
                if (gridManager.GetTileType(x, y) != TileType.MegaIsland) continue;
                // Jen uvnitř hradeb (ne ve zdi ani na pláži za ní) a ne v chatrči / na bednách.
                if (!IsInnerTile(x, y, 1) || yardBlocked.Contains(new Vector2Int(x, y))) continue;
                cand.Add(new Vector2Int(x, y));
            }

        // Deterministické "zamíchání" — ke každé dlaždici přiřaď hash-klíč a
        // seřaď podle něj. Stejný ostrov (stejné tilePos) dá pokaždé stejné
        // pořadí, ale to pořadí není podle vzdálenosti od středu jako dřív.
        cand.Sort((a, b) => ScatterKey(a).CompareTo(ScatterKey(b)));

        var pick = new List<Vector2Int>();
        foreach (var c in cand)
        {
            bool tooClose = false;
            foreach (var p in pick)
                if (Mathf.Abs(p.x - c.x) < 3 && Mathf.Abs(p.y - c.y) < 3) { tooClose = true; break; }
            if (tooClose) continue;
            pick.Add(c);
            if (pick.Count >= maxCount) break;
        }
        return pick;
    }

    private static int ScatterKey(Vector2Int t) => unchecked(t.x * 374761393 ^ t.y * 668265263);

    private int DistSqToCenter(Vector2Int p)
        => (p.x - tilePos.x) * (p.x - tilePos.x) + (p.y - tilePos.y) * (p.y - tilePos.y);

    private void BuildWreckGraveyard()
    {
        BuildSign("Hřbitov lodí — v mělčině kolem se ukrývají kousky roztržené mapy. Hledej trosky trčící z vody.",
                  "Ship Graveyard — pieces of a torn map hide in the shoals around it. Look for wrecks sticking out of the water.",
                   new Color(0.32f, 0.36f, 0.42f));

        if (gridManager == null) return;
        var d = gridManager.gameData;

        // Bludný Holanďan už tady NEhlídkuje — napadne hráče až při odjezdu z tohoto
        // ostrova (viz StoryEvents.CheckGhost). Kousky mapy jsou proto dostupné hned po
        // příjezdu: megaTask 0 → 1. Po reloadu se rovnou obnoví, co z ostrova zbývá.
        if (d.megaTask == 0)
        {
            d.megaTask = 1;
            gridManager.Save();
            gridManager.NotifyWorldChanged();
        }

        SpawnDigSpots();
        if (d.megaTask >= 2) BuildHoldIfNeeded();
    }

    // Vodní dlaždice na hlídku (strážce/Holanďan) — nejdřív zkus blízký pás
    // (GetGuardWaterSpots), když nic nenajde, prohledej širší okolí. Ostrov
    // vždycky obklopuje moře, takže tohle prakticky vždy něco najde.
    private Vector2Int? FindGuardWaterSpot()
    {
        var near = gridManager.GetGuardWaterSpots(tilePos, 1);
        if (near.Count > 0) return near[0];

        for (int r = 14; r <= 40; r += 4)
            for (int x = tilePos.x - r; x <= tilePos.x + r; x++)
                for (int y = tilePos.y - r; y <= tilePos.y + r; y++)
                {
                    if (Mathf.Max(Mathf.Abs(x - tilePos.x), Mathf.Abs(y - tilePos.y)) != r) continue;
                    var t = gridManager.GetTileType(x, y);
                    if (t == TileType.Water || t == TileType.Water_Fish) return new Vector2Int(x, y);
                }
        return null;
    }

    // 3 místa v mělčině (vodní dlaždice hned u ostrova) na kousky roztržené
    // mapy — deterministicky podle pozice ostrova. Už sebrané (bit v
    // megaCluesMask) nedostanou vizuál a nejdou znovu vykopat.
    private void SpawnDigSpots()
    {
        if (digSpots.Count > 0 || gridManager == null) return; // už postaveno (reload-safety)

        var cand = new List<Vector2Int>();
        for (int x = tilePos.x - 15; x <= tilePos.x + 15; x++)
            for (int y = tilePos.y - 15; y <= tilePos.y + 15; y++)
            {
                var t = gridManager.GetTileType(x, y);
                if (t != TileType.Water && t != TileType.Water_Fish) continue;
                bool nextToLand = gridManager.GetTileType(x + 1, y) == TileType.MegaIsland
                                || gridManager.GetTileType(x - 1, y) == TileType.MegaIsland
                                || gridManager.GetTileType(x, y + 1) == TileType.MegaIsland
                                || gridManager.GetTileType(x, y - 1) == TileType.MegaIsland;
                if (nextToLand) cand.Add(new Vector2Int(x, y));
            }
        cand.Sort((a, b) => DistSqToCenter(a) - DistSqToCenter(b));

        int mask = gridManager.gameData.megaCluesMask;
        for (int i = 0; i < cand.Count && digSpots.Count < 3; i++)
        {
            var c = cand[i];
            bool tooClose = false;
            foreach (var p in digSpots)
                if (Mathf.Abs(p.x - c.x) < 4 && Mathf.Abs(p.y - c.y) < 4) { tooClose = true; break; }
            if (tooClose) continue;

            int idx = digSpots.Count;
            digSpots.Add(c);
            if ((mask & (1 << idx)) != 0) { digSpotVisuals.Add(null); continue; } // už dřív vykopáno

            var wreck = BuildWreckPiece(c);
            digSpotVisuals.Add(wreck);
        }
    }

    // Kus vraku trčící z vody — orientační bod pro kopání (primitiva).
    private GameObject BuildWreckPiece(Vector2Int tile)
    {
        var go = new GameObject("WreckPiece");
        go.transform.position = new Vector3(tile.x, -0.1f, tile.y);
        Material wood = MakeMat(new Color(0.32f, 0.22f, 0.15f));

        var plank = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plank.name = "Plank";
        var col = plank.GetComponent<Collider>();
        if (col != null) Destroy(col);
        plank.transform.SetParent(go.transform, false);
        plank.transform.localScale       = new Vector3(1.3f, 0.12f, 0.35f);
        plank.transform.localEulerAngles = new Vector3(0f, 25f, 12f);
        plank.GetComponent<MeshRenderer>().sharedMaterial = wood;

        var mast = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        mast.name = "Mast";
        var mc = mast.GetComponent<Collider>();
        if (mc != null) Destroy(mc);
        mast.transform.SetParent(go.transform, false);
        mast.transform.localPosition    = new Vector3(0.3f, 0.5f, 0f);
        mast.transform.localScale       = new Vector3(0.08f, 0.6f, 0.08f);
        mast.transform.localEulerAngles = new Vector3(0f, 0f, 20f);
        mast.GetComponent<MeshRenderer>().sharedMaterial = wood;

        return go;
    }

    /// <summary>Je na daném políčku nesebraný kus mapy? (pro nápovědu i pro
    /// PlayerController.TryInteract — Space na lodi.)</summary>
    public bool HasDigSpot(int x, int y)
    {
        if (gridManager == null || gridManager.gameData.megaTask != 1) return false;
        int mask = gridManager.gameData.megaCluesMask;
        for (int i = 0; i < digSpots.Count; i++)
            if (digSpots[i].x == x && digSpots[i].y == y && (mask & (1 << i)) == 0)
                return true;
        return false;
    }

    /// <summary>Vykope kus mapy na daném políčku (volá PlayerController po
    /// doběhnutí kopací animace). Když sebere poslední kus, rovnou postaví
    /// podpalubí a posune megaTask 1→2.</summary>
    public void TryDig(int x, int y, int playerIndex)
    {
        if (!HasDigSpot(x, y)) return;

        int idx = digSpots.FindIndex(p => p.x == x && p.y == y);
        gridManager.gameData.megaCluesMask |= 1 << idx;
        gridManager.Save();

        if (digSpotVisuals[idx] != null) Destroy(digSpotVisuals[idx]);
        SoundManager.PlayCoin();

        int have = PieceCount();
        if (have < 3)
        {
            if (CombatDirector.Instance != null) CombatDirector.Instance.Toast(Loc.T($"Kousek roztržené mapy! ({have}/3)", $"A piece of the torn map! ({have}/3)"));
            return;
        }

        gridManager.gameData.megaTask = 2;
        gridManager.Save();
        gridManager.NotifyWorldChanged();
        if (CombatDirector.Instance != null) CombatDirector.Instance.Toast(Loc.T("Mapa je kompletní! Kód vede do podpalubí vraku.", "The map is complete! The code leads to the wreck's hold."), 4f);
        BuildHoldIfNeeded();
    }

    private int PieceCount()
    {
        int mask = gridManager.gameData.megaCluesMask;
        int n = 0;
        for (int i = 0; i < 3; i++) if ((mask & (1 << i)) != 0) n++;
        return n;
    }

    // Podpalubí vraku — vzniká, až je mapa kompletní (megaTask >= 2). Stejný
    // princip jako trezor na ostrově 1 (BuildVaultIfNeeded).
    private void BuildHoldIfNeeded()
    {
        if (holdBuilt || gridManager == null || gridManager.gameData.megaTask < 2) return;
        holdBuilt = true;

        var candidates = FindGuardTiles(1);
        holdTile = candidates.Count > 0 ? candidates[0] : tilePos;

        Material wood = MakeMat(new Color(0.25f, 0.17f, 0.12f));
        var go = new GameObject("WreckHold");
        go.transform.position = new Vector3(holdTile.x, 0f, holdTile.y);
        var hull = GameObject.CreatePrimitive(PrimitiveType.Cube);
        hull.name = "Hull";
        var col = hull.GetComponent<Collider>();
        if (col != null) Destroy(col);
        hull.transform.SetParent(go.transform, false);
        hull.transform.localPosition    = new Vector3(0f, 0.3f, 0f);
        hull.transform.localScale       = new Vector3(1.6f, 0.6f, 1.0f);
        hull.transform.localEulerAngles = new Vector3(0f, 0f, 18f);
        hull.GetComponent<MeshRenderer>().sharedMaterial = wood;
    }

    private void BuildConfrontation()
    {
        BuildSign("Poslední ostrov — kde všechno začalo. Bratrova loď hlídá příjezd.",
                  "Final Island — where it all began. The brother's ship guards the approach.",
                   new Color(0.52f, 0.44f, 0.16f));

        if (gridManager == null) return;
        var d = gridManager.gameData;

        // Po finále: bratr tu už nestojí — buď odplul s hráčem domů (konec 1),
        // nebo po něm zůstal hrob (konec 2). Viz ApplyEnding.
        if (d.storyDone) { if (d.storyEnding == 2) BuildGrave(); return; }

        // Po reloadu: loď se staví jen jednou, jinak rovnou obnov bratra.
        if (d.megaTask > 0) { SpawnRival(); return; }

        Vector2Int? bossSpot = FindGuardWaterSpot();
        if (bossSpot != null)
        {
            var w = bossSpot.Value;
            myBossShip = PirateShip.Spawn(new Vector3(w.x, 0f, w.y), 2); // velká loď
            myBossShip.isBrotherShip = true; // jiné jméno na boss baru
            myBossShip.SetGuard(new Vector3(w.x, 0f, w.y));
            myBossShip.guardIslandKey = "mega";
            if (CombatDirector.Instance != null) CombatDirector.Instance.RegisterGuardShip(myBossShip);
            bossShipSpawnedOk = true;
        }
        else
        {
            // Nouzovka — viz stejný postup u FindGuardWaterSpot v BuildWreckGraveyard.
            d.megaTask = 1;
            gridManager.Save();
            SpawnRival();
        }
        bossShipSpawned = true;
    }

    // ── Finále: bratr volá z paluby během bitvy (fáze C) ──────────────────────
    // Čtyři hlášky: když loď poprvé zaútočí a když jí klesne zdraví pod 70 / 40 / 15 %.
    // Toast "Hlas z paluby" — hráč tak ví, s kým bojuje, a monolog na břehu pak navazuje.
    private void UpdateBrotherTaunts()
    {
        var cd = CombatDirector.Instance;
        if (cd == null) return;

        float hp = myBossShip.HpFraction;
        int want = (!myBossShip.Engaged && tauntStage == 0) ? 0
                 : hp > 0.70f ? 1 : hp > 0.40f ? 2 : hp > 0.15f ? 3 : 4;
        if (want <= tauntStage) return;
        tauntStage = want;

        string voice = Loc.T("Hlas z paluby: ", "A voice from the deck: ");
        string line;
        switch (tauntStage)
        {
            case 1:  line = Loc.T("„Otoč to, dokud můžeš! Tenhle ostrov je můj!“", "“Turn back while you still can! This island is mine!”"); break;
            case 2:  line = Loc.T("„Starý tě poslal, co? Sám si netroufl přijet?“", "“The old man sent you, did he? Too afraid to come himself?”"); break;
            case 3:  line = Loc.T("„Tuhle loď jsem čtyřicet let skládal z trosek, co moře vyplavilo!“", "“I spent forty years building this ship from wreckage the sea washed up!”"); break;
            default: line = Loc.T("„Dost… dost. Přistaň. Promluvíme si na břehu.“", "“Enough… enough. Come ashore. We will talk on land.”"); break;
        }
        cd.Toast(voice + line, 5f);
    }

    /// <summary>
    /// Viditelný konec příběhu na Posledním ostrově (volá RivalNpc po zavření dialogu s volbou).
    /// Konec 1 (ušetřit): bratr zmizí — odplul s hráčem, objeví se doma u dědy (StoryNpc).
    /// Konec 2 (zabít): na jeho místě zůstane hrob.
    /// </summary>
    public void ApplyEnding(int ending)
    {
        if (rival != null) Destroy(rival.gameObject);
        rival = null;
        if (ending == 2) BuildGrave();
    }

    // Hrob — hromádka kamenů a dřevěný kříž na místě, kde stál bratr.
    private void BuildGrave()
    {
        if (graveGo != null || gridManager == null) return;
        var candidates = FindGuardTiles(1);
        Vector2Int spot = candidates.Count > 0 ? candidates[0] : tilePos;

        graveGo = new GameObject("BrotherGrave");
        graveGo.transform.SetParent(transform, false);
        graveGo.transform.position = new Vector3(spot.x, 0f, spot.y);

        Material stone = MakeMat(new Color(0.45f, 0.44f, 0.42f));
        Material wood  = MakeMat(new Color(0.30f, 0.20f, 0.12f));
        GravePart(PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0f),      new Vector3(0.9f, 0.25f, 1.3f),  stone);
        GravePart(PrimitiveType.Sphere, new Vector3(0.25f, 0.14f, 0.3f), new Vector3(0.3f, 0.2f, 0.3f),   stone);
        GravePart(PrimitiveType.Cube,   new Vector3(0f, 0.55f, -0.55f),  new Vector3(0.08f, 0.9f, 0.08f), wood);
        GravePart(PrimitiveType.Cube,   new Vector3(0f, 0.75f, -0.55f),  new Vector3(0.45f, 0.08f, 0.08f), wood);
    }

    private void GravePart(PrimitiveType type, Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(graveGo.transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    // Bratr se objeví na pevném políčku poblíž obelisku, jakmile jeho loď padne.
    private void SpawnRival()
    {
        if (rivalBuilt || gridManager == null) return;
        rivalBuilt = true;

        var candidates = FindGuardTiles(1);
        Vector2Int spot = candidates.Count > 0 ? candidates[0] : tilePos;
        rival = RivalNpc.Spawn(spot);
    }

    // Dřevěná cedule kousek od obelisku — jen orientační, dokud nevznikne
    // skutečný obsah ostrova. Text se ukáže jako toast při interakci (E).
    // atCenter = cedule stojí přesně na středu ostrova (tam, kde se na E ozve její text) —
    // používá Pirátský ostrov, který nemá obelisk. Jinak stojí kousek od obelisku.
    private void BuildSign(string csText, string enText, Color boardColor, bool atCenter = false)
    {
        signTextCs = csText;
        signTextEn = enText;

        Material wood  = MakeMat(new Color(0.35f, 0.24f, 0.14f));
        Material board = MakeMat(boardColor);

        float o = atCenter ? 0f : 1.8f;
        Box("SignPost",  new Vector3(o, 0.6f,  o), new Vector3(0.12f, 1.2f, 0.12f), wood);
        Box("SignBoard", new Vector3(o, 1.25f, o), new Vector3(1.1f,  0.6f, 0.08f),  board);
    }

    // ── Hradby ostrova 1 (Kenney Pirate Kit) ─────────────────────────────────
    // Hradba přesně KOPÍRUJE pobřeží ostrova — žádné "nahazování políček podle
    // úhlu" (to dělalo duplicity/překryvy v zátokách, kde je víc pobřežních
    // dlaždic ve stejném směru). Místo toho:
    //   1) TraceIslandBoundary  — obejde skutečnou hranici pevnina/voda po
    //      jednotkových hranách dlaždic (marching-squares styl) a vrátí ji
    //      jako uzavřenou smyčku rohových bodů, popořadě.
    //   2) MergeIntoStraightRuns — poslepuje po sobě jdoucí hrany stejného
    //      směru do rovných úseků (rovné pobřeží = jeden dlouhý úsek).
    //   3) Podél každého úseku se rozmístí PŘESNĚ tolik dílů hradby, kolik má
    //      dlaždic — díly na sebe navazují bez mezery i bez překryvu, protože
    //      Kenney hradba je při WALL_SCALE přesně 1.0 j široká = 1 dlaždice.
    // Hradby blokují střely (BlocksShot) i chůzi pěšáka (BlocksWalkingAt) — modely
    // samy collider nemají, kolize se počítá z tvaru úseků (shotRuns).
    // ── Střely a chůze vs. hradby ──────────────────────────────────────────
    // Tvar hradeb posledního postaveného ostrova (statické — hradby stojí i po
    // zničení markeru). CannonBall se přes BlocksShot ptá, jestli střela do něčeho
    // narazila; PlayerController přes BlocksWalkingAt, jestli pěšák nejde do zdi.
    private static List<WallRun> shotRuns;
    private static int     gateRunIndex = -1;   // úsek hradby s bránou
    private static float   gateAlong;           // kde na tom úseku je střed brány (vzdálenost od začátku)
    private static Rect    fortressBounds;      // obdélník kolem hradeb (rychlé "jsem vůbec u pevnosti?")
    private static readonly HashSet<Vector2Int> yardBlocked = new HashSet<Vector2Int>(); // políčka s chatrčí / bednami
    private const float WALL_SHOT_HEIGHT = 1.8f;  // do jaké výšky zeď střelu zastaví (výš přeletí)
    private const float WALK_BLOCK       = 0.95f; // půl tloušťky zdi + poloměr panáčka
    private const float GATE_HALF_OPEN   = 0.8f;  // polovina průchodu v bráně (molo je 2 dlaždice široké)

    /// <summary>Narazila by střela v bodě "p" do hradby nebo věže? (střela letící vysoko zeď přeletí)
    /// Hráčova dělová koule (`playerShot`, `heavy`) věž zároveň poškodí — viz FortressTower.</summary>
    public static bool BlocksShot(Vector3 p, bool playerShot = false, float damage = 0f, bool heavy = false)
    {
        // Věže (stojící do své výšky, sutiny jen nízko).
        foreach (var tw in FortressTower.All)
        {
            if (tw == null || !tw.Contains(p)) continue;
            if (playerShot) tw.TakeHit(p, damage, heavy);
            return true;
        }

        if (shotRuns != null && p.y <= WALL_SHOT_HEIGHT && fortressBounds.Contains(new Vector2(p.x, p.z)))
        {
            Vector2 pt = new Vector2(p.x, p.z);
            foreach (var run in shotRuns)
            {
                if (DistToRun(run, pt, out _) > WALL_HALF_DEPTH) continue;
                // Věže stojí v rozích, kde se sbíhají zdi — koule letící na věž často
                // narazí nejdřív do zdi těsně u ní. To se počítá jako zásah věže.
                if (playerShot)
                {
                    FortressTower near = NearestStandingTower(pt, FortressTower.RADIUS + 0.9f);
                    if (near != null) near.TakeHit(p, damage, heavy);
                }
                return true;
            }
        }
        return false;
    }

    // Nejbližší stojící věž do vzdálenosti `range` od bodu (nebo null).
    private static FortressTower NearestStandingTower(Vector2 pt, float range)
    {
        FortressTower best = null;
        float bestSq = range * range;
        foreach (var tw in FortressTower.All)
        {
            if (tw == null || !tw.Standing) continue;
            float sq = (new Vector2(tw.transform.position.x, tw.transform.position.z) - pt).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = tw; }
        }
        return best;
    }

    /// <summary>
    /// Smí pěšák udělat krok z `from` do `to`? Vrací true, když by vešel do zdi, věže
    /// nebo do chatrče/beden na nádvoří. Brána u mola je průchozí. Blokuje se jen krok
    /// SMĚREM do překážky — kdyby panáček (třeba po načtení hry) stál ve zdi, z ní vyjde.
    /// </summary>
    public static bool BlocksWalkingAt(Vector3 from, Vector3 to)
    {
        if (shotRuns == null) return false;
        Vector2 a = new Vector2(from.x, from.z), b = new Vector2(to.x, to.z);
        if (!fortressBounds.Contains(b)) return false; // daleko od pevnosti — nic neřeš

        for (int i = 0; i < shotRuns.Count; i++)
        {
            float distTo = DistToRun(shotRuns[i], b, out float along);
            if (distTo >= WALK_BLOCK) continue;
            if (i == gateRunIndex && Mathf.Abs(along - gateAlong) < GATE_HALF_OPEN) continue; // průchod bránou
            if (distTo < DistToRun(shotRuns[i], a, out _)) return true;
        }

        foreach (var tw in FortressTower.All)
        {
            if (tw == null) continue;
            Vector2 c = new Vector2(tw.transform.position.x, tw.transform.position.z);
            float r = FortressTower.RADIUS + 0.2f;
            if ((b - c).sqrMagnitude < r * r && (b - c).sqrMagnitude < (a - c).sqrMagnitude) return true;
        }

        var tb = new Vector2Int(Mathf.RoundToInt(to.x), Mathf.RoundToInt(to.z));
        var ta = new Vector2Int(Mathf.RoundToInt(from.x), Mathf.RoundToInt(from.z));
        return yardBlocked.Contains(tb) && !yardBlocked.Contains(ta);
    }

    // Vzdálenost bodu od osy úseku hradby (konce úseku jsou "zaoblené"); `along` = kde na úseku.
    private static float DistToRun(WallRun run, Vector2 pt, out float along)
    {
        float len = Mathf.Max(0f, run.EffLength);
        along = Mathf.Clamp(Vector2.Dot(pt - run.EffStart, run.Dir), 0f, len);
        return (pt - (run.EffStart + run.Dir * along)).magnitude;
    }

    private const float WALL_SCALE = 0.5f; // Kenney hradba/brána jsou při scale 1 přes 2/4 j — 0.5 = přesně 1.0/2.0 j (1/2 dlaždice)

    // Věže postavené pro tenhle ostrov (pořadí = index v GameData.fortressTowersDown).
    private readonly List<FortressTower> towers = new List<FortressTower>();
    private readonly HashSet<int> cannonTowerIdx = new HashSet<int>(); // na kterých věžích stojí dělo

    // Postaví hradby po obvodu ostrova, bránu přesně proti molu a strážní věže v rozích.
    // Vrací pozice věží (world XZ) — BuildFortress na některé z nich posadí dělo.
    private List<Vector2> BuildWalls()
    {
        // Pojistka proti duplicitám: hradby NEJSOU potomkem markeru (viz níže),
        // takže je GiveNextMegaIsland nezničí spolu s ním — ale to znamená, že
        // opakované volání BuildFortress pro STEJNÝ ostrov (např. po "Nová
        // hra"/"Pokračovat" bez restartu Unity) by je postavilo podruhé na
        // sebe. Kontrola podle jména v AKTUÁLNÍ scéně (ne statické pole — to
        // by přežívalo i Stop/Play, kde se scéna vždy staví od nuly).
        // Tvar hradby, brána i věže se ale počítají VŽDY (deterministicky z tvaru ostrova)
        // — podle nich se blokuje chůze a rozmisťují děla.
        string wallsName  = $"Walls_{tilePos.x}_{tilePos.y}";
        string towersName = $"Towers_{tilePos.x}_{tilePos.y}";
        bool wallsAlreadyBuilt = GameObject.Find(wallsName) != null;

        var loop = TraceIslandBoundary();
        if (loop == null) return null;
        var runs = MergeIntoStraightRuns(loop);
        if (runs.Count == 0) return null;

        // Věže na nejvýraznějších rozích (až 7, rozmístěné po obvodu).
        var towerCorners = PickTowerCorners(runs, 7);

        // Rohy: zeď je tlustá, takže bez úpravy by u každého rohu chyběl vnější
        // výřez a uvnitř by se dva díly překrývaly (viz MiterRuns).
        MiterRuns(runs);
        shotRuns = runs;
        fortressBounds = BoundsOf(runs);

        Vector2 center = new Vector2(tilePos.x, tilePos.y);
        PickGate(runs, center);
        AlignPierToGate(center);

        // Na kterých věžích bude dělo (2–3, deterministicky podle pozice ostrova).
        int seed = Mathf.Abs(unchecked(tilePos.x * 73856093 ^ tilePos.y * 19349663));
        int cannonCount = Mathf.Min(towerCorners.Count, 2 + seed % 2);
        var order = new List<int>();
        for (int i = 0; i < towerCorners.Count; i++) order.Add(i);
        order.Sort((x, y) => TowerPickKey(seed, x).CompareTo(TowerPickKey(seed, y)));
        cannonTowerIdx.Clear();
        for (int k = 0; k < cannonCount; k++) cannonTowerIdx.Add(order[k]);

        BuildTowers(towerCorners, center, towersName);

        if (wallsAlreadyBuilt) return towerCorners;

        // ZÁMĚRNĚ NEparentěno pod marker (transform) — na rozdíl od obelisku.
        // Hradby jsou fyzická stavba na ostrově, ne "UI" příběhového markeru:
        // GiveNextMegaIsland() marker znovu zničí, jakmile hráč dočte vzkaz
        // a příběh ho posune na další ostrov (viz TryReadMessage), ale hradby
        // mají zůstat stát, i když se tam hráč vrátí později. Kdyby byly dítě
        // markeru, zmizely by spolu s ním hned po přečtení vzkazu.
        var wallsGo = new GameObject(wallsName);
        wallsGo.transform.position = new Vector3(tilePos.x, 0f, tilePos.y);

        for (int i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            float len = run.EffLength;
            if (len < WALL_MIN_SPAN) continue; // úsek pohltily sousední rohy, nic k postavení

            Vector2 start = run.EffStart;
            float yRot = Mathf.Atan2(run.Dir.y, run.Dir.x) * Mathf.Rad2Deg;
            // Každý druhý úsek o kapku výš: kde se tlusté zdi u drobných schodů na
            // pobřeží ještě trochu překrývají, se tak horní plochy neperou (z-fighting).
            float yLift = (i % 2) * WALL_LIFT;

            if (i == gateRunIndex)
            {
                // Brána je 2 j široká a stojí přesně proti molu; po stranách na ni navazují díly hradby.
                Vector2 gCenter = start + run.Dir * gateAlong;
                BuildPirateKitPart("castle-gate", new Vector3(gCenter.x, yLift, gCenter.y), yRot, wallsGo.transform, 1f);
                BuildWallSpan(start, run.Dir, 0f, gateAlong - 1f, yRot, yLift, wallsGo.transform);
                BuildWallSpan(start, run.Dir, gateAlong + 1f, len, yRot, yLift, wallsGo.transform);
            }
            else
            {
                BuildWallSpan(start, run.Dir, 0f, len, yRot, yLift, wallsGo.transform);
            }
        }

        // Hradby jsou statické (nikdy se nehýbou) a sdílejí jeden materiál (viz
        // BuildPirateKitPart) — ideální kandidát na static batching. Bez něj je
        // to 100+ samostatných draw callů (věže + brána + desítky dílů zdi) i
        // pro shadow pass; Combine je slije do pár dávek (fáze 6 výkon). Volané
        // rovnou tady (stejný snímek, hned po Instantiate všech dílů) v Play
        // ověřeně NEFUNGOVALO (isPartOfStaticBatch zůstalo false u všeho) —
        // stejné volání o snímek později už spolehlivě zabralo, proto korutina.
        // Věže v tom NEJSOU (stojí zvlášť) — rozpadají se, takže se musí hýbat.
        StartCoroutine(CombineWallsNextFrame(wallsGo));

        return towerCorners;
    }

    // Brána: na úsek hradby, který je nejblíž molu (střed obou dlaždic mola u břehu),
    // a na tom úseku přesně proti molu. Když molo chybí (starý save), brána jde na úsek
    // ve směru ke světovému počátku (tam se molo staví — GridManager.PlaceMegaIsland).
    private void PickGate(List<WallRun> runs, Vector2 center)
    {
        Vector2 target;
        Vector2? pier = FindPierBase();
        if (pier != null) target = pier.Value;
        else
        {
            Vector2 toWorld = -center;
            if (toWorld.sqrMagnitude < 1f) toWorld = Vector2.down;
            target = center + toWorld.normalized * 30f;
        }

        gateRunIndex = -1;
        float best = float.MaxValue;
        for (int i = 0; i < runs.Count; i++)
        {
            float len = runs[i].EffLength;
            if (len < 2.4f) continue; // brána je 2 j široká, potřebuje aspoň tolik místa
            float along = Mathf.Clamp(Vector2.Dot(target - runs[i].EffStart, runs[i].Dir), 1.1f, len - 1.1f);
            float dist  = (target - (runs[i].EffStart + runs[i].Dir * along)).magnitude;
            if (dist < best) { best = dist; gateRunIndex = i; gateAlong = along; }
        }
        if (gateRunIndex < 0) return;

        // Střed brány "přicvakni" přesně mezi dvě dlaždice (souřadnice x,5) — molo je 2 dlaždice
        // široké, takže pak sedí přesně proti průchodu (viz AlignPierToGate).
        var run = runs[gateRunIndex];
        Vector2 g = run.EffStart + run.Dir * gateAlong;
        float axis    = Mathf.Abs(run.Dir.x) > 0.5f ? g.x : g.y;           // souřadnice podél zdi
        float snapped = Mathf.Floor(axis) + 0.5f;
        float sign    = Mathf.Abs(run.Dir.x) > 0.5f ? run.Dir.x : run.Dir.y; // ±1 podle směru úseku
        gateAlong = Mathf.Clamp(gateAlong + (snapped - axis) * sign, 1.0f, run.EffLength - 1.0f);
    }

    // Molo vede přesně do brány. Když ho GridManager kdysi postavil jinam (schodovité
    // pobřeží — úsek zdi přímo proti němu byl na bránu moc krátký), staré molo se
    // změní zpátky na vodu a nové (2 × 4 dlaždice) se postaví od brány ven do moře.
    // Deterministické — po načtení hry vyjde stejně a už se nic nemění.
    private void AlignPierToGate(Vector2 center)
    {
        if (gateRunIndex < 0 || shotRuns == null) return;
        var run = shotRuns[gateRunIndex];
        Vector2 g = run.EffStart + run.Dir * gateAlong;

        // Směr ven z pevnosti = kolmice na zeď, která míří od středu ostrova.
        Vector2 outward = new Vector2(run.Dir.y, -run.Dir.x);
        if (Vector2.Dot(outward, g - center) < 0f) outward = -outward;

        var wanted = new HashSet<Vector2Int>();
        for (int side = -1; side <= 1; side += 2)
        {
            Vector2 lane = g + run.Dir * (0.5f * side); // dvě řady mola vedle sebe
            for (int k = 1; k <= 4; k++)
            {
                Vector2 p = lane + outward * k;
                var t = new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
                if (gridManager.GetTileType(t.x, t.y) == TileType.MegaIsland) continue; // pevninu nepřepisuj
                wanted.Add(t);
            }
        }
        if (wanted.Count == 0) return;

        var existing = new List<Vector2Int>();
        for (int x = tilePos.x - 22; x <= tilePos.x + 22; x++)
            for (int y = tilePos.y - 22; y <= tilePos.y + 22; y++)
                if (gridManager.GetTileType(x, y) == TileType.Pier) existing.Add(new Vector2Int(x, y));

        bool same = existing.Count == wanted.Count;
        foreach (var e in existing) if (!wanted.Contains(e)) { same = false; break; }
        if (same) return; // molo už vede do brány

        foreach (var e in existing)
            if (!wanted.Contains(e)) gridManager.SetTileType(e.x, e.y, TileType.Water);
        foreach (var w in wanted)
            gridManager.SetOrCreateTile(w.x, w.y, TileType.Pier);
        gridManager.Save();
    }

    // Střed mola u břehu: průměr dlaždic mola, které sousedí s pevninou ostrova.
    private Vector2? FindPierBase()
    {
        Vector2 sum = Vector2.zero;
        int n = 0;
        for (int x = tilePos.x - 22; x <= tilePos.x + 22; x++)
            for (int y = tilePos.y - 22; y <= tilePos.y + 22; y++)
            {
                if (gridManager.GetTileType(x, y) != TileType.Pier) continue;
                bool touchesLand = gridManager.GetTileType(x + 1, y) == TileType.MegaIsland
                                || gridManager.GetTileType(x - 1, y) == TileType.MegaIsland
                                || gridManager.GetTileType(x, y + 1) == TileType.MegaIsland
                                || gridManager.GetTileType(x, y - 1) == TileType.MegaIsland;
                if (!touchesLand) continue;
                sum += new Vector2(x, y);
                n++;
            }
        return n > 0 ? sum / n : (Vector2?)null;
    }

    // Obdélník kolem všech úseků hradby (+ rezerva) — rychlý test "jsem u pevnosti?".
    private static Rect BoundsOf(List<WallRun> runs)
    {
        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var r in runs)
        {
            Vector2 a = r.EffStart, b = r.EffStart + r.Dir * r.EffLength;
            minX = Mathf.Min(minX, Mathf.Min(a.x, b.x)); maxX = Mathf.Max(maxX, Mathf.Max(a.x, b.x));
            minY = Mathf.Min(minY, Mathf.Min(a.y, b.y)); maxY = Mathf.Max(maxY, Mathf.Max(a.y, b.y));
        }
        const float M = 3f;
        return Rect.MinMaxRect(minX - M, minY - M, maxX + M, maxY + M);
    }

    // Strážní věže v rozích. Stojí zvlášť (ne pod hradbami), protože se dají rozbít.
    // Zbořené (GameData.fortressTowersDown) se postaví jen jako sutiny.
    private void BuildTowers(List<Vector2> corners, Vector2 center, string towersName)
    {
        towers.Clear();
        var existing = GameObject.Find(towersName);
        if (existing != null)
        {
            // Věže v téhle scéně už stojí (další "Nová hra" bez restartu) — jen si je najdi.
            foreach (var t in existing.GetComponentsInChildren<FortressTower>()) towers.Add(t);
            towers.Sort((a, b) => a.Index.CompareTo(b.Index));
            return;
        }

        var parent = new GameObject(towersName);
        parent.transform.position = new Vector3(tilePos.x, 0f, tilePos.y);
        int downMask = gridManager.gameData.fortressTowersDown;

        for (int i = 0; i < corners.Count; i++)
        {
            Vector2 c = corners[i];
            Vector2 inward = center - c; // vchod věže dovnitř pevnosti
            float yRot = Mathf.Atan2(inward.x, inward.y) * Mathf.Rad2Deg;
            bool ruined = (downMask & (1 << i)) != 0;
            bool hasCannon = cannonTowerIdx.Contains(i);
            // Věž s dělem má nahoře cimbuří (plošina pro dělo), ostatní strážní věžičku se stříškou.
            towers.Add(FortressTower.Spawn(i, new Vector3(c.x, 0f, c.y), yRot, !hasCannon, ruined, parent.transform, GetWallMaterial()));
        }
    }

    // Sdílený materiál Kenney dílů (atlas colormap.png) — hradby, věže i vybavení nádvoří.
    private static Material GetWallMaterial()
    {
        if (!wallColormapTried) { wallColormapTried = true; wallColormap = Resources.Load<Texture2D>("PirateKit/colormap"); }
        if (wallMaterial == null && wallColormap != null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            wallMaterial = new Material(sh);
            if (wallMaterial.HasProperty("_BaseMap")) wallMaterial.SetTexture("_BaseMap", wallColormap);
            if (wallMaterial.HasProperty("_MainTex")) wallMaterial.SetTexture("_MainTex", wallColormap);
        }
        return wallMaterial;
    }

    // ── Nádvoří pevnosti (vnitřek hradeb) ───────────────────────────────────
    // Ať uvnitř není jen trezor: chatrče s doškovou střechou, hromady beden a sudů,
    // pirátské vlajky, palma a polní dělo. Od brány k náměstí vede volná cesta.
    // Všechno deterministicky podle tvaru ostrova (stejný ostrov = stejné nádvoří).
    // Chatrče a bedny blokují chůzi (yardBlocked) — volné zůstane náměstí uprostřed
    // (cedule, trezor, stránka deníku) a chodník od brány.
    private void BuildYard()
    {
        yardBlocked.Clear();
        if (gateRunIndex < 0 || shotRuns == null) return;

        Vector2 center = new Vector2(tilePos.x, tilePos.y);
        var gateRun = shotRuns[gateRunIndex];
        Vector2 gate = gateRun.EffStart + gateRun.Dir * gateAlong;

        // Políčka, která musí zůstat volná: náměstí, trezor/stránka deníku, chodník.
        var keepFree = new HashSet<Vector2Int>();
        foreach (var t in FindGuardTiles(3))
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++) keepFree.Add(new Vector2Int(t.x + dx, t.y + dy));

        var cand = new List<Vector2Int>();
        for (int x = tilePos.x - 16; x <= tilePos.x + 16; x++)
            for (int y = tilePos.y - 16; y <= tilePos.y + 16; y++)
            {
                var t = new Vector2Int(x, y);
                if (Mathf.Max(Mathf.Abs(x - tilePos.x), Mathf.Abs(y - tilePos.y)) < 3) continue; // náměstí
                if (keepFree.Contains(t)) continue;
                if (!IsInnerTile(x, y, 2)) continue;                                              // ne u zdi
                if (DistToSegment(new Vector2(x, y), gate, center) < 1.8f) continue;             // chodník
                if (NearTower(new Vector2(x, y), 1.8f)) continue;
                cand.Add(t);
            }
        cand.Sort((a, b) => YardKey(a).CompareTo(YardKey(b)));

        string yardName = $"Yard_{tilePos.x}_{tilePos.y}";
        bool alreadyBuilt = GameObject.Find(yardName) != null;
        GameObject yard = alreadyBuilt ? null : new GameObject(yardName);

        // Co kam: 3 chatrče, 2 vlajky, 1 palma, 1 polní dělo, pak bedny a sudy.
        string[] plan = { "hut", "hut", "flag", "crates", "hut", "barrels", "cannon", "flag", "crates", "palm", "barrels", "crates" };
        var picked = new List<Vector2Int>();
        int next = 0;
        foreach (var t in cand)
        {
            if (next >= plan.Length) break;
            string what = plan[next];
            int spacing = what == "hut" ? 3 : 2;
            bool tooClose = false;
            foreach (var p in picked)
                if (Mathf.Max(Mathf.Abs(p.x - t.x), Mathf.Abs(p.y - t.y)) < spacing) { tooClose = true; break; }
            if (tooClose) continue;

            picked.Add(t);
            next++;
            if (what != "flag" && what != "palm") yardBlocked.Add(t); // přes chatrč a bedny se nechodí
            if (yard != null) BuildYardProp(what, t, center, yard.transform);
        }
        // (Prkenný chodník od brány jsem zkoušel — terén ostrova uvnitř stoupá, prkna se
        //  zanořovala do trávy. Cesta od brány k náměstí proto zůstává jen volná.)
    }

    // Jedna rekvizita nádvoří na políčku `t` (natočená ke středu, ať "kouká" na náměstí).
    private void BuildYardProp(string what, Vector2Int t, Vector2 center, Transform parent)
    {
        Vector2 toCenter = center - new Vector2(t.x, t.y);
        float yRot = Mathf.Atan2(toCenter.x, toCenter.y) * Mathf.Rad2Deg;
        Vector3 pos = new Vector3(t.x, 0f, t.y);
        int h = YardKey(t) & 0xffff; // drobná "náhoda" (natočení, posun) — pořád deterministická

        switch (what)
        {
            case "hut":
                PlaceKit("structure-roof", pos, yRot, 0.55f, parent);
                PlaceKit("barrel", pos + new Vector3(0.75f, 0f, -0.55f), h % 360, 0.35f, parent);
                break;
            case "flag":
                PlaceKit("flag-pirate-high", pos, yRot + 90f, 0.6f, parent);
                break;
            case "crates":
                PlaceKit("crate", pos + new Vector3(-0.2f, 0f, 0.1f), h % 90, 0.45f, parent);
                PlaceKit("crate-bottles", pos + new Vector3(0.35f, 0f, -0.25f), (h / 7) % 90, 0.45f, parent);
                PlaceKit("crate", pos + new Vector3(-0.15f, 0.35f, 0.05f), (h / 13) % 90, 0.42f, parent); // druhá bedna navrch
                break;
            case "barrels":
                PlaceKit("barrel", pos + new Vector3(-0.25f, 0f, 0f), 0f, 0.4f, parent);
                PlaceKit("barrel", pos + new Vector3(0.3f, 0f, 0.2f), 40f, 0.4f, parent);
                PlaceKit("barrel", pos + new Vector3(0.1f, 0f, -0.35f), 80f, 0.4f, parent);
                break;
            case "cannon":
                PlaceKit("cannon-mobile", pos, yRot + 180f, 0.5f, parent); // míří ven, k hradbám
                break;
            case "palm":
                PlaceKit("palm-detailed-straight", pos, h % 360, 0.45f, parent);
                break;
        }
    }

    // Postaví jeden Kenney díl (sdílený materiál, bez colliderů).
    private static void PlaceKit(string model, Vector3 pos, float yRot, float scale, Transform parent)
    {
        var prefab = Resources.Load<GameObject>("PirateKit/" + model);
        if (prefab == null) return;
        var go = Instantiate(prefab, pos, Quaternion.Euler(0f, yRot, 0f), parent);
        go.name = model;
        go.transform.localScale = Vector3.one * scale;
        Material mat = GetWallMaterial();
        foreach (var mr in go.GetComponentsInChildren<MeshRenderer>())
        {
            if (mat != null) mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        foreach (var col in go.GetComponentsInChildren<Collider>()) Destroy(col);
    }

    // Políčko uvnitř hradeb: kolem něj (do vzdálenosti `margin`) je samá pevnina ostrova.
    private bool IsInnerTile(int x, int y, int margin)
    {
        for (int dx = -margin; dx <= margin; dx++)
            for (int dy = -margin; dy <= margin; dy++)
                if (gridManager.GetTileType(x + dx, y + dy) != TileType.MegaIsland) return false;
        return true;
    }

    private bool NearTower(Vector2 p, float range)
    {
        foreach (var t in towers)
            if (t != null && (new Vector2(t.transform.position.x, t.transform.position.z) - p).sqrMagnitude < range * range)
                return true;
        return false;
    }

    private static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        if (len2 < 0.0001f) return (p - a).magnitude;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        return (p - (a + ab * t)).magnitude;
    }

    private static int YardKey(Vector2Int t) => unchecked(t.x * 83492791 ^ t.y * 297121507) & 0x7fffffff;

    private IEnumerator CombineWallsNextFrame(GameObject wallsGo)
    {
        yield return new WaitForEndOfFrame();
        if (wallsGo != null) StaticBatchingUtility.Combine(wallsGo);
    }

    // Postaví díly hradby v rozsahu [from, to] (vzdálenost od začátku úseku).
    // Počet dílů = nejbližší celé číslo k délce, díly se o pár procent natáhnou
    // podél zdi, aby rozsah vyplnily přesně — bez mezer a bez překryvů.
    // (Kenney textura je paleta plných barev, natažení jí nevadí.)
    private static void BuildWallSpan(Vector2 start, Vector2 dir, float from, float to,
                                      float yRot, float yLift, Transform parent)
    {
        float span = to - from;
        if (span < WALL_MIN_SPAN) return;

        int   count = Mathf.Max(1, Mathf.RoundToInt(span));
        float width = span / count; // šířka jednoho dílu, ≈ 1.0

        for (int s = 0; s < count; s++)
        {
            Vector2 pos = start + dir * (from + width * (s + 0.5f));
            BuildPirateKitPart("castle-wall", new Vector3(pos.x, yLift, pos.y), yRot, parent, width);
        }
    }

    // Půl tloušťky hradby: Kenney díl je při scale 1 hluboký 2.8 j (změřeno),
    // při WALL_SCALE 0.5 tedy 1.4 j → 0.7 j na každou stranu od osy pobřeží.
    private const float WALL_HALF_DEPTH = 0.7f;
    private const float WALL_MIN_SPAN   = 0.3f;   // kratší kousky zdi se nestaví
    private const float WALL_LIFT       = 0.004f; // svislý posun proti z-fightingu

    // Vyřeší ROHY hradby. Zeď je tlustá, a jak končí přesně v rohu, na vnější
    // straně chybí čtvrtka tloušťka × tloušťka (mezera) a uvnitř se dva díly
    // překrývají. Řešení "mitra": v každém rohu se JEDEN úsek protáhne přes
    // roh o půl tloušťky a druhý se o půl tloušťky zkrátí — dohromady dají
    // čisté "L" bez díry i bez překryvu. Protahuje se vždy KRATŠÍ úsek, ať u
    // drobných schodů pobřeží nevznikají záporné délky.
    private static void MiterRuns(List<WallRun> runs)
    {
        int n = runs.Count;
        for (int i = 0; i < n; i++)
        {
            WallRun cur = runs[i];
            WallRun nxt = runs[(i + 1) % n];

            bool curThrough = cur.Length < nxt.Length
                              || (Mathf.Approximately(cur.Length, nxt.Length) && i % 2 == 0);

            cur.ExtEnd   = curThrough ?  WALL_HALF_DEPTH : -WALL_HALF_DEPTH;
            nxt.ExtStart = curThrough ? -WALL_HALF_DEPTH :  WALL_HALF_DEPTH;
        }
    }

    // Jeden rovný úsek hradby (víc po sobě jdoucích hran hranice se stejným směrem).
    private class WallRun
    {
        public Vector2 Start;
        public Vector2 Dir;    // jednotkový směr, vždy osově zarovnaný (±1,0) nebo (0,±1)
        public float   Length; // v dlaždicích (délka pobřeží, bez úprav rohů)
        public Vector2 Midpoint => Start + Dir * (Length * 0.5f);

        // Úprava konců v rozích (viz MiterRuns): + = protažení přes roh, − = zkrácení.
        public float   ExtStart, ExtEnd;
        public Vector2 EffStart    => Start - Dir * ExtStart;
        public float   EffLength   => Length + ExtStart + ExtEnd;
        public Vector2 EffMidpoint => EffStart + Dir * (EffLength * 0.5f);
    }

    // Obejde hranici pevnina/voda ostrova po jednotkových hranách dlaždic
    // (klasický "marching squares" postup) a vrátí ji jako uzavřenou smyčku
    // rohových bodů v pořadí, jak jdou po obvodu. Pier dlaždice (molo) se
    // počítají jako pevnina, ať hradba obejde i výběžek mola místo aby ho
    // "prokousla" — bránu pak najde BuildWalls podle směru k molu.
    private List<Vector2> TraceIslandBoundary()
    {
        // "Pevná" dlaždice = pevnina ostrova. Molo (Pier) se nepočítá, hradba ho
        // obchází zvenku — jen u starých savů je základna mola přepsané pobřežní
        // políčko obklopené pevninou aspoň ze 3 stran, to za pevnou bereme.
        bool IsSolid(int x, int y)
        {
            TileType t = gridManager.GetTileType(x, y);
            if (t == TileType.MegaIsland) return true;
            if (t != TileType.Pier) return false;
            int around = 0;
            if (gridManager.GetTileType(x + 1, y) == TileType.MegaIsland) around++;
            if (gridManager.GetTileType(x - 1, y) == TileType.MegaIsland) around++;
            if (gridManager.GetTileType(x, y + 1) == TileType.MegaIsland) around++;
            if (gridManager.GetTileType(x, y - 1) == TileType.MegaIsland) around++;
            return around >= 3;
        }

        // Hradba stojí o JEDNU dlaždici uvnitř pobřeží: dlaždice počítáme za "zdivo" jen
        // tehdy, když jsou pevné i všichni čtyři sousedé. Tlustá zeď (1,4 j) tak celá
        // stojí na pevnině a nepřesahuje nad vodu.
        bool IsLand(int x, int y)
            => IsSolid(x, y) && IsSolid(x + 1, y) && IsSolid(x - 1, y) && IsSolid(x, y + 1) && IsSolid(x, y - 1);

        // Směrovaná jednotková hrana pro každou "odkrytou" stěnu pevninové
        // dlaždice, orientovaná tak, že sousedící hrany od sousedních dlaždic
        // na sebe automaticky navazují do jedné souvislé smyčky (roh → roh).
        // Souřadnice rohů jsou ×2 (celá čísla místo půlek), ať jde použít Dictionary.
        var next = new Dictionary<(int, int), (int, int)>();
        for (int x = tilePos.x - 17; x <= tilePos.x + 17; x++)
            for (int y = tilePos.y - 17; y <= tilePos.y + 17; y++)
            {
                if (!IsLand(x, y)) continue;
                if (!IsLand(x + 1, y)) AddBoundaryEdge(next, x, y,  1,  1,  1, -1); // východní stěna odkrytá
                if (!IsLand(x, y - 1)) AddBoundaryEdge(next, x, y,  1, -1, -1, -1); // jižní stěna odkrytá
                if (!IsLand(x - 1, y)) AddBoundaryEdge(next, x, y, -1, -1, -1,  1); // západní stěna odkrytá
                if (!IsLand(x, y + 1)) AddBoundaryEdge(next, x, y, -1,  1,  1,  1); // severní stěna odkrytá
            }
        if (next.Count == 0) return null;

        // Nejpravější roh (při shodě nejvyšší Y) je vždy na VNĚJŠÍ smyčce —
        // bezpečný start, nemůže patřit vnitřní díře (ostrov žádné nemá, ale
        // pro jistotu robustní i kdyby náhodou).
        (int, int) start = default;
        int bestX = int.MinValue, bestY = int.MinValue;
        foreach (var k in next.Keys)
            if (k.Item1 > bestX || (k.Item1 == bestX && k.Item2 > bestY)) { bestX = k.Item1; bestY = k.Item2; start = k; }

        var loop = new List<Vector2>();
        var cur = start;
        int guard = 0;
        do
        {
            // +0,5: terén ostrova pokrývá dlaždici [x, x+1), zatímco logická dlaždice je
            // vycentrovaná na x — bez posunu by hradba seděla o půl dlaždice vedle pobřeží.
            loop.Add(new Vector2(cur.Item1 * 0.5f + 0.5f, cur.Item2 * 0.5f + 0.5f));
            if (!next.TryGetValue(cur, out cur)) return null; // nemělo by nastat u uzavřené smyčky
            guard++;
        }
        while (cur != start && guard < 20000);

        return loop.Count >= 4 ? loop : null;
    }

    private static void AddBoundaryEdge(Dictionary<(int, int), (int, int)> next, int x, int y, int ax, int ay, int bx, int by)
        => next[(x * 2 + ax, y * 2 + ay)] = (x * 2 + bx, y * 2 + by);

    // Poslepuje po sobě jdoucí hrany smyčky se stejným směrem do delších
    // rovných úseků (rovné pobřeží = jeden dlouhý úsek hradby místo desítek
    // jednotlivých dlaždicových dílů).
    private List<WallRun> MergeIntoStraightRuns(List<Vector2> loop)
    {
        var runs = new List<WallRun>();
        int n = loop.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 a = loop[i], b = loop[(i + 1) % n];
            Vector2 d = (b - a).normalized;
            if (runs.Count > 0 && Vector2.Dot(runs[runs.Count - 1].Dir, d) > 0.99f)
                runs[runs.Count - 1].Length += (b - a).magnitude;
            else
                runs.Add(new WallRun { Start = a, Dir = d, Length = (b - a).magnitude });
        }
        // Smyčka se mohla "protočit" přes index 0 uprostřed rovného úseku —
        // slij první a poslední kus, ať se na startu nezdvojí díl hradby.
        if (runs.Count > 1 && Vector2.Dot(runs[0].Dir, runs[runs.Count - 1].Dir) > 0.99f)
        {
            runs[runs.Count - 1].Length += runs[0].Length;
            runs.RemoveAt(0);
        }
        return runs;
    }

    // Vybere až `count` nejvýraznějších rohů (kde se hradba nejvíc láme) na
    // věže, rozmístěných po obvodu — váha rohu = kratší z obou sousedních
    // úseků, ať věž nevyroste na každém malém schodu kruhového pobřeží.
    private List<Vector2> PickTowerCorners(List<WallRun> runs, int count)
    {
        int n = runs.Count;
        var candidates = new List<(Vector2 point, float weight, int index)>();
        for (int i = 0; i < n; i++)
        {
            var cur = runs[i];
            var nxt = runs[(i + 1) % n];
            Vector2 corner = cur.Start + cur.Dir * cur.Length;
            candidates.Add((corner, Mathf.Min(cur.Length, nxt.Length), i));
        }
        candidates.Sort((x, y) => y.weight.CompareTo(x.weight));

        var picked = new List<Vector2>();
        var pickedIdx = new List<int>();
        foreach (var c in candidates)
        {
            bool tooClose = false;
            foreach (int pi in pickedIdx)
            {
                int d = Mathf.Abs(c.index - pi);
                d = Mathf.Min(d, n - d); // vzdálenost po obvodu (kratší směr)
                if (d < Mathf.Max(2, n / 8)) { tooClose = true; break; }
            }
            if (tooClose) continue;
            picked.Add(c.point);
            pickedIdx.Add(c.index);
            if (picked.Count >= count) break;
        }
        return picked;
    }

    // Načte a postaví jeden díl z Kenney Pirate Kitu (Assets/Resources/PirateKit),
    // obarvený stejnou atlasovou texturou jako ostatní Kenney modely v projektu
    // (HostileIslandCannon.TryBuildKenneyCannon). Chybějící model = potichu nic
    // (hradby jsou jen dekorace, nesmí kvůli chybějícímu assetu spadnout hra).
    private static Texture2D wallColormap;
    private static bool      wallColormapTried;

    // lengthStretch = natažení dílu podél zdi (lokální osa X modelu); 1 = původní šířka.
    private static Material wallMaterial; // jeden sdílený materiál pro všechny díly (méně objektů, jde dávkovat)

    private static void BuildPirateKitPart(string resourceName, Vector3 worldPos, float yRotationDeg, Transform parent, float lengthStretch)
    {
        var prefab = Resources.Load<GameObject>("PirateKit/" + resourceName);
        if (prefab == null) return;

        var go = Instantiate(prefab, worldPos, Quaternion.Euler(0f, yRotationDeg, 0f), parent);
        go.name = resourceName;
        go.transform.localScale = new Vector3(WALL_SCALE * lengthStretch, WALL_SCALE, WALL_SCALE);

        if (!wallColormapTried) { wallColormapTried = true; wallColormap = Resources.Load<Texture2D>("PirateKit/colormap"); }
        if (wallColormap == null) return;

        if (wallMaterial == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            wallMaterial = new Material(sh);
            if (wallMaterial.HasProperty("_BaseMap")) wallMaterial.SetTexture("_BaseMap", wallColormap);
            if (wallMaterial.HasProperty("_MainTex")) wallMaterial.SetTexture("_MainTex", wallColormap);
        }
        foreach (var mr in go.GetComponentsInChildren<MeshRenderer>())
            mr.sharedMaterial = wallMaterial;
    }

    // ── Interakce (hák z PlayerController.TryInteractAdjacentBuilding) ──────
    // Vrací true, když hráč stojí vedle obelisku (nebo živého strážce) a stisk
    // E patří tomuhle ostrovu. V dalších krocích přibudou další interaktivní
    // body (tabulky, trezor, vzkaz) — stejnou metodou, viz plán §2.
    public bool TryInteract(int x, int y, int playerIndex)
    {
        // Nejdřív živí strážci — E vedle nich = úder (viz LandGuard.PLAYER_HIT).
        foreach (var guard in myGuards)
            if (guard != null && guard.IsAt(x, y))
            {
                guard.TakeHit(LandGuard.PLAYER_HIT);
                SoundManager.PlayHit();
                return true;
            }

        // Trezor (Ostrov pirátů) — dokud není vyřešený, E otevře puzzle; po vyřešení
        // E přečte vzkaz uvnitř (jednou — pak posune příběh na další ostrov).
        if (vault != null && vault.IsAt(x, y))
            return vault.Solved ? TryReadMessage(playerIndex) : vault.Open(playerIndex);

        // Podpalubí vraku (ostrov 2) — E přečte vzkaz, stejně jako trezor.
        if (holdBuilt && x == holdTile.x && y == holdTile.y)
            return TryReadMessage(playerIndex);

        // Stránka bratrova deníku.
        if (pageIndex >= 0 && x == pageTile.x && y == pageTile.y) { CollectDiaryPage(); return true; }

        if (x != tilePos.x || y != tilePos.y) return false;
        if (CombatDirector.Instance != null) CombatDirector.Instance.Toast(Loc.T(signTextCs, signTextEn));
        return true;
    }

    /// <summary>Co udělá E na daném políčku — bez vedlejších účinků, jen na
    /// vypsání nápovědy (viz PlayerController.GetContextHint). Vrací null, když
    /// tam není nic k udělání.</summary>
    public string GetHint(int x, int y)
    {
        foreach (var guard in myGuards)
            if (guard != null && guard.IsAt(x, y)) return Loc.T("zaútočit na stráž", "attack the guard");

        if (vault != null && vault.IsAt(x, y))
        {
            if (!vault.Solved) return Loc.T("otevřít trezor", "open the vault");
            return gridManager != null && gridManager.gameData.megaTask < 3 ? Loc.T("přečíst vzkaz", "read the message") : null;
        }

        if (holdBuilt && x == holdTile.x && y == holdTile.y)
            return gridManager != null && gridManager.gameData.megaTask < 3 ? Loc.T("prohledat podpalubí", "search the hold") : null;

        if (pageIndex >= 0 && x == pageTile.x && y == pageTile.y) return Loc.T("vzít svitek", "take the scroll");

        if (x == tilePos.x && y == tilePos.y)
        {
            bool fortress = gridManager != null && gridManager.gameData.megaIndex == 0; // bez obelisku, jen cedule
            return fortress ? Loc.T("přečíst tabuli", "read the sign") : Loc.T("prozkoumat obelisk", "examine the obelisk");
        }
        return null;
    }

    /// <summary>Zavolá LandGuard při své smrti — jen okamžitá hláška. Skutečný postup
    /// (megaTask) řeší Update() nahoře, který sečte, co všechno ještě žije.</summary>
    public void OnGuardDestroyed(LandGuard g)
    {
        var cd = CombatDirector.Instance;
        if (cd == null) return;

        // Stráž je taky pirát → stejná šance na kořist jako u pirátské lodi.
        // Kořist se připíše dřív než mince, protože RewardNearestPlayer hru uloží.
        string lootText = cd.RollPirateLoot(cd.PlayerStateOf(cd.RewardTarget()), LootItems.DROP_CHANCE);
        cd.RewardNearestPlayer(EconomyConfig.LandGuardReward);
        int r = EconomyConfig.LandGuardReward;
        cd.Toast(Loc.T($"Stráž poražena.  +{r} {Loc.CoinsWord(r)}", $"Guard defeated.  +{r} {Loc.CoinsWord(r)}") + lootText);
    }

    // Vzkazy bratra na jednotlivých ostrovech (Krok 4 = Ostrov pirátů, Krok 6 =
    // ostrov 2) — čte se podle megaIndex. Ostrov 3 vzkaz nedává (tam už je
    // bratr osobně, viz plán §1).
    private static string BrotherMessage(int megaIndex)
    {
        switch (megaIndex)
        {
            case 0:
                return Loc.T(
                    "Vzkaz: „Přišel jsi pozdě. Mám to já — mám to od samého začátku. "
                  + "Jestli opravdu chcete, co patří rodině, přijeďte si pro to sami.“",
                    "Message: “You’re too late. I have it — I’ve had it from the very beginning. "
                  + "If you truly want what belongs to the family, come and get it yourselves.”");
            case 1:
                return Loc.T(
                    "Vzkaz: „Vylezl jsem z vody a loď s vámi byla pryč. Čekal jsem. Nikdo nepřijel.“",
                    "Message: “I climbed out of the water and the ship, with all of you aboard, was gone. I waited. Nobody came.”");
            default:
                return Loc.T("Vzkaz beze slov.", "A message without words.");
        }
    }

    // Přečtení vzkazu (v trezoru na ostrově 1 / v podpalubí na ostrově 2) —
    // jen jednou, pak posune příběh na další mega ostrov.
    private bool TryReadMessage(int playerIndex)
    {
        if (gridManager == null) return true;
        var d = gridManager.gameData;

        if (d.megaTask >= 3)
        {
            if (CombatDirector.Instance != null) CombatDirector.Instance.Toast(Loc.T("Vzkaz už jsi přečetl.", "You have already read the message."));
            return true;
        }
        if (d.megaTask != 2) return true; // pojistka — nemělo by nastat (vault.Solved/podpalubí už megaTask=2 zajišťuje)

        d.megaTask = 3;
        gridManager.Save();

        if (d.megaIndex == 0)
        {
            // Pirátský ostrov: dopis přes celou obrazovku s hádankou na souřadnice dalšího
            // ostrova. Ostrov se umístí (souřadnice se tím zjistí), ale waypoint se NEnastaví —
            // hráč si X a Y z hádanky spočítá a najde je sám na mapě.
            gridManager.GiveNextMegaIsland(false); // zničí tenhle marker a postaví další ostrov
            d.hasLetter = true;
            d.letterX   = d.storyIslandX;
            d.letterY   = d.storyIslandY;
            gridManager.Save();
            LetterScreen.Show(playerIndex, d.letterX, d.letterY);
            return true;
        }

        string message = BrotherMessage(d.megaIndex);
        if (CombatDirector.Instance != null) CombatDirector.Instance.Toast(message, 7f);

        gridManager.GiveNextMegaIsland(); // umístí další ostrov, nastaví waypoint, zničí tenhle marker
        return true;
    }

    // ── Vizuál obelisku (beze změny z předchozí verze) ───────────────────────
    private void BuildObelisk()
    {
        Material stone = MakeMat(new Color(0.42f, 0.40f, 0.38f));
        Material glow  = MakeMat(new Color(0.55f, 0.85f, 0.95f));
        if (glow.HasProperty("_EmissionColor"))
        {
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(0.35f, 0.7f, 0.95f) * 2f);
        }

        // Stupňovitý podstavec + obelisk.
        Box("Base",   new Vector3(0f, 0.15f, 0f), new Vector3(3.0f, 0.3f, 3.0f), stone);
        Box("Base2",  new Vector3(0f, 0.45f, 0f), new Vector3(2.2f, 0.3f, 2.2f), stone);
        Box("Shaft",  new Vector3(0f, 2.4f, 0f),  new Vector3(0.8f, 3.6f, 0.8f), stone);
        Box("Cap",    new Vector3(0f, 4.4f, 0f),  new Vector3(0.5f, 0.5f, 0.5f), glow);

        var lightGo = new GameObject("MarkerLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 4.4f, 0f);
        var l = lightGo.AddComponent<Light>();
        l.type      = LightType.Point;
        l.color     = new Color(0.55f, 0.85f, 0.95f);
        l.range     = 14f;
        l.intensity = 2.5f;
    }

    private void Box(string name, Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    private static Material MakeMat(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
        return m;
    }
}
