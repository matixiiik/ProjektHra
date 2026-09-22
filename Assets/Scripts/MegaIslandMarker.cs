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
    private string      signText; // co cedule říká — vrátí se jako toast při interakci

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

    void Awake()     { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        gridManager = FindFirstObjectByType<GridManager>();
        tilePos = new Vector2Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.z));

        BuildObelisk();

        int megaIndex = gridManager != null ? gridManager.gameData.megaIndex : 0;
        switch (megaIndex)
        {
            case 0: BuildFortress();       break;
            case 1: BuildWreckGraveyard(); break;
            default: BuildConfrontation(); break; // 2 (a jistota pro budoucí)
        }
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
            if (myCannons.Count == 0 && myGuards.Count == 0 && myGuardShip == null)
            {
                d.megaTask = 1;
                gridManager.Save();
                gridManager.NotifyWorldChanged();
                if (CombatDirector.Instance != null) CombatDirector.Instance.Toast("Obrana ostrova padla. Prohledej ho dál.");
                BuildVaultIfNeeded();
            }
        }
        else if (graveyardGuardSpawned && ghostShipSpawnedOk && myGhostShip == null)
        {
            d.megaTask = 1;
            gridManager.Save();
            gridManager.NotifyWorldChanged();
            if (CombatDirector.Instance != null) CombatDirector.Instance.Toast("Bludný Holanďan je poražen. Mělčina teď skrývá kousky mapy.");
            SpawnDigSpots();
        }
        else if (bossShipSpawned && bossShipSpawnedOk && myBossShip == null)
        {
            d.megaTask = 1;
            gridManager.Save();
            gridManager.NotifyWorldChanged();
            if (CombatDirector.Instance != null) CombatDirector.Instance.Toast("Bratrova loď je potopená. Vylodi se.");
            SpawnRival();
        }
    }

    // Postaví trezor, jakmile obrana padla (megaTask >= 1) — buď hned po
    // zničení poslední hlídky (výše v Update()), nebo při načtení savu, kde
    // už obrana dřív padla (viz BuildFortress, větev pro megaTask > 0).
    private void BuildVaultIfNeeded()
    {
        if (vaultBuilt || gridManager == null || gridManager.gameData.megaTask < 1) return;
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
        BuildSign("Ostrov pirátů — Pevnost staré posádky. Kolem obelisku hlídkuje ozbrojená posádka — trezor je někde uvnitř.",
                   new Color(0.5f, 0.24f, 0.18f));

        if (gridManager == null) return;

        // Hradby + věže jsou čistě kosmetické (nezávisí na megaTask) — stavíme
        // je vždy, i po reloadu, kdy je obrana už dávno poražená. Vrátí null,
        // když už (v týhle scéně) stojí — pak se dole ani děla nerozmisťují
        // znovu (byla by to stejná duplicita jako dřív u hradeb).
        var towerCorners = BuildWalls();

        // Obrana se staví, jen když ještě nebyla vyřízená (staré savy po
        // reloadu ať znovu nespawnou už poražené hlídky) — místo toho rovnou
        // postav trezor, ten na megaTask 0 nezávisí.
        if (gridManager.gameData.megaTask > 0) { BuildVaultIfNeeded(); return; }

        // 2–3 strážci, deterministicky podle pozice ostrova. Děla teď sedí NA
        // věžích (viz PlaceCannonsOnTowers), ne na okraji ostrova jako dřív.
        int seed = Mathf.Abs(unchecked(tilePos.x * 73856093 ^ tilePos.y * 19349663));
        int guardCount = 2 + (seed / 2) % 2;

        PlaceCannonsOnTowers(towerCorners, seed);

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

        defenseSpawned = true;
    }

    // Rozmístí 2–3 děla NA věže hradby (náhodně, ale deterministicky podle
    // pozice ostrova — žádný Random.value, ať stejný ostrov po reloadu vypadá
    // pořád stejně). Dělo sedí na plošině věže (CANNON_HEIGHT), ne až úplně
    // nahoře na střeše. HostileIslandCannon.IsNear počítá jen vodorovnou
    // vzdálenost, takže i takhle vysoko posazené dělo jde normálně trefit
    // koulí letící u hladiny (nebo obloukem — viz CannonBall/PlayerController).
    private const float CANNON_TOWER_HEIGHT = 2.4f;

    private void PlaceCannonsOnTowers(List<Vector2> towerPositions, int seed)
    {
        if (towerPositions == null || towerPositions.Count == 0) return;

        int count = Mathf.Min(towerPositions.Count, 2 + seed % 2);
        var order = new List<int>();
        for (int i = 0; i < towerPositions.Count; i++) order.Add(i);
        order.Sort((a, b) => TowerPickKey(seed, a).CompareTo(TowerPickKey(seed, b)));

        for (int k = 0; k < count; k++)
        {
            Vector2 pos = towerPositions[order[k]];
            var tile = new Vector2Int(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.y));
            var cannon = HostileIslandCannon.Spawn(tile, "mega");
            cannon.transform.position = new Vector3(pos.x, CANNON_TOWER_HEIGHT, pos.y);
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
        BuildSign("Mega ostrov 2 — Hřbitov lodí. V mělčině kolem hlídkuje Bludný Holanďan.",
                   new Color(0.32f, 0.36f, 0.42f));

        if (gridManager == null) return;
        var d = gridManager.gameData;

        // Obrana (Holanďan) se staví, jen když ještě nebyla poražená — po
        // reloadu rovnou obnov to, co z ostrova zbývá (mapa/podpalubí).
        if (d.megaTask > 0)
        {
            SpawnDigSpots();
            if (d.megaTask >= 2) BuildHoldIfNeeded();
            return;
        }

        Vector2Int? guardSpot = FindGuardWaterSpot();
        if (guardSpot != null)
        {
            var w = guardSpot.Value;
            myGhostShip = PirateShip.SpawnGhost(new Vector3(w.x, 0f, w.y), 1); // střední loď
            myGhostShip.SetGuard(new Vector3(w.x, 0f, w.y));
            myGhostShip.guardIslandKey = "mega";
            if (CombatDirector.Instance != null) CombatDirector.Instance.RegisterGuardShip(myGhostShip);
            ghostShipSpawnedOk = true;
        }
        else
        {
            // Nouzovka — i po širším hledání se nenašla voda na hlídku (nemělo
            // by nastat, ostrov vždycky obklopuje moře). Ať hráč nezůstane
            // zaseknutý: rovnou pusť dál na kopání.
            d.megaTask = 1;
            gridManager.Save();
            SpawnDigSpots();
        }
        graveyardGuardSpawned = true;
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
            if (CombatDirector.Instance != null) CombatDirector.Instance.Toast($"Kousek roztržené mapy! ({have}/3)");
            return;
        }

        gridManager.gameData.megaTask = 2;
        gridManager.Save();
        gridManager.NotifyWorldChanged();
        if (CombatDirector.Instance != null) CombatDirector.Instance.Toast("Mapa je kompletní! Kód vede do podpalubí vraku.", 4f);
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
        BuildSign("Mega ostrov 3 — Kde to začalo. Bratrova loď hlídá příjezd.",
                   new Color(0.52f, 0.44f, 0.16f));

        if (gridManager == null) return;
        var d = gridManager.gameData;

        // Po reloadu: loď se staví jen jednou, jinak rovnou obnov bratra.
        if (d.megaTask > 0) { SpawnRival(); return; }

        Vector2Int? bossSpot = FindGuardWaterSpot();
        if (bossSpot != null)
        {
            var w = bossSpot.Value;
            myBossShip = PirateShip.Spawn(new Vector3(w.x, 0f, w.y), 2); // velká loď
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
    private void BuildSign(string text, Color boardColor)
    {
        signText = text;

        Material wood  = MakeMat(new Color(0.35f, 0.24f, 0.14f));
        Material board = MakeMat(boardColor);

        Box("SignPost",  new Vector3(1.8f, 0.6f,  1.8f), new Vector3(0.12f, 1.2f, 0.12f), wood);
        Box("SignBoard", new Vector3(1.8f, 1.25f, 1.8f), new Vector3(1.1f,  0.6f, 0.08f),  board);
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
    // Čistě dekorativní, žádný vliv na hratelnost/kolize (modely nemají collider).
    private const float WALL_SCALE = 0.5f; // Kenney hradba/brána jsou při scale 1 přes 2/4 j — 0.5 = přesně 1.0/2.0 j (1/2 dlaždice)

    // Vrací pozice postavených věží (world XZ) — BuildFortress na některé
    // z nich (náhodně, deterministicky) posadí dělo, viz PlaceCannonsOnTowers.
    private List<Vector2> BuildWalls()
    {
        // Pojistka proti duplicitám: hradby NEJSOU potomkem markeru (viz níže),
        // takže je GiveNextMegaIsland nezničí spolu s ním — ale to znamená, že
        // opakované volání BuildFortress pro STEJNÝ ostrov (např. po "Nová
        // hra"/"Pokračovat" bez restartu Unity) by je postavilo podruhé na
        // sebe. Kontrola podle jména v AKTUÁLNÍ scéně (ne statické pole — to
        // by přežívalo i Stop/Play, kde se scéna vždy staví od nuly).
        string wallsName = $"Walls_{tilePos.x}_{tilePos.y}";
        if (GameObject.Find(wallsName) != null) return null;

        var loop = TraceIslandBoundary();
        if (loop == null) return null;
        var runs = MergeIntoStraightRuns(loop);
        if (runs.Count == 0) return null;

        // Směr k molu = stejný vzorec jako v GridManager.PlaceMegaIsland (mola
        // se vždy dává na stranu přivrácenou ke světovému počátku) — brána jde
        // na ten rovný úsek hradby, co je tomu směru nejblíž.
        Vector2 center = new Vector2(tilePos.x, tilePos.y);
        Vector2 toPier = -center;
        if (toPier.sqrMagnitude < 1f) toPier = Vector2.down;
        float pierAngleDeg = Mathf.Atan2(toPier.y, toPier.x) * Mathf.Rad2Deg;

        int gateRun = -1; float bestDiff = 999f;
        for (int i = 0; i < runs.Count; i++)
        {
            if (Mathf.RoundToInt(runs[i].Length) < 2) continue; // brána potřebuje aspoň 2 dlaždice místa
            Vector2 mid = runs[i].Midpoint - center;
            float diff = Mathf.Abs(Mathf.DeltaAngle(Mathf.Atan2(mid.y, mid.x) * Mathf.Rad2Deg, pierAngleDeg));
            if (diff < bestDiff) { bestDiff = diff; gateRun = i; }
        }

        // ZÁMĚRNĚ NEparentěno pod marker (transform) — na rozdíl od obelisku.
        // Hradby jsou fyzická stavba na ostrově, ne "UI" příběhového markeru:
        // GiveNextMegaIsland() marker znovu zničí, jakmile hráč dočte vzkaz
        // a příběh ho posune na další ostrov (viz TryReadMessage), ale hradby
        // mají zůstat stát, i když se tam hráč vrátí později. Kdyby byly dítě
        // markeru, zmizely by spolu s ním hned po přečtení vzkazu.
        var wallsGo = new GameObject(wallsName);
        wallsGo.transform.position = new Vector3(tilePos.x, 0f, tilePos.y);

        var towerCorners = PickTowerCorners(runs, 5);
        foreach (var corner in towerCorners)
        {
            Vector2 inward = center - corner; // věž "vchodem" dovnitř pevnosti
            float yRot = Mathf.Atan2(inward.y, inward.x) * Mathf.Rad2Deg;
            BuildPirateKitPart("tower-complete-small", new Vector3(corner.x, 0f, corner.y), yRot, wallsGo.transform);
        }

        for (int i = 0; i < runs.Count; i++)
        {
            var run = runs[i];
            int segCount = Mathf.RoundToInt(run.Length);
            float yRot = Mathf.Atan2(run.Dir.y, run.Dir.x) * Mathf.Rad2Deg;
            int gateSlot = i == gateRun ? segCount / 2 : -1; // dva prostřední díly úseku nahradí brána

            for (int s = 0; s < segCount; s++)
            {
                if (i == gateRun && (s == gateSlot - 1 || s == gateSlot))
                {
                    if (s == gateSlot - 1)
                    {
                        // Brána je 2x širší než jeden díl hradby — její STŘED je
                        // přesně na hranici mezi oběma nahrazenými sloty (offset
                        // `gateSlot`, ne `gateSlot - 0.5`, což byl bug: posunulo
                        // to bránu o půl dlaždice, takže na jedné straně vznikla
                        // mezera a na druhé se brána překrývala se zdí).
                        Vector2 gCenter = run.Start + run.Dir * gateSlot;
                        BuildPirateKitPart("castle-gate", new Vector3(gCenter.x, 0f, gCenter.y), yRot, wallsGo.transform);
                    }
                    continue; // oba prostřední sloty zůstanou bez hradby — tudy se vchází
                }

                Vector2 pos = run.Start + run.Dir * (s + 0.5f);
                BuildPirateKitPart("castle-wall", new Vector3(pos.x, 0f, pos.y), yRot, wallsGo.transform);
            }
        }

        return towerCorners;
    }

    // Jeden rovný úsek hradby (víc po sobě jdoucích hran hranice se stejným směrem).
    private class WallRun
    {
        public Vector2 Start;
        public Vector2 Dir;    // jednotkový směr, vždy osově zarovnaný (±1,0) nebo (0,±1)
        public float   Length; // v dlaždicích
        public Vector2 Midpoint => Start + Dir * (Length * 0.5f);
    }

    // Obejde hranici pevnina/voda ostrova po jednotkových hranách dlaždic
    // (klasický "marching squares" postup) a vrátí ji jako uzavřenou smyčku
    // rohových bodů v pořadí, jak jdou po obvodu. Pier dlaždice (molo) se
    // počítají jako pevnina, ať hradba obejde i výběžek mola místo aby ho
    // "prokousla" — bránu pak najde BuildWalls podle směru k molu.
    private List<Vector2> TraceIslandBoundary()
    {
        bool IsLand(int x, int y)
        {
            TileType t = gridManager.GetTileType(x, y);
            return t == TileType.MegaIsland || t == TileType.Pier;
        }

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
            loop.Add(new Vector2(cur.Item1 * 0.5f, cur.Item2 * 0.5f));
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

    private static void BuildPirateKitPart(string resourceName, Vector3 worldPos, float yRotationDeg, Transform parent)
    {
        var prefab = Resources.Load<GameObject>("PirateKit/" + resourceName);
        if (prefab == null) return;

        var go = Instantiate(prefab, worldPos, Quaternion.Euler(0f, yRotationDeg, 0f), parent);
        go.name = resourceName;
        go.transform.localScale = Vector3.one * WALL_SCALE;

        if (!wallColormapTried) { wallColormapTried = true; wallColormap = Resources.Load<Texture2D>("PirateKit/colormap"); }
        if (wallColormap != null)
        {
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>())
            {
                Shader sh = mr.sharedMaterial != null && mr.sharedMaterial.shader != null
                    ? mr.sharedMaterial.shader : (Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                var mat = new Material(sh);
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", wallColormap);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", wallColormap);
                mr.sharedMaterial = mat;
            }
        }
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
            return vault.Solved ? TryReadMessage() : vault.Open(playerIndex);

        // Podpalubí vraku (ostrov 2) — E přečte vzkaz, stejně jako trezor.
        if (holdBuilt && x == holdTile.x && y == holdTile.y)
            return TryReadMessage();

        if (x != tilePos.x || y != tilePos.y) return false;
        if (CombatDirector.Instance != null) CombatDirector.Instance.Toast(signText);
        return true;
    }

    /// <summary>Co udělá E na daném políčku — bez vedlejších účinků, jen na
    /// vypsání nápovědy (viz PlayerController.GetContextHint). Vrací null, když
    /// tam není nic k udělání.</summary>
    public string GetHint(int x, int y)
    {
        foreach (var guard in myGuards)
            if (guard != null && guard.IsAt(x, y)) return "zaútočit na stráž";

        if (vault != null && vault.IsAt(x, y))
        {
            if (!vault.Solved) return "otevřít trezor";
            return gridManager != null && gridManager.gameData.megaTask < 3 ? "přečíst vzkaz" : null;
        }

        if (holdBuilt && x == holdTile.x && y == holdTile.y)
            return gridManager != null && gridManager.gameData.megaTask < 3 ? "prohledat podpalubí" : null;

        if (x == tilePos.x && y == tilePos.y) return "prozkoumat obelisk";
        return null;
    }

    /// <summary>Zavolá LandGuard při své smrti — jen okamžitá hláška. Skutečný postup
    /// (megaTask) řeší Update() nahoře, který sečte, co všechno ještě žije.</summary>
    public void OnGuardDestroyed(LandGuard g)
    {
        if (CombatDirector.Instance == null) return;
        CombatDirector.Instance.RewardNearestPlayer(EconomyConfig.LandGuardReward);
        CombatDirector.Instance.Toast($"Stráž poražena.  +{EconomyConfig.LandGuardReward} minci");
    }

    // Vzkazy bratra na jednotlivých ostrovech (Krok 4 = Ostrov pirátů, Krok 6 =
    // ostrov 2) — čte se podle megaIndex. Ostrov 3 vzkaz nedává (tam už je
    // bratr osobně, viz plán §1).
    private static readonly string[] BROTHER_MESSAGES =
    {
        "Vzkaz: \"Přišel jsi pozdě. Mám to já — měl jsem to celou dobu. "
      + "Jestli fakt chcete, co je rodiny, přijeď si pro to sám.\"",
        "Vzkaz: \"Vylezl jsem z vody a loď s vámi byla pryč. Čekal jsem. Nikdo nepřijel.\"",
    };

    // Přečtení vzkazu (v trezoru na ostrově 1 / v podpalubí na ostrově 2) —
    // jen jednou, pak posune příběh na další mega ostrov.
    private bool TryReadMessage()
    {
        if (gridManager == null) return true;
        var d = gridManager.gameData;

        if (d.megaTask >= 3)
        {
            if (CombatDirector.Instance != null) CombatDirector.Instance.Toast("Vzkaz už jsi přečetl.");
            return true;
        }
        if (d.megaTask != 2) return true; // pojistka — nemělo by nastat (vault.Solved/podpalubí už megaTask=2 zajišťuje)

        d.megaTask = 3;
        gridManager.Save();

        string message = d.megaIndex >= 0 && d.megaIndex < BROTHER_MESSAGES.Length
            ? BROTHER_MESSAGES[d.megaIndex] : "Vzkaz beze slov.";
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
