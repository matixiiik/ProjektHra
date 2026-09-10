using UnityEngine;
using System.Collections.Generic;
using System;

// ─────────────────────────────────────────────────────────────────────────────
//  GridManager.cs  — SRDCE HRY
//
//  Stará se o celý herní svět (nekonečné moře s ostrovy):
//   • generuje políčka kolem hráče, když tam ještě žádná nejsou,
//   • vytváří / maže 3D objekty políček podle toho, kde zrovna hráč je,
//   • drží data o mlze (co je prozkoumané),
//   • ukládá a načítá hru (přes SaveManager),
//   • posílá událost OnWorldChanged, na kterou reaguje HUD a minimapa.
//
//  Svět je nekonečná mřížka. Data políček jsou ve slovníku gameData.tileData,
//  klíč je text "x,y". Uloží se jen políčka, která už byla někdy vygenerovaná.
// ─────────────────────────────────────────────────────────────────────────────

public class GridManager : MonoBehaviour
{
    // Kolik políček na každou stranu od hráče se drží "naživu" (s 3D objekty).
    // Obyčejná voda 3D objekt nevytváří (kreslí ji OceanSurface), takže objekty
    // mají jen ostrovy, ryby a poklady — číslo může být klidně vyšší.
    // Konec mlhy (viz SkyClouds) je sladěný s touhle hodnotou, aby "naskočení"
    // vzdálených ostrovů zůstalo schované v oparu.
    public const int ACTIVE_GRID_SIZE = 28;

    // Prefaby jednotlivých typů políček (nastavují se v inspektoru).
    public GameObject waterPrefab;
    public GameObject waterFishPrefab;
    public GameObject treasurePrefab;
    public GameObject harborPrefab;
    public GameObject pierPrefab;
    public GameObject upgradeShopPrefab;  // starý obchod — drží se kvůli starým savům
    public GameObject questShopPrefab;    // dtto
    public GameObject lighthousePrefab;   // maják (vejde se do něj – viz LighthouseManager)
    public GameObject chestPrefab;        // bedna na ostrově (otevírá ChestManager)
    public Material   islandTerrainMaterial; // materiál hladkého terénu ostrova

    // Výšky zvláštních mořských dlaždic vůči velké vodní ploše (viz OceanSurface).
    // Sladěné s OceanSurface.seaLevel (-0.22). Kdyby ryby/vrak plavaly nad vodou
    // nebo se topily, dolaď tady o pár setin.
    private const float FISH_TILE_Y     = -0.45f; // rybí dlaždice — kousek pod hladinou, přes průhlednou vodu prosvítá jako mělčina
    private const float TREASURE_TILE_Y = -1.9f;  // dlaždice pokladu hluboko → trup vraku sedí v mělčině (SeaFloor tam zvedne dno), nad hladinu kouká jen stěžeň
    private const float PIER_TILE_Y     = -0.25f; // molo posazené níž — dřevěná plošina těsně nad hladinou (cca ve výšce paluby lodě), ať nasedání nevypadá jako skok z výšky

    // Veškerý stav hry. Fyzicky ho drží GameSession (přežívá i přechod do
    // scény majáku), GridManager k němu jen přistupuje přes tuhle zkratku.
    public GameData gameData => GameSession.Instance != null ? GameSession.Instance.Data : null;

    // Právě existující 3D objekty políček. Klíč "x,y" → objekt ve scéně.
    private Dictionary<string, GameObject> activeTiles = new Dictionary<string, GameObject>();

    // Políčko, na kterém sedí příběhové NPC (děda) — nesmí na něm být žádná
    // dekorace. Zamluví si ho StoryNpc, jakmile se usadí (a znovu při každém
    // vygenerování té dlaždice, když se hráč vrátí).
    private Vector2Int? npcClearTile;

    // Hladké terénní meshe ostrovů. Klíč = "minX,minY" ostrova.
    private class IslandRec { public GameObject go; public List<string> tileKeys; }
    private Dictionary<string, IslandRec> islandTerrains = new Dictionary<string, IslandRec>();
    private HashSet<string> islandTilesWithTerrain = new HashSet<string>();

    // Ostrovy, které hráč zahlédl na dálku a jsou teď na velké mapě (klíč "minX,minY").
    // Cache z gameData.mappedIslands, aby se ostrov nemapoval (a neukládal) pořád dokola.
    private HashSet<string> mappedIslandKeys = new HashSet<string>();
    private int  lastMapScanX, lastMapScanY;
    private bool didFirstMapScan;

    /// <summary>Vyvolá se po každé změně světa (pohyb, těžba, nákup...). Poslouchá HUD a minimapa.</summary>
    public event Action OnWorldChanged;

    private int fogLayer;     // vrstva "Fog" (mlha se nekreslí do minimapy)
    private int minimapLayer; // vrstva "MinimapOnly" (ikony jen pro minimapu)

    // Parametry generování ostrovů.
    private const int ISLAND_SIZE         = 10; // "jmenovitá" velikost (kompatibilita se starým kódem)
    private const int ISLAND_CANVAS       = 20; // max rozměr organického ostrova (plátno, do kterého se vejde)
    private const int ISLAND_PADDING      = 1;  // volné pole kolem ostrova při kontrole místa
    private const int MIN_ISLAND_DISTANCE = 200;// minimální rozestup mezi ostrovy (dřív 50 — ostrovy jsou teď vzácnější)
    private const int CLEANUP_LIMIT       = 120;// políčka dál než tohle se ze save mažou

    void Awake()
    {
        fogLayer     = LayerMask.NameToLayer("Fog");
        minimapLayer = LayerMask.NameToLayer("MinimapOnly");

        // Ve scéně smí být aktivní jen jeden AudioListener (jinak Unity varuje).
        var listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        for (int i = 1; i < listeners.Length; i++) listeners[i].enabled = false;

        // Načti naposledy použitý slot a jeho data do GameSession.
        // (Interiér majáku ukládá po každé změně, takže save je vždy aktuální —
        //  i po návratu z majáku je bezpečné načíst ho znovu.)
        SaveManager.CurrentSlot = PlayerPrefs.GetInt("LastSlot", 0);
        GameSession.Ensure().SetData(SaveManager.LoadGame());

        // Úplně nová hra → vygeneruj startovní ostrov.
        if (gameData.tileData.Count == 0) GenerateInitialWorld();

        // Starý save bez uložené dekorace ostrovů → doplň ji jednou (a ulož).
        MigrateIslandDecor();

        // Načti seznam ostrovů, co jsou už na velké mapě.
        mappedIslandKeys = new HashSet<string>(gameData.mappedIslands);

        GenerateWorld(gameData.playerGridX, gameData.playerGridY);
        CreateSeaWorld();

        // Po načtení save s už objeveným mega ostrovem obnov jeho obelisk.
        if (gameData.storyIslandActive
            && FindFirstObjectByType<MegaIslandMarker>() == null)
        {
            var markerGo = new GameObject("MegaIslandMarker");
            markerGo.transform.SetParent(transform);
            markerGo.transform.position = new Vector3(gameData.storyIslandX, 0f, gameData.storyIslandY);
            markerGo.AddComponent<MegaIslandMarker>();
        }

        OnWorldChanged?.Invoke();

        SoundManager.StartWaves(); // hukot moře na pozadí
        if (GameSession.ReturningFromLighthouse) SoundManager.PlayDoor(); // vrznutí — vyšel ven z majáku

        CombatDirector.Ensure(); // piráti + děla nepřátelských ostrovů
        SoloPause.Ensure();      // v sólu pauza při otevřeném obchodu / dialogu / mapě
    }

    // Při zavření hry ulož.
    void OnApplicationQuit() => Save();

    // ── Moře jako celek: hladina + dno + obloha ────────────────────────────
    // Místo stovek malých vodních dlaždic je celé moře jedna poloprůhledná
    // plocha (OceanSurface), pod ní členité dno (SeaFloor) a nad tím obloha
    // s mraky (SkyClouds). Všechno jede za hráčem. Materiál vody si půjčíme
    // z waterPrefabu, materiál dna z terénu ostrova.
    private void CreateSeaWorld()
    {
        Material waterMat = null;
        if (waterPrefab != null)
        {
            var r = waterPrefab.GetComponentInChildren<MeshRenderer>();
            if (r != null) waterMat = r.sharedMaterial;
        }

        Transform p1 = null;
        foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            if (pc.playerIndex == 0) { p1 = pc.transform; break; }

        var ocean = new GameObject("Ocean");
        ocean.transform.SetParent(transform);
        ocean.AddComponent<OceanSurface>().Init(waterMat, p1);

        var floor = new GameObject("SeaFloor");
        floor.transform.SetParent(transform);
        floor.AddComponent<SeaFloor>().Init(islandTerrainMaterial, p1, this);

        var sky = new GameObject("SkyClouds");
        sky.transform.SetParent(transform);
        sky.AddComponent<SkyClouds>().Init(p1);
    }

    /// <summary>Uklidí zbytečná data a uloží hru na disk.</summary>
    public void Save()
    {
        CleanupWorldData();
        SaveManager.SaveGame(gameData);
    }

    // ── Práce s klíči slovníku ("x,y") ──────────────────────────────────────
    private static string GridKey(int x, int y) => $"{x},{y}";

    private static (int x, int y) ParseGridKey(string key)
    {
        var p = key.Split(',');
        return (int.Parse(p[0]), int.Parse(p[1]));
    }

    // ── Úklid uložených dat ─────────────────────────────────────────────────
    // Smaže políčka daleko od hráče, ale nechá důležitá (ostrovy, prozkoumaná),
    // aby save nerostl donekonečna.
    private void CleanupWorldData()
    {
        var keysToRemove = new List<string>();

        foreach (var entry in gameData.tileData)
        {
            var (x, y) = ParseGridKey(entry.Key);

            bool isTooFar = Mathf.Abs(x - gameData.playerGridX) > CLEANUP_LIMIT
                         || Mathf.Abs(y - gameData.playerGridY) > CLEANUP_LIMIT;

            bool isImportant = entry.Value.isExplored || IsIslandTile(entry.Value.type);

            if (isTooFar && !isImportant) keysToRemove.Add(entry.Key);
        }

        foreach (string key in keysToRemove) gameData.tileData.Remove(key);
    }

    // ── Mlha / prozkoumávání ────────────────────────────────────────────────

    /// <summary>Označí jedno políčko jako prozkoumané a schová u něj mlhu.</summary>
    public void MarkTileExplored(int x, int y)
    {
        string key = GridKey(x, y);
        if (!gameData.tileData.ContainsKey(key)) return;
        if (gameData.tileData[key].isExplored) return;

        gameData.tileData[key].isExplored = true;
        HideFogAt(key);
        OnWorldChanged?.Invoke();
    }

    /// <summary>Označí čtverec políček (střed cx,cy, poloměr radius) jako prozkoumaný.</summary>
    public void MarkAreaExplored(int cx, int cy, int radius)
    {
        for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
            {
                string key = GridKey(cx + x, cy + y);
                if (!gameData.tileData.ContainsKey(key)) continue;
                if (gameData.tileData[key].isExplored) continue;

                gameData.tileData[key].isExplored = true;
                HideFogAt(key);
            }
        OnWorldChanged?.Invoke();
    }

    // Vypne objekt "FogVisual" u aktivního políčka (pokud existuje).
    private void HideFogAt(string key)
    {
        if (!activeTiles.ContainsKey(key)) return;
        Transform fog = activeTiles[key].transform.Find("FogVisual");
        if (fog != null) fog.gameObject.SetActive(false);
    }

    // ── Generování / obnova okolí hráče ─────────────────────────────────────

    /// <summary>
    /// Přegeneruje svět tak, aby kolem daného středu (a v multiplayeru i kolem
    /// druhého hráče) byla políčka. Vzdálené 3D objekty smaže.
    /// </summary>
    public void GenerateWorld(int centerX, int centerY)
    {
        ClearOldTiles();
        GenerateRegion(centerX, centerY);

        // V multiplayeru drž naživu i okolí obou hráčů.
        if (MultiplayerManager.IsMultiplayer)
        {
            int p1x = gameData.playerGridX,  p1y = gameData.playerGridY;
            int p2x = gameData.player2GridX, p2y = gameData.player2GridY;
            if (centerX != p1x || centerY != p1y) GenerateRegion(p1x, p1y);
            if (centerX != p2x || centerY != p2y) GenerateRegion(p2x, p2y);
        }

        OnWorldChanged?.Invoke();
    }

    // Zajistí data i 3D objekty pro čtverec políček kolem středu.
    private void GenerateRegion(int centerX, int centerY)
    {
        for (int x = centerX - ACTIVE_GRID_SIZE; x <= centerX + ACTIVE_GRID_SIZE; x++)
        {
            for (int y = centerY - ACTIVE_GRID_SIZE; y <= centerY + ACTIVE_GRID_SIZE; y++)
            {
                string key = GridKey(x, y);
                if (!gameData.tileData.ContainsKey(key)) CheckAndGenerateArea(x, y); // vytvoř data
                if (!activeTiles.ContainsKey(key))       InstantiateTile(x, y, gameData.tileData[key]); // vytvoř objekt

                // Mega ostrov nemá per-dlaždicový prefab (InstantiateTile ho přeskočí) —
                // terénní mesh se mu tedy musí zajistit tady.
                if ((TileType)gameData.tileData[key].type == TileType.MegaIsland)
                    EnsureIslandTerrain(x, y);
            }
        }
    }

    // Rozhodne, co na daném (zatím prázdném) políčku vznikne: ostrov nebo moře.
    private void CheckAndGenerateArea(int x, int y)
    {
        // Ostrovy vznikají jen na mřížce každých 40 políček, s 30% pravděpodobností,
        // a jen když je kolem dost místa (min. rozestup 200 — viz CanPlaceIsland).
        // Ostrov je organický a nemusí přesně pokrýt spouštěcí políčko [x,y] —
        // pokud ne, doplní se dole moře.
        // Ostrovy jen na mřížce po 40. Vynucené ostrovy (konzole "locate pirate")
        // vzniknou vždy — bez hodu kostkou.
        bool onLattice = x % 40 == 0 && y % 40 == 0;
        bool forced = onLattice && gameData.forcedIslandKeys != null
                   && gameData.forcedIslandKeys.Contains(GridKey(x, y));
        if (onLattice && (forced || UnityEngine.Random.value < 0.3f) && CanPlaceIsland(x, y))
            GenerateIsland(x, y);

        // Jinak obyčejné mořské políčko (většinou voda, občas ryby / poklad).
        string key = GridKey(x, y);
        if (!gameData.tileData.ContainsKey(key))
        {
            TileType seaType = GenerateRandomSeaType();

            // Blízko ostrova ať je klid — žádné poklady ani ryby (a piráti taky ne,
            // to řeší CombatDirector). Test se dělá jen po nenulovém hodu, ať to
            // nestojí výkon na 99,5 % vodních políček.
            if ((seaType == TileType.Treasure || seaType == TileType.Water_Fish)
                && IsNearIsland(x, y, SPAWN_ISLAND_CLEARANCE))
                seaType = TileType.Water;

            var status = new TileStatus((int)seaType);
            if (seaType == TileType.Water_Fish) status.fishRemaining = 3;
            gameData.tileData.Add(key, status);
        }
    }

    // Je kolem [centerX,centerY] volné místo na nový ostrov? (ostrov je vycentrovaný na tento bod)
    private bool CanPlaceIsland(int centerX, int centerY)
    {
        if (IsAnotherIslandTooClose(centerX, centerY)) return false;

        int r     = ISLAND_CANVAS / 2 + ISLAND_PADDING;
        int fromX = centerX - r;
        int toX   = centerX + r;
        int fromY = centerY - r;
        int toY   = centerY + r;

        // V ploše plátna ostrova (+ okraj) nesmí být kus jiného ostrova.
        for (int x = fromX; x <= toX; x++)
        {
            for (int y = fromY; y <= toY; y++)
            {
                string key = GridKey(x, y);
                if (!gameData.tileData.ContainsKey(key)) continue;
                if (IsIslandTile(gameData.tileData[key].type)) return false;
            }
        }
        return true;
    }

    // Je poblíž jiný ostrov (blíž než MIN_ISLAND_DISTANCE)? centerX,centerY = střed nového ostrova.
    private bool IsAnotherIslandTooClose(int centerX, int centerY)
    {
        int max = MIN_ISLAND_DISTANCE + ISLAND_CANVAS;

        foreach (var kv in gameData.tileData)
        {
            if (!IsIslandTile(kv.Value.type)) continue;

            var (x, y) = ParseGridKey(kv.Key);
            if (Mathf.Abs(x - centerX) > max || Mathf.Abs(y - centerY) > max) continue; // hrubý rychlý test

            float dx = x - centerX, dy = y - centerY;
            if (dx * dx + dy * dy < MIN_ISLAND_DISTANCE * MIN_ISLAND_DISTANCE) return true; // přesný test
        }
        return false;
    }

    // Patří tento typ políčka k ostrovu (pevnina / molo / obchod / maják)?
    private static bool IsIslandTile(int type)
        => type == (int)TileType.Harbor || type == (int)TileType.Pier
        || type == (int)TileType.UpgradeShop || type == (int)TileType.QuestShop
        || type == (int)TileType.Lighthouse || type == (int)TileType.Chest
        || type == (int)TileType.MegaIsland;

    // Jak daleko od ostrova nesmí vzniknout poklad / ryby / pirát (klidná zóna).
    public const int SPAWN_ISLAND_CLEARANCE = 25;

    /// <summary>Je políčko [x,y] blíž než `radius` k nějakému ostrovnímu políčku?</summary>
    public bool IsNearIsland(int x, int y, int radius)
    {
        int r2 = radius * radius;
        foreach (var kv in gameData.tileData)
        {
            if (!IsIslandTile(kv.Value.type)) continue;
            var (ix, iy) = ParseGridKey(kv.Key);
            int dx = ix - x, dy = iy - y;
            if (dx > radius || dx < -radius || dy > radius || dy < -radius) continue; // rychlý test
            if (dx * dx + dy * dy <= r2) return true;
        }
        return false;
    }

    // ── Zahlédnutí ostrova na dálku → přidat na velkou mapu ─────────────────
    /// <summary>
    /// Ostrov blíž než `radius` políček od [cx,cy] se přidá na velkou mapu (a
    /// zůstane tam), i když u něj hráč fyzicky nebyl. Volá PlayerController při
    /// pohybu. Sám si to throttluje (scan celého světa jen po posunu o 10 políček).
    /// </summary>
    public void MapNearbyIslands(int cx, int cy, int radius)
    {
        if (didFirstMapScan && Mathf.Abs(cx - lastMapScanX) < 10 && Mathf.Abs(cy - lastMapScanY) < 10) return;
        didFirstMapScan = true;
        lastMapScanX = cx;
        lastMapScanY = cy;

        long r2 = (long)radius * radius;
        List<Vector2Int> seeds = null;
        foreach (var kv in gameData.tileData)
        {
            if (!IsIslandTile(kv.Value.type)) continue;
            var (x, y) = ParseGridKey(kv.Key);
            long dx = x - cx, dy = y - cy;
            if (dx * dx + dy * dy > r2) continue;
            (seeds ??= new List<Vector2Int>()).Add(new Vector2Int(x, y));
        }
        if (seeds == null) return;

        bool any = false;
        foreach (var s in seeds)
        {
            var tiles = FloodFillIsland(s.x, s.y, out int minX, out int minY);
            if (tiles.Count == 0) continue;

            string key = minX + "," + minY;
            if (!mappedIslandKeys.Add(key)) continue; // ostrov už na mapě je

            gameData.mappedIslands.Add(key);
            foreach (var t in tiles)
                if (gameData.tileData.TryGetValue(GridKey(t.x, t.y), out var st))
                    st.mapped = true;
            any = true;
        }

        if (any) { Save(); OnWorldChanged?.Invoke(); }
    }

    // Flood-fill spojité souše (Harbor/Pier/Lighthouse/Chest/MegaIsland) z [sx,sy].
    private List<Vector2Int> FloodFillIsland(int sx, int sy, out int minX, out int minY)
    {
        var result = new List<Vector2Int>();
        minX = sx; minY = sy;

        var seen  = new HashSet<Vector2Int>();
        var stack = new Stack<Vector2Int>();
        stack.Push(new Vector2Int(sx, sy));

        while (stack.Count > 0)
        {
            var c = stack.Pop();
            if (!seen.Add(c)) continue;
            if (!IsIslandTile((int)GetTileType(c.x, c.y))) continue;

            result.Add(c);
            if (c.x < minX) minX = c.x;
            if (c.y < minY) minY = c.y;

            stack.Push(new Vector2Int(c.x + 1, c.y));
            stack.Push(new Vector2Int(c.x - 1, c.y));
            stack.Push(new Vector2Int(c.x, c.y + 1));
            stack.Push(new Vector2Int(c.x, c.y - 1));
        }
        return result;
    }

    // ── Organický (nepravidelný) ostrov ────────────────────────────────────
    // Ostrov není čtverec: pevné jádro (min. 4×4) + náhodné rozrůstání na okraj.
    // Vrací seznam souřadnic pevniny (Harbor).
    private List<(int x, int y)> StampOrganicLand(int centerX, int centerY, bool explored)
    {
        int cx = centerX;
        int cy = centerY;
        int half = ISLAND_CANVAS / 2 - 1; // meze plátna (nech okraj volný pro molo)

        var land = new HashSet<(int, int)>();

        // 1) Pevné jádro — náhodný obdélník 7..10 × 7..10 uprostřed (min. 7×7),
        //    ať je na ostrově dost místa a dekorace se nemusí mačkat na sebe.
        int cw = UnityEngine.Random.Range(7, 11);
        int ch = UnityEngine.Random.Range(7, 11);
        for (int x = cx - cw / 2; x <= cx - cw / 2 + cw - 1; x++)
            for (int y = cy - ch / 2; y <= cy - ch / 2 + ch - 1; y++)
                land.Add((x, y));

        // 2) Organické rozrůstání — opakovaně přilep náhodné políčko na okraj tvaru.
        var dirs4 = new (int dx, int dy)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        var frontier = new List<(int, int)>(land);
        int grow = UnityEngine.Random.Range(40, 90);
        for (int i = 0; i < grow; i++)
        {
            var pick = frontier[UnityEngine.Random.Range(0, frontier.Count)];
            var d = dirs4[UnityEngine.Random.Range(0, 4)];
            var n = (pick.Item1 + d.dx, pick.Item2 + d.dy);
            if (Mathf.Abs(n.Item1 - cx) > half || Mathf.Abs(n.Item2 - cy) > half) continue;
            if (land.Add(n)) frontier.Add(n);
        }

        // 2b) Zaplň zálivy/díry: nepevninové políčko se 3+ pevninovými sousedy → pevnina.
        //     (opakuj — zahladí i užší výběžky, ať ostrov není děravý)
        for (int pass = 0; pass < 3; pass++)
        {
            var fill = new List<(int, int)>();
            foreach (var p in land)
                foreach (var d in dirs4)
                {
                    var n = (p.Item1 + d.dx, p.Item2 + d.dy);
                    if (land.Contains(n)) continue;
                    if (Mathf.Abs(n.Item1 - cx) > half || Mathf.Abs(n.Item2 - cy) > half) continue;
                    int nb = 0;
                    foreach (var dd in dirs4)
                        if (land.Contains((n.Item1 + dd.dx, n.Item2 + dd.dy))) nb++;
                    if (nb >= 3) fill.Add(n);
                }
            foreach (var p in fill) land.Add(p);
        }

        // 3) Zapiš jako Harbor.
        var result = new List<(int x, int y)>();
        foreach (var p in land)
        {
            string key = GridKey(p.Item1, p.Item2);
            bool wasExplored = explored
                || (gameData.tileData.ContainsKey(key) && gameData.tileData[key].isExplored);
            gameData.tileData[key] = new TileStatus((int)TileType.Harbor) { isExplored = wasExplored };
            result.Add((p.Item1, p.Item2));
        }
        return result;
    }

    // Vygeneruje celý ostrov: organická pevnina + molo + maják + (možná) bedna.
    private void GenerateIsland(int startX, int startY)
    {
        var land = StampOrganicLand(startX, startY, explored: false);
        if (land.Count < 49) return; // pojistka (jádro je min. 7×7)

        PlaceEdgePier(land);
        PlaceLighthouse(land);
        MaybePlaceChest(land);
        ClearSpawnsNearIsland(land); // klidná zóna: smaž poklady/ryby, co vznikly dřív, než tu byl ostrov
        AssignIslandDecor(land);     // dekorace se vygeneruje jednou a uloží se do dlaždic

        // ~40 % ostrovů je nepřátelských — mají dělo, co po hráči střílí
        // (klíč = bod, kolem kterého ostrov vznikl; ten je stabilní).
        // Vynucený ostrov (konzole) je nepřátelský vždy.
        string ikey = GridKey(startX, startY);
        bool forcedHostile = gameData.forcedIslandKeys != null && gameData.forcedIslandKeys.Contains(ikey);
        if (forcedHostile || UnityEngine.Random.value < 0.40f)
        {
            if (!gameData.hostileIslands.Contains(ikey)) gameData.hostileIslands.Add(ikey);
        }
    }

    // Kolem nově vzniklého ostrova udělá klidnou zónu: políčka s pokladem nebo
    // rybami blíž než SPAWN_ISLAND_CLEARANCE k pevnině převede zpět na vodu.
    // (Mořská políčka se generují dřív než ostrov, takže se to musí uklidit.)
    private void ClearSpawnsNearIsland(List<(int x, int y)> land)
    {
        int r = SPAWN_ISLAND_CLEARANCE;
        int r2 = r * r;

        // hrubé ohraničení ostrova, ať neprocházíme celý slovník zbytečně daleko
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var p in land)
        {
            if (p.x < minX) minX = p.x;
            if (p.y < minY) minY = p.y;
            if (p.x > maxX) maxX = p.x;
            if (p.y > maxY) maxY = p.y;
        }

        var toWater = new List<string>();
        foreach (var kv in gameData.tileData)
        {
            int t = kv.Value.type;
            if (t != (int)TileType.Treasure && t != (int)TileType.Water_Fish) continue;

            var (tx, ty) = ParseGridKey(kv.Key);
            if (tx < minX - r || tx > maxX + r || ty < minY - r || ty > maxY + r) continue;

            foreach (var p in land)
            {
                int dx = p.x - tx, dy = p.y - ty;
                if (dx * dx + dy * dy <= r2) { toWater.Add(kv.Key); break; }
            }
        }

        foreach (string key in toWater)
        {
            gameData.tileData[key].type = (int)TileType.Water;
            gameData.tileData[key].fishRemaining = 0;
        }
    }

    // Dvě políčka mola vedle sebe na okraji ostrova (obě mají "ven" vodu).
    //  Molo = dvoupolíčkový výběžek DO VODY (přístaviště): vnitřní dlaždice se
    //  dotýká pevniny, vnější má vodu na 3 strany. Loď se pak dá zaparkovat
    //  hned za koncem mola a nasedání/vylodění je jednoznačné.
    private void PlaceEdgePier(List<(int x, int y)> land)
    {
        var set  = new HashSet<(int, int)>(land);
        var dirs = new (int dx, int dy)[] { (0, -1), (0, 1), (-1, 0), (1, 0) };
        ShuffleDirs(dirs);

        var lands = new List<(int x, int y)>(land);
        ShuffleTiles(lands);

        // 1. pokus: molo s vodou na 3 strany u vnější dlaždice (ideál).
        if (TryPlaceJetty(set, lands, dirs, strict: true))  return;
        // 2. pokus: aspoň výběžek 2 dlaždic do vody.
        if (TryPlaceJetty(set, lands, dirs, strict: false)) return;

        // 3. pojistka: dvě dlaždice na kraji ostrova vedle sebe (starý styl).
        foreach (var d in dirs)
        {
            var perp = d.dx == 0 ? (dx: 1, dy: 0) : (dx: 0, dy: 1);
            foreach (var p in lands)
            {
                if (set.Contains((p.x + d.dx, p.y + d.dy))) continue;
                var q = (p.x + perp.dx, p.y + perp.dy);
                if (!set.Contains(q)) continue;
                if (set.Contains((q.Item1 + d.dx, q.Item2 + d.dy))) continue;

                gameData.tileData[GridKey(p.x, p.y)]         = new TileStatus((int)TileType.Pier);
                gameData.tileData[GridKey(q.Item1, q.Item2)] = new TileStatus((int)TileType.Pier);
                return;
            }
        }
    }

    // Zkusí položit dvoupolíčkové molo trčící z pevniny do vody.
    //  p            = pevninová dlaždice na kraji
    //  p+d, p+2d    = vnitřní a vnější dlaždice mola (voda → Pier)
    //  p+3d         = voda za molem (kam se dá zaparkovat loď)
    private bool TryPlaceJetty(HashSet<(int, int)> set, List<(int x, int y)> lands,
                               (int dx, int dy)[] dirs, bool strict)
    {
        foreach (var p in lands)
            foreach (var d in dirs)
            {
                var i1 = (p.x + d.dx,       p.y + d.dy);
                var i2 = (p.x + 2 * d.dx,   p.y + 2 * d.dy);
                var i3 = (p.x + 3 * d.dx,   p.y + 3 * d.dy);

                // celý výběžek + voda za ním musí být mimo pevninu
                if (set.Contains(i1) || set.Contains(i2) || set.Contains(i3)) continue;

                var pp = d.dx == 0 ? (1, 0) : (0, 1); // kolmý směr
                // boky vnitřní dlaždice nesmí být pevnina (jinak to netrčí)
                if (set.Contains((i1.Item1 + pp.Item1, i1.Item2 + pp.Item2)) ||
                    set.Contains((i1.Item1 - pp.Item1, i1.Item2 - pp.Item2))) continue;

                if (strict &&
                   (set.Contains((i2.Item1 + pp.Item1, i2.Item2 + pp.Item2)) ||
                    set.Contains((i2.Item1 - pp.Item1, i2.Item2 - pp.Item2)))) continue;

                gameData.tileData[GridKey(i1.Item1, i1.Item2)] = new TileStatus((int)TileType.Pier);
                gameData.tileData[GridKey(i2.Item1, i2.Item2)] = new TileStatus((int)TileType.Pier);
                return true;
            }
        return false;
    }

    private void ShuffleDirs((int dx, int dy)[] a)
    {
        for (int i = 0; i < a.Length; i++)
        {
            int j = UnityEngine.Random.Range(i, a.Length);
            var t = a[i]; a[i] = a[j]; a[j] = t;
        }
    }

    private void ShuffleTiles(List<(int x, int y)> a)
    {
        for (int i = 0; i < a.Count; i++)
        {
            int j = UnityEngine.Random.Range(i, a.Count);
            var t = a[i]; a[i] = a[j]; a[j] = t;
        }
    }

    // Maják zabírá 2×2 políčka uvnitř pevniny (na maják se nechodí).
    private void PlaceLighthouse(List<(int x, int y)> land)
    {
        var set = new HashSet<(int, int)>(land);

        var spots = new List<(int x, int y)>();
        foreach (var p in land)
        {
            // p = levý dolní roh 2×2, všechny 4 musí být pevnina a zatím Harbor
            if (set.Contains((p.x + 1, p.y)) && set.Contains((p.x, p.y + 1)) && set.Contains((p.x + 1, p.y + 1))
                && IsHarborTile(p.x, p.y) && IsHarborTile(p.x + 1, p.y)
                && IsHarborTile(p.x, p.y + 1) && IsHarborTile(p.x + 1, p.y + 1)
                // maják nesmí stát hned vedle mola — ať si hráč nesplete cestu na molo se vstupem do majáku
                && !Any2x2TileTouchesPier(p.x, p.y)
                // maják nesmí ostrov rozdělit — hráč se musí pořád dostat na molo
                && LighthouseKeepsIslandWalkable(p, set))
                spots.Add(p);
        }
        if (spots.Count == 0) return;

        var a = spots[UnityEngine.Random.Range(0, spots.Count)];
        for (int ix = 0; ix < 2; ix++)
            for (int iy = 0; iy < 2; iy++)
                gameData.tileData[GridKey(a.x + ix, a.y + iy)] = new TileStatus((int)TileType.Lighthouse);
    }

    // Ověří, že po položení majáku (2×2 od p) zůstane celá zbylá pevnina jedním
    // souvislým kusem — hráč se pořád dostane odkudkoli na molo (maják neroztne ostrov).
    private bool LighthouseKeepsIslandWalkable((int x, int y) p, HashSet<(int, int)> land)
    {
        var block = new HashSet<(int, int)>
        {
            (p.x, p.y), (p.x + 1, p.y), (p.x, p.y + 1), (p.x + 1, p.y + 1)
        };

        (int, int)? start = null;
        foreach (var t in land) if (!block.Contains(t)) { start = t; break; }
        if (start == null) return false;

        var seen  = new HashSet<(int, int)>();
        var stack = new Stack<(int, int)>();
        stack.Push(start.Value);
        seen.Add(start.Value);
        var dirs = new (int dx, int dy)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };

        while (stack.Count > 0)
        {
            var c = stack.Pop();
            foreach (var d in dirs)
            {
                var n = (c.Item1 + d.dx, c.Item2 + d.dy);
                if (block.Contains(n) || !land.Contains(n) || seen.Contains(n)) continue;
                seen.Add(n);
                stack.Push(n);
            }
        }

        // Musí projít úplně všechna políčka pevniny mimo maják.
        foreach (var t in land)
            if (!block.Contains(t) && !seen.Contains(t)) return false;
        return true;
    }

    // True, když aspoň jedno políčko bloku 2×2 (levý dolní roh x,y) sousedí (4-směrně) s molem.
    private bool Any2x2TileTouchesPier(int x, int y)
    {
        for (int ix = 0; ix < 2; ix++)
            for (int iy = 0; iy < 2; iy++)
            {
                int tx = x + ix, ty = y + iy;
                if (GetTileType(tx + 1, ty) == TileType.Pier || GetTileType(tx - 1, ty) == TileType.Pier
                 || GetTileType(tx, ty + 1) == TileType.Pier || GetTileType(tx, ty - 1) == TileType.Pier)
                    return true;
            }
        return false;
    }

    // S 25% šancí položí jednu bednu na náhodné (volné) políčko pevniny.
    private void MaybePlaceChest(List<(int x, int y)> land)
    {
        if (UnityEngine.Random.value >= 0.25f) return;

        var free = new List<(int x, int y)>();
        foreach (var p in land)
            if (IsHarborTile(p.x, p.y)) free.Add(p);
        if (free.Count == 0) return;

        var c = free[UnityEngine.Random.Range(0, free.Count)];
        gameData.tileData[GridKey(c.x, c.y)] = new TileStatus((int)TileType.Chest);
    }

    private bool IsHarborTile(int x, int y)
    {
        string key = GridKey(x, y);
        return gameData.tileData.ContainsKey(key)
            && gameData.tileData[key].type == (int)TileType.Harbor;
    }

    // ── Dekorace ostrova (kameny / palmy / trsy) ───────────────────────────
    // Vygeneruje se JEDNOU při vzniku ostrova a uloží se přímo do dlaždic
    // (TileStatus.decor / decorRot / decorScale / tileRot). Po návratu z majáku
    // se ostrov znovu vytvoří ze save → vypadá pořád stejně. Nová hra vygeneruje
    // ostrovy znova (jsou jinde a projdou tímhle znova).
    private void AssignIslandDecor(List<(int x, int y)> islandTiles)
    {
        // Kandidáti = dlaždice, co ZŮSTALY pevninou (Harbor) a ještě nemají
        // dekoraci vyřešenou. Zároveň jim tady nastav natočení písku.
        var cand = new List<(int x, int y)>();
        foreach (var p in islandTiles)
        {
            string key = GridKey(p.x, p.y);
            if (!gameData.tileData.ContainsKey(key)) continue;
            var st = gameData.tileData[key];
            if (st.type != (int)TileType.Harbor) continue;
            if (st.decor != 0) continue; // už vyřešeno

            st.tileRot = UnityEngine.Random.Range(0, 4);
            cand.Add(p);
        }
        if (cand.Count == 0) return;

        // Zamíchej pořadí kandidátů.
        for (int i = 0; i < cand.Count; i++)
        {
            int j = UnityEngine.Random.Range(i, cand.Count);
            var t = cand[i]; cand[i] = cand[j]; cand[j] = t;
        }

        // Kolik dekorací ostrov dostane: 5–8 (méně, když je ostrov malý).
        int want = Mathf.Min(UnityEngine.Random.Range(5, 9), cand.Count);
        var used = new HashSet<(int, int)>();
        int placed = 0;

        // 1. kolo: ber jen dlaždice, co 4-směrně nesousedí s už použitou →
        //          dekorace se nemačká na sebe.
        foreach (var p in cand)
        {
            if (placed >= want) break;
            if (used.Contains((p.x + 1, p.y)) || used.Contains((p.x - 1, p.y))
             || used.Contains((p.x, p.y + 1)) || used.Contains((p.x, p.y - 1))) continue;
            SetTileDecor(p.x, p.y);
            used.Add((p.x, p.y));
            placed++;
        }
        // 2. kolo: kdyby první nestačilo (malý ostrov), doplň i sousedící.
        foreach (var p in cand)
        {
            if (placed >= want) break;
            if (used.Contains((p.x, p.y))) continue;
            SetTileDecor(p.x, p.y);
            used.Add((p.x, p.y));
            placed++;
        }

        // Zbylým kandidátům řekni "záměrně bez dekorace" (aby byli vyřešení).
        foreach (var p in cand)
            if (!used.Contains((p.x, p.y)))
                gameData.tileData[GridKey(p.x, p.y)].decor = 1;
    }

    private void SetTileDecor(int x, int y)
    {
        var st = gameData.tileData[GridKey(x, y)];
        st.decor      = 2 + UnityEngine.Random.Range(0, 10000); // model = (decor-2) % počet_modelů (řeší IslandDecor)
        st.decorRot   = UnityEngine.Random.Range(0, 360);
        st.decorScale = UnityEngine.Random.Range(80, 126);      // 80–125 %
    }

    // Starý save (ostrovy vygenerované před zavedením uložené dekorace): projdi
    // pevninové dlaždice bez určené dekorace, seskup je do ostrovů a přiřaď
    // dekoraci. Uloží se → po návratu z majáku se pak už nepřehazuje.
    private void MigrateIslandDecor()
    {
        var visited = new HashSet<string>();
        var islands = new List<List<(int x, int y)>>();

        var startKeys = new List<string>(gameData.tileData.Keys);
        foreach (string k in startKeys)
        {
            if (!gameData.tileData.TryGetValue(k, out var st0)) continue;
            if (st0.type != (int)TileType.Harbor || st0.decor != 0) continue;
            if (visited.Contains(k)) continue;

            // Flood-fill spojité souše (Harbor / Lighthouse / Chest), seber Harbor.
            var group = new List<(int x, int y)>();
            var stack = new Stack<(int x, int y)>();
            var (sx, sy) = ParseGridKey(k);
            stack.Push((sx, sy));
            while (stack.Count > 0)
            {
                var c = stack.Pop();
                string ck = GridKey(c.x, c.y);
                if (visited.Contains(ck)) continue;
                var t = GetTileType(c.x, c.y);
                if (t != TileType.Harbor && t != TileType.Lighthouse && t != TileType.Chest) continue;
                visited.Add(ck);
                if (t == TileType.Harbor) group.Add((c.x, c.y));
                stack.Push((c.x + 1, c.y)); stack.Push((c.x - 1, c.y));
                stack.Push((c.x, c.y + 1)); stack.Push((c.x, c.y - 1));
            }
            if (group.Count > 0) islands.Add(group);
        }

        if (islands.Count == 0) return;
        foreach (var g in islands) AssignIslandDecor(g);
        SaveManager.SaveGame(gameData); // ať to reload po návratu z majáku najde hotové
    }

    // Náhodný typ mořského políčka: 0,10 % vrak s pokladem, 0,35 % ryby, zbytek voda.
    // Vraky jsou vzácné schválně — má se za nimi "lovit", ne je potkávat na potkání.
    private TileType GenerateRandomSeaType()
    {
        float roll = UnityEngine.Random.value * 100f;
        if (roll < 0.10f) return TileType.Treasure;
        if (roll < 0.45f) return TileType.Water_Fish;
        return TileType.Water;
    }

    // ── Vytvoření / mazání 3D objektů políček ──────────────────────────────

    // Vytvoří 3D objekt jednoho políčka podle jeho typu.
    private void InstantiateTile(int x, int y, TileStatus status)
    {
        GameObject prefab = GetPrefabForType((TileType)status.type);
        if (prefab == null) return; // obyčejná voda se nekreslí po dlaždicích — je to velká plocha (OceanSurface)

        Vector3 pos = new Vector3(x, -0.1f, y);

        // Rybí dlaždici posaď kousek pod hladinu — přes poloprůhlednou vodu
        // (OceanSurface) prosvítá jako světlejší mělčina = poznáš kde rybařit.
        // Zvlášť se nehoupe (WaterWave dole zrušíme).
        if ((TileType)status.type == TileType.Water_Fish) pos.y = FISH_TILE_Y;

        // Molo posaď níž k hladině (dřevěná plošina jen kousek nad vodou).
        if ((TileType)status.type == TileType.Pier) pos.y = PIER_TILE_Y;

        // Poklad: celou dlaždici (i s vrakem) posaď hluboko pod hladinu, ať vrak
        // vypadá jako potopená troska — kouká jen kus trupu a stěžeň. Kolem je
        // jen velká voda ve stejné barvě, žádná tmavší dlaždice.
        if ((TileType)status.type == TileType.Treasure) pos.y = TREASURE_TILE_Y;

        GameObject newTile = Instantiate(prefab, pos, Quaternion.identity, transform);
        activeTiles.Add(GridKey(x, y), newTile);

        // Dědovo políčko musí zůstat holé — sundej z něj dekoraci, kterou si
        // HarborPrefab (IslandDecor) právě přidal ve svém Awake.
        if (npcClearTile.HasValue && npcClearTile.Value.x == x && npcClearTile.Value.y == y)
            StripTileDecor(newTile);

        // Rybí dlaždice: zruš vlastní pohupování — sedí napevno v hladině.
        if ((TileType)status.type == TileType.Water_Fish)
        {
            var wave = newTile.GetComponent<WaterWave>();
            if (wave != null) Destroy(wave);
        }

        // Mlha: zapnutá, dokud políčko není prozkoumané.
        Transform fog = newTile.transform.Find("FogVisual");
        if (fog != null)
        {
            fog.gameObject.layer = fogLayer;
            fog.gameObject.SetActive(!status.isExplored);
        }

        // Ikona do minimapy. Prefaby ji mají, obchody ne → tomu ji dodělej.
        Transform icon = newTile.transform.Find("MapIcon");
        if (icon == null)
            icon = CreateShopMapIcon(newTile, (TileType)status.type);

        if (icon != null && minimapLayer >= 0)
            icon.gameObject.layer = minimapLayer;

        // Otevřená bedna: pokud ji už někdo vybral, odklop víko (objekt "Lid" v prefabu).
        if ((TileType)status.type == TileType.Chest
            && ChestManager.Instance != null && ChestManager.Instance.IsOpened(x, y))
        {
            foreach (Transform t in newTile.GetComponentsInChildren<Transform>(true))
                if (t.name == "Lid") { t.localRotation = Quaternion.Euler(-105f, 0f, 0f); break; }
        }

        // Souš → zajisti hladký terénní mesh celého ostrova.
        if (IsMeshLandTile((TileType)status.type))
            EnsureIslandTerrain(x, y);

        // Maják zabírá 2×2 políčka. Věž ("Tower") se ukáže jen na levém dolním
        // rohu bloku a přesune se doprostřed 2×2 + zvětší; ostatní 3 dlaždice
        // ukážou jen písčitý podklad.
        if ((TileType)status.type == TileType.Lighthouse)
        {
            Transform tower = newTile.transform.Find("Tower");
            if (tower != null)
            {
                bool anchor = GetTileType(x + 1, y) == TileType.Lighthouse
                           && GetTileType(x, y + 1) == TileType.Lighthouse
                           && GetTileType(x + 1, y + 1) == TileType.Lighthouse;
                if (anchor)
                {
                    tower.localPosition += new Vector3(0.5f, 0f, 0.5f);
                    tower.localScale    *= 3.2f; // maják má být na mapě pořádně vidět (cca 2× víc než dřív)

                    // Nepřátelský ostrov → červená vlajka na špičce majáku (vidíš to
                    // dřív, než po tobě dělo začne pálit).
                    if (IsHostileIslandNear(x, y)) AddHostileFlag(newTile, tower);
                }
                else
                {
                    tower.gameObject.SetActive(false);
                }
            }
        }
    }

    // Je poblíž [x,y] kotva nepřátelského ostrova, kterému hráč ještě nezničil dělo?
    private bool IsHostileIslandNear(int x, int y)
    {
        foreach (string k in gameData.hostileIslands)
        {
            if (gameData.clearedIslands.Contains(k)) continue;
            var t = KeyToTile(k);
            if (Mathf.Abs(t.x - x) <= 16 && Mathf.Abs(t.y - y) <= 16) return true;
        }
        return false;
    }

    // Červená vlajka na stožáru NAD špičkou majáku. Věž bývá různě vysoká
    // (prefab × škálování), tak si výšku vršku změříme z jejích rendererů.
    private void AddHostileFlag(GameObject tile, Transform tower)
    {
        float topLocalY = 6.6f; // fallback, kdyby se věž nedala změřit
        if (tower != null)
        {
            float maxY = float.MinValue;
            foreach (var r in tower.GetComponentsInChildren<Renderer>(true))
                if (r.bounds.max.y > maxY) maxY = r.bounds.max.y;
            if (maxY > float.MinValue) topLocalY = maxY - tile.transform.position.y;
        }

        var pole = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(pole.GetComponent<Collider>());
        pole.name = "HostileFlagPole";
        pole.transform.SetParent(tile.transform, false);
        pole.transform.localPosition = new Vector3(0.5f, topLocalY + 0.75f, 0.5f);
        pole.transform.localScale    = new Vector3(0.09f, 1.6f, 0.09f);
        TintPrimitive(pole, new Color(0.12f, 0.08f, 0.06f));

        var flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(flag.GetComponent<Collider>());
        flag.name = "HostileFlag";
        flag.transform.SetParent(tile.transform, false);
        flag.transform.localPosition = new Vector3(0.5f + 0.46f, topLocalY + 1.2f, 0.5f);
        flag.transform.localScale    = new Vector3(0.85f, 0.52f, 0.04f);
        TintPrimitive(flag, new Color(0.86f, 0.09f, 0.07f));
    }

    private static void TintPrimitive(GameObject go, Color c)
    {
        var r = go.GetComponent<Renderer>();
        if (r == null) return;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = sh != null ? new Material(sh) : r.material;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
        r.material = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>
    /// Zamluví políčko [x,y] jako "bez dekorace" — volá StoryNpc, když se děda
    /// usadí. Když ta dlaždice právě existuje ve scéně, dekoraci z ní hned sundá.
    /// </summary>
    public void ReserveNpcTile(int x, int y)
    {
        npcClearTile = new Vector2Int(x, y);

        // Zapiš "bez dekorace" i do dat dlaždice, ať IslandDecor při příštím
        // vygenerování ostrova na dědovo políčko nic nepoloží.
        string key = GridKey(x, y);
        if (gameData != null && gameData.tileData.TryGetValue(key, out var st))
            st.decor = 1;

        if (activeTiles.TryGetValue(key, out GameObject tile))
            StripTileDecor(tile);
    }

    // Sundá z dlaždice všechnu ozdobu: vestavěné děti "Decor_*" vypne,
    // doplněné Kenney "DecorExtra" zničí.
    private static void StripTileDecor(GameObject tile)
    {
        foreach (Transform child in tile.transform)
        {
            if (child.name.StartsWith("Decor_")) child.gameObject.SetActive(false);
            else if (child.name == "DecorExtra") Destroy(child.gameObject);
        }
    }

    // Políčka, pod která patří hladký terénní mesh ostrova (ne molo — to je nad vodou).
    private static bool IsMeshLandTile(TileType t)
        => t == TileType.Harbor || t == TileType.Lighthouse || t == TileType.Chest
        || t == TileType.MegaIsland;

    // Zajistí, že ostrov obsahující políčko [x,y] má vygenerovaný hladký terén.
    private void EnsureIslandTerrain(int x, int y)
    {
        if (islandTilesWithTerrain.Contains(GridKey(x, y))) return;

        // Flood-fill spojité souše z tileData.
        var land   = new HashSet<Vector2Int>();
        var keys   = new List<string>();
        var stack  = new Stack<Vector2Int>();
        stack.Push(new Vector2Int(x, y));
        int minX = x, minY = y;

        while (stack.Count > 0)
        {
            var p = stack.Pop();
            if (land.Contains(p)) continue;
            if (!IsMeshLandTile(GetTileType(p.x, p.y))) continue;

            land.Add(p);
            keys.Add(GridKey(p.x, p.y));
            if (p.x < minX) minX = p.x;
            if (p.y < minY) minY = p.y;

            stack.Push(new Vector2Int(p.x + 1, p.y));
            stack.Push(new Vector2Int(p.x - 1, p.y));
            stack.Push(new Vector2Int(p.x, p.y + 1));
            stack.Push(new Vector2Int(p.x, p.y - 1));
        }
        if (land.Count == 0) return;

        foreach (string k in keys) islandTilesWithTerrain.Add(k);

        string islandKey = minX + "," + minY;
        if (islandTerrains.ContainsKey(islandKey)) return;

        var go = new GameObject("IslandTerrain " + islandKey);
        go.transform.SetParent(transform);
        go.transform.position = new Vector3(0f, -0.1f, 0f); // stejná rovina jako dlaždice
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mf.sharedMesh      = IslandTerrain.Build(land);
        mr.sharedMaterial  = islandTerrainMaterial;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        // Travnatý povrch navrch písku (jen vnitřek ostrova). Stejný materiál
        // jako písek, jen obarvený do zelena → sedí do světla scény.
        var grass = new GameObject("IslandGrass " + islandKey);
        grass.transform.SetParent(go.transform, false);
        var gmf = grass.AddComponent<MeshFilter>();
        var gmr = grass.AddComponent<MeshRenderer>();
        gmf.sharedMesh       = IslandTerrain.BuildGrass(land);
        gmr.sharedMaterial   = IslandGrassMaterial();
        gmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

        islandTerrains[islandKey] = new IslandRec { go = go, tileKeys = keys };
    }

    // Zelený materiál pro trávu — odvozený jednou z pískového materiálu, ať má
    // stejný shader a reaguje na světlo scény stejně jako zbytek ostrova.
    private Material grassMaterialCache;
    private Material IslandGrassMaterial()
    {
        if (grassMaterialCache != null) return grassMaterialCache;

        grassMaterialCache = islandTerrainMaterial != null
            ? new Material(islandTerrainMaterial)
            : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        grassMaterialCache.name = "IslandGrass (runtime)";

        Color green = new Color(0.36f, 0.55f, 0.28f);
        if (grassMaterialCache.HasProperty("_BaseColor")) grassMaterialCache.SetColor("_BaseColor", green);
        if (grassMaterialCache.HasProperty("_Color"))     grassMaterialCache.SetColor("_Color", green);
        return grassMaterialCache;
    }

    // Smaže terénní meshe ostrovů, ze kterých už nezůstalo žádné aktivní políčko.
    private void CleanupIslandTerrains()
    {
        int keep = ACTIVE_GRID_SIZE + 4;
        var dead = new List<string>();
        foreach (var kv in islandTerrains)
        {
            bool anyActive = false;
            foreach (string tk in kv.Value.tileKeys)
            {
                // Dlaždice buď má 3D objekt, nebo je aspoň blízko hráče (mega
                // ostrov nemá per-dlaždicové objekty, tak se ptáme na souřadnice).
                if (activeTiles.ContainsKey(tk)) { anyActive = true; break; }
                var (tx, ty) = ParseGridKey(tk);
                bool nearP1 = Mathf.Abs(tx - gameData.playerGridX) <= keep && Mathf.Abs(ty - gameData.playerGridY) <= keep;
                bool nearP2 = MultiplayerManager.IsMultiplayer
                           && Mathf.Abs(tx - gameData.player2GridX) <= keep && Mathf.Abs(ty - gameData.player2GridY) <= keep;
                if (nearP1 || nearP2) { anyActive = true; break; }
            }

            if (!anyActive)
            {
                Destroy(kv.Value.go);
                foreach (string tk in kv.Value.tileKeys) islandTilesWithTerrain.Remove(tk);
                dead.Add(kv.Key);
            }
        }
        foreach (string k in dead) islandTerrains.Remove(k);
    }

    // Nasbírá souřadnice políček s vrakem (Treasure) v okolí středu. Používá
    // SeaFloor, aby pod vraky přírodně zvedl dno (mělčina), místo umělé kupky.
    public void CollectTreasureTilesNear(int cx, int cz, int radius, List<Vector2Int> outList)
    {
        outList.Clear();
        for (int x = cx - radius; x <= cx + radius; x++)
            for (int y = cz - radius; y <= cz + radius; y++)
                if (GetTileType(x, y) == TileType.Treasure)
                    outList.Add(new Vector2Int(x, y));
    }

    // Nasbírá souřadnice pevninových políček ostrova (souš / maják / bedna) v
    // okolí středu. Používá SeaFloor, aby kolem ostrova zvedl dno až k jeho
    // úpatí — ostrov pak "vyrůstá ze dna" a nekončí pod vodou uříznutý.
    public void CollectIslandTilesNear(int cx, int cz, int radius, List<Vector2Int> outList)
    {
        outList.Clear();
        for (int x = cx - radius; x <= cx + radius; x++)
            for (int y = cz - radius; y <= cz + radius; y++)
                if (IsMeshLandTile(GetTileType(x, y)))
                    outList.Add(new Vector2Int(x, y));
    }

    // Vytvoří barevnou ikonku budovy (čtvereček nad ní) jen pro minimapu.
    private Transform CreateShopMapIcon(GameObject tile, TileType type)
    {
        if (type != TileType.UpgradeShop && type != TileType.QuestShop && type != TileType.Lighthouse)
            return null;

        Color iconColor = type == TileType.UpgradeShop ? new Color(1f, 0.8f, 0f)     // zlatá   = upgrade shop
                        : type == TileType.QuestShop   ? new Color(0.2f, 0.8f, 1f)   // azurová = quest shop
                        :                                new Color(1f, 0.25f, 0.2f); // červená = maják

        GameObject iconGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(iconGO.GetComponent<MeshCollider>()); // kolizi nechceme
        iconGO.name = "MapIcon";
        iconGO.transform.SetParent(tile.transform);
        iconGO.transform.localPosition = new Vector3(0.5f, 2f, 0.5f);
        iconGO.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // otoč plochou nahoru
        iconGO.transform.localScale    = new Vector3(1.5f, 1.5f, 1f);

        Renderer r = iconGO.GetComponent<Renderer>();
        if (r != null)
        {
            // Zkus URP shader, pak starší varianty. _BaseColor = URP, _Color = built-in.
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Standard");
            if (sh != null)
            {
                var mat = new Material(sh);
                mat.SetColor("_BaseColor", iconColor);
                mat.SetColor("_Color", iconColor);
                r.material = mat;
            }
            else
            {
                r.material.SetColor("_BaseColor", iconColor);
                r.material.SetColor("_Color", iconColor);
            }
        }

        return iconGO.transform;
    }

    // Smaže 3D objekty políček, která jsou daleko od (obou) hráčů.
    private void ClearOldTiles()
    {
        var keysToRemove = new List<string>();
        int dist = ACTIVE_GRID_SIZE + 2;

        foreach (var tile in activeTiles)
        {
            var (x, y) = ParseGridKey(tile.Key);

            bool nearP1 = Mathf.Abs(x - gameData.playerGridX) <= dist
                       && Mathf.Abs(y - gameData.playerGridY) <= dist;
            bool nearP2 = MultiplayerManager.IsMultiplayer
                       && Mathf.Abs(x - gameData.player2GridX) <= dist
                       && Mathf.Abs(y - gameData.player2GridY) <= dist;

            if (!nearP1 && !nearP2) keysToRemove.Add(tile.Key);
        }

        foreach (string k in keysToRemove)
        {
            Destroy(activeTiles[k]);
            activeTiles.Remove(k);
        }

        CleanupIslandTerrains();
    }

    // ── Dotazy na políčka ──────────────────────────────────────────────────

    /// <summary>Typ políčka (nevygenerované bere jako vodu).</summary>
    public TileType GetTileType(int x, int y)
    {
        string key = GridKey(x, y);
        return gameData.tileData.ContainsKey(key) ? (TileType)gameData.tileData[key].type : TileType.Water;
    }

    /// <summary>Celý stav políčka, nebo null když neexistuje.</summary>
    public TileStatus GetTileStatus(int x, int y)
    {
        string key = GridKey(x, y);
        return gameData.tileData.ContainsKey(key) ? gameData.tileData[key] : null;
    }

    /// <summary>Ručně vyvolá OnWorldChanged (překreslí HUD a minimapu).</summary>
    public void NotifyWorldChanged() => OnWorldChanged?.Invoke();

    // ── Nepřátelské ostrovy (soubojový systém) ─────────────────────────────
    /// <summary>Klíče nepřátelských ostrovů, kterým hráč ještě NEzničil dělo.</summary>
    public List<string> ActiveHostileIslandKeys()
    {
        var result = new List<string>();
        foreach (string k in gameData.hostileIslands)
            if (!gameData.clearedIslands.Contains(k)) result.Add(k);
        return result;
    }

    /// <summary>Zapamatuje si, že hráč zničil dělo tohoto nepřátelského ostrova.</summary>
    public void MarkIslandCleared(string key)
    {
        if (!gameData.clearedIslands.Contains(key)) gameData.clearedIslands.Add(key);
    }

    /// <summary>"x,y" klíč rozloží na souřadnice.</summary>
    public static Vector2Int KeyToTile(string key)
    {
        var p = key.Split(',');
        return new Vector2Int(int.Parse(p[0]), int.Parse(p[1]));
    }

    /// <summary>Najde pevninovou dlaždici u vody poblíž středu ostrova (kam dát dělo).</summary>
    public bool TryGetHostileCannonSpot(Vector2Int center, out Vector2Int spot)
    {
        var spots = GetHostileCannonSpots(center, 1);
        spot = spots.Count > 0 ? spots[0] : default;
        return spots.Count > 0;
    }

    /// <summary>
    /// Až `maxCount` pevninových dlaždic u kraje ostrova (soused = voda), rozmístěných
    /// od sebe — kam nepřátelský ostrov dá děla. Nejblíž ke středu jako první.
    /// </summary>
    public List<Vector2Int> GetHostileCannonSpots(Vector2Int center, int maxCount)
    {
        var cand = new List<Vector2Int>();
        for (int x = center.x - 13; x <= center.x + 13; x++)
            for (int y = center.y - 13; y <= center.y + 13; y++)
            {
                if (GetTileType(x, y) != TileType.Harbor) continue;
                if (GetTileType(x + 1, y) != TileType.Water && GetTileType(x - 1, y) != TileType.Water
                 && GetTileType(x, y + 1) != TileType.Water && GetTileType(x, y - 1) != TileType.Water) continue;
                cand.Add(new Vector2Int(x, y));
            }

        cand.Sort((a, b) =>
            ((a.x - center.x) * (a.x - center.x) + (a.y - center.y) * (a.y - center.y))
          - ((b.x - center.x) * (b.x - center.x) + (b.y - center.y) * (b.y - center.y)));

        var pick = new List<Vector2Int>();
        foreach (var c in cand)
        {
            bool tooClose = false;
            foreach (var p in pick)
                if (Mathf.Abs(p.x - c.x) < 4 && Mathf.Abs(p.y - c.y) < 4) { tooClose = true; break; }
            if (tooClose) continue;
            pick.Add(c);
            if (pick.Count >= maxCount) break;
        }
        return pick;
    }

    /// <summary>
    /// Až `count` vodních dlaždic v pásu ~6–14 políček kolem středu ostrova —
    /// kam se postaví hlídkující pirátské lodě nepřátelského ostrova.
    /// </summary>
    public List<Vector2Int> GetGuardWaterSpots(Vector2Int center, int count)
    {
        var result = new List<Vector2Int>();
        if (count <= 0) return result;

        // Zkus rovnoměrně po kruhu (8 směrů), poloměr 10, s pár záložními poloměry.
        int[] radii = { 10, 8, 12, 7, 13 };
        for (int i = 0; i < count; i++)
        {
            float ang = (i / (float)count) * Mathf.PI * 2f + 0.4f;
            foreach (int r in radii)
            {
                int x = center.x + Mathf.RoundToInt(Mathf.Cos(ang) * r);
                int y = center.y + Mathf.RoundToInt(Mathf.Sin(ang) * r);
                var t = GetTileType(x, y);
                if (t != TileType.Water && t != TileType.Water_Fish) continue;
                result.Add(new Vector2Int(x, y));
                break;
            }
        }
        return result;
    }

    // ── Mapa (šipka k nejbližšímu ostrovu) + respawn po smrti ──────────────
    /// <summary>Nejbližší dlaždice pevniny (Harbor) k bodu — pro šipku "mapy" na minimapě.</summary>
    public Vector2Int? NearestHarborTile(int fromX, int fromY) => NearestTileOfType(fromX, fromY, TileType.Harbor);

    /// <summary>Nejbližší molo (Pier) k bodu — pro přemístění lodě po opravě.</summary>
    public Vector2Int? NearestPierTile(int fromX, int fromY) => NearestTileOfType(fromX, fromY, TileType.Pier);

    /// <summary>Nejbližší vygenerovaná dlaždice daného typu k bodu (nebo null). Pro konzoli "locate".</summary>
    public Vector2Int? NearestTileOfType(int fromX, int fromY, TileType type)
    {
        Vector2Int best = default;
        bool found = false;
        long bestSq = long.MaxValue;

        foreach (var kv in gameData.tileData)
        {
            if (kv.Value.type != (int)type) continue;
            var (x, y) = ParseGridKey(kv.Key);
            long sq = (long)(x - fromX) * (x - fromX) + (long)(y - fromY) * (y - fromY);
            if (sq < bestSq) { bestSq = sq; best = new Vector2Int(x, y); found = true; }
        }
        return found ? best : (Vector2Int?)null;
    }

    // Vynutí vznik ostrova poblíž bodu (ignoruje náhodu) — pro respawn po smrti,
    // kdyby v okolí žádný ostrov nebyl.
    private void ForceIslandNear(int cx, int cy)
    {
        int bx = Mathf.RoundToInt(cx / 40f) * 40;
        int by = Mathf.RoundToInt(cy / 40f) * 40;

        for (int ring = 0; ring <= 12; ring++)
            for (int dx = -ring; dx <= ring; dx++)
                for (int dy = -ring; dy <= ring; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != ring) continue;
                    int gx = bx + dx * 40, gy = by + dy * 40;
                    if (CanPlaceIsland(gx, gy)) { GenerateIsland(gx, gy); return; }
                }
    }

    /// <summary>
    /// Vynutí vznik NEPŘÁTELSKÉHO ostrova poblíž bodu — pro konzoli "locate pirate",
    /// když hráč žádný neobjevil. Ostrov není přímo pod hráčem (prstenec 2–14 × 40
    /// políček), zapamatuje se jako "vynucený" (přežije úklid dlaždic) a je vždy
    /// nepřátelský. Vrací klíč "x,y" (kotva ostrova), nebo null když se nikam nevešel.
    /// </summary>
    public string ForceHostileIslandNear(int cx, int cy)
    {
        int bx = Mathf.RoundToInt(cx / 40f) * 40;
        int by = Mathf.RoundToInt(cy / 40f) * 40;

        for (int ring = 2; ring <= 14; ring++)
            for (int dx = -ring; dx <= ring; dx++)
                for (int dy = -ring; dy <= ring; dy++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != ring) continue;
                    int gx = bx + dx * 40, gy = by + dy * 40;
                    if (!CanPlaceIsland(gx, gy)) continue;

                    string key = GridKey(gx, gy);
                    if (!gameData.forcedIslandKeys.Contains(key)) gameData.forcedIslandKeys.Add(key);
                    if (!gameData.hostileIslands.Contains(key))   gameData.hostileIslands.Add(key);
                    gameData.clearedIslands.Remove(key);

                    GenerateIsland(gx, gy);
                    Save();
                    NotifyWorldChanged();
                    return key;
                }
        return null;
    }

    // ── Příběhový mega ostrov ─────────────────────────────────────────────
    /// <summary>
    /// Vygeneruje příběhový "mega ostrov" — velkou plochu pevniny (typ MegaIsland)
    /// se středem uprostřed. Zatím je to jen země + obelisk uprostřed; obsah se
    /// bude přidávat později. Volá StoryNpc, když starý námořník dá souřadnice.
    /// </summary>
    public void PlaceMegaIsland(int centerX, int centerY)
    {
        const int R = 13; // poloměr plochy (→ ~26×26 políček)

        // Organický kruhový blok — kruh + trochu šumu na okraji, ať to není přesný kruh.
        for (int dx = -R - 2; dx <= R + 2; dx++)
            for (int dy = -R - 2; dy <= R + 2; dy++)
            {
                float edge = R + (Mathf.PerlinNoise((centerX + dx) * 0.18f, (centerY + dy) * 0.18f) - 0.5f) * 5f;
                if (dx * dx + dy * dy > edge * edge) continue;

                string key = GridKey(centerX + dx, centerY + dy);
                bool wasExplored = gameData.tileData.ContainsKey(key) && gameData.tileData[key].isExplored;
                gameData.tileData[key] = new TileStatus((int)TileType.MegaIsland) { isExplored = wasExplored };

                // Když už tam byla vygenerovaná dlaždice, zahoď její 3D objekt.
                if (activeTiles.TryGetValue(key, out GameObject old)) { Destroy(old); activeTiles.Remove(key); }
            }

        // Molo na kraji přivráceném ke světu (odkud hráč připluje) — 2 dlaždice,
        // ať se dá u mega ostrova zakotvit a vylodit.
        Vector2 toWorld = new Vector2(-centerX, -centerY);
        if (toWorld.sqrMagnitude < 1f) toWorld = Vector2.down;
        toWorld.Normalize();
        for (int step = R + 1; step > 2; step--)
        {
            int ex = centerX + Mathf.RoundToInt(toWorld.x * step);
            int ey = centerY + Mathf.RoundToInt(toWorld.y * step);
            string ek = GridKey(ex, ey);
            if (gameData.tileData.TryGetValue(ek, out TileStatus es) && es.type == (int)TileType.MegaIsland)
            {
                gameData.tileData[ek] = new TileStatus((int)TileType.Pier) { isExplored = es.isExplored };
                int fx = centerX + Mathf.RoundToInt(toWorld.x * (step + 1));
                int fy = centerY + Mathf.RoundToInt(toWorld.y * (step + 1));
                gameData.tileData[GridKey(fx, fy)] = new TileStatus((int)TileType.Pier);
                break;
            }
        }

        gameData.storyIslandActive = true;
        gameData.storyIslandX      = centerX;
        gameData.storyIslandY      = centerY;

        // Obelisk uprostřed (marker + hák pro budoucí obsah).
        var markerGo = new GameObject("MegaIslandMarker");
        markerGo.transform.SetParent(transform);
        markerGo.transform.position = new Vector3(centerX, 0f, centerY);
        markerGo.AddComponent<MegaIslandMarker>();

        Save();
        NotifyWorldChanged();
    }

    /// <summary>
    /// Respawn po smrti: přesune hráče PĚŠKY na nejbližší ostrov (loď = veslice
    /// zaparkovaná ve vodě u mola), obnoví zdraví. Kořist (ryby, poklady, náboje,
    /// vylepšení, mapa) se ZTRATÍ, mince a rozdělaný mega quest zůstanou.
    /// </summary>
    public void RespawnPlayerAtNearestIsland(int playerIndex)
    {
        var d = gameData;
        int fromX = playerIndex == 0 ? d.playerGridX : d.player2GridX;
        int fromY = playerIndex == 0 ? d.playerGridY : d.player2GridY;

        Vector2Int? harbor = NearestHarborTile(fromX, fromY);
        if (harbor == null) { ForceIslandNear(fromX, fromY); harbor = NearestHarborTile(fromX, fromY); }

        Vector2Int spot   = harbor ?? new Vector2Int(fromX, fromY);
        var        water  = FindWaterNextTo(spot.x, spot.y);
        Vector2Int boatAt = water != null ? new Vector2Int(water.Value.x, water.Value.y) : spot;

        // ── Vynuluj kořist + vylepšení (mince a mega quest zůstávají) ──────
        if (playerIndex == 0)
        {
            d.fishCount = 0; d.treasureCount = 0; d.ammo = 0;
            d.hasSpeedUpgrade = d.hasRodUpgrade = d.hasMiningUpgrade = false;
            d.hasMap = false; d.sellBonus = false;
            d.shipLevel = 0;
            d.activeQuest.Reset();
            d.boatHealth = 100; d.playerHealth = 100; d.boatWrecked = false; d.boatNeedsRehome = false;
            d.isOnFoot   = true;
            d.playerGridX = spot.x; d.playerGridY = spot.y;
            d.boatGridX   = boatAt.x; d.boatGridY = boatAt.y;
        }
        else
        {
            d.player2FishCount = 0; d.player2TreasureCount = 0; d.player2Ammo = 0;
            d.player2HasSpeedUpgrade = d.player2HasRodUpgrade = d.player2HasMiningUpgrade = false;
            d.player2HasMap = false; d.player2SellBonus = false;
            d.player2ShipLevel = 0;
            d.player2ActiveQuest.Reset();
            d.player2BoatHealth = 100; d.player2PlayerHealth = 100;
            d.player2BoatWrecked = false; d.player2BoatNeedsRehome = false;
            d.player2GridX = spot.x; d.player2GridY = spot.y;
        }

        GenerateWorld(spot.x, spot.y);
        MarkAreaExplored(spot.x, spot.y, 3);
        Save();
        OnWorldChanged?.Invoke();
    }

    /// <summary>
    /// Políčka pevniny (Harbor) startovního ostrova — toho u počátku [0,0].
    /// Používá příběhové NPC (děda), aby se objevilo jen na základním ostrově.
    /// Vrací prázdný seznam, když ještě žádná pevnina není.
    /// </summary>
    public List<Vector2Int> GetStartIslandHarborTiles()
    {
        // 1) Najdi pevninové políčko nejblíž počátku (to je startovní ostrov).
        Vector2Int seed = default;
        bool found = false;
        long best = long.MaxValue;
        foreach (var kv in gameData.tileData)
        {
            if (!IsMeshLandTile((TileType)kv.Value.type)) continue;
            var (x, y) = ParseGridKey(kv.Key);
            long d = (long)x * x + (long)y * y;
            if (d < best) { best = d; seed = new Vector2Int(x, y); found = true; }
        }

        var result = new List<Vector2Int>();
        if (!found) return result;

        // 2) Flood-fill spojité pevniny z tohoto políčka, seber jen Harbor dlaždice.
        var seen  = new HashSet<Vector2Int>();
        var stack = new Stack<Vector2Int>();
        stack.Push(seed);
        seen.Add(seed);
        var dirs = new Vector2Int[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        while (stack.Count > 0)
        {
            var c = stack.Pop();
            if (!IsMeshLandTile(GetTileType(c.x, c.y))) continue;

            if (GetTileType(c.x, c.y) == TileType.Harbor) result.Add(c);

            foreach (var d in dirs)
            {
                var n = c + d;
                if (seen.Add(n)) stack.Push(n);
            }
        }
        return result;
    }

    // ── Startovní ostrov (úplně nová hra) ──────────────────────────────────
    private void GenerateInitialWorld()
    {
        // Stejný organický generátor jako pro ostatní ostrovy, jen kolem počátku
        // a rovnou prozkoumaný.
        var land = StampOrganicLand(0, 0, explored: true);
        PlaceEdgePier(land);
        PlaceLighthouse(land);
        // Na startovním ostrově ZÁMĚRNĚ není bedna s mega questem (naváže se na příběh).
        AssignIslandDecor(land); // vygeneruj dekoraci jednou a ulož ji do dlaždic

        // Loď zaparkuj do VODY hned vedle prvního mola, hráče postav PĚŠKY na
        // pevninu vedle mola. (Nová hra = probudíš se jako panáček na ostrově,
        // loď se ti houpe u mola.)
        foreach (var kv in gameData.tileData)
        {
            if (kv.Value.type != (int)TileType.Pier) continue;
            var (px, py) = ParseGridKey(kv.Key);

            var water = FindWaterNextTo(px, py);
            gameData.boatGridX = water != null ? water.Value.x : px;
            gameData.boatGridY = water != null ? water.Value.y : py;

            var foot = FindHarborNextTo(px, py);
            if (foot != null)
            {
                gameData.playerGridX = foot.Value.x;
                gameData.playerGridY = foot.Value.y;
                gameData.isOnFoot    = true;
            }
            else
            {
                // pojistka: kdyby vedle mola nebyla pevnina, nech hráče na molu v lodi
                gameData.playerGridX = px;
                gameData.playerGridY = py;
            }
            break;
        }

        MarkAreaExplored(gameData.playerGridX, gameData.playerGridY, ISLAND_CANVAS);
    }

    // Najde políčko pevniny (Harbor) hned vedle [x,y]. Null, když žádné není.
    private (int x, int y)? FindHarborNextTo(int x, int y)
    {
        var dirs = new (int dx, int dy)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        foreach (var d in dirs)
            if (IsHarborTile(x + d.dx, y + d.dy))
                return (x + d.dx, y + d.dy);
        return null;
    }

    // Najde vodní políčko hned vedle [x,y] (pro zaparkování lodě u mola).
    private (int x, int y)? FindWaterNextTo(int x, int y)
    {
        var dirs = new (int dx, int dy)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        foreach (var d in dirs)
        {
            TileType t = GetTileType(x + d.dx, y + d.dy);
            if (t == TileType.Water || t == TileType.Water_Fish || t == TileType.Treasure)
                return (x + d.dx, y + d.dy);
        }
        return null;
    }

    // Prefab pro daný typ políčka (obchody padají zpět na harborPrefab, když nejsou nastavené).
    private GameObject GetPrefabForType(TileType t)
    {
        switch (t)
        {
            case TileType.Water:       return null; // kreslí ji velká vodní plocha (OceanSurface), ne dlaždice
            case TileType.Water_Fish:  return waterFishPrefab;
            case TileType.Treasure:    return treasurePrefab;
            case TileType.Harbor:      return harborPrefab;
            case TileType.Pier:        return pierPrefab;
            case TileType.UpgradeShop: return upgradeShopPrefab != null ? upgradeShopPrefab : harborPrefab;
            case TileType.QuestShop:   return questShopPrefab   != null ? questShopPrefab   : harborPrefab;
            case TileType.Lighthouse:  return lighthousePrefab  != null ? lighthousePrefab  : harborPrefab;
            case TileType.Chest:       return chestPrefab       != null ? chestPrefab       : harborPrefab;
            default:                   return null;
        }
    }

    /// <summary>
    /// Změní typ existujícího políčka (např. políčko s rybami → obyčejná voda,
    /// když se ryby vyloví) a hned mu vymění 3D objekt.
    /// </summary>
    public void SetTileType(int x, int y, TileType newType)
    {
        string key = GridKey(x, y);
        if (!gameData.tileData.ContainsKey(key)) return;

        gameData.tileData[key].type = (int)newType;

        if (activeTiles.ContainsKey(key))
        {
            Destroy(activeTiles[key]);
            activeTiles.Remove(key);
            InstantiateTile(x, y, gameData.tileData[key]);
        }

        OnWorldChanged?.Invoke();
    }

    // ── Nová hra / načtení slotu ───────────────────────────────────────────

    /// <summary>Nová hra ve stávajícím slotu (volá pauza).</summary>
    public void NewGameReset()
    {
        SaveManager.DeleteSave();
        GameSession.Ensure().SetData(new GameData());
        gameData.shipLevel = 0;

        DestroyAllActiveTiles();

        GenerateInitialWorld();
        GenerateWorld(0, 0);
        Save();
        OnWorldChanged?.Invoke();
    }

    /// <summary>Načte existující save v daném slotu (volá hlavní menu).</summary>
    public void LoadSlot(int slot)
    {
        SaveManager.CurrentSlot = slot;
        PlayerPrefs.SetInt("LastSlot", slot);

        DestroyAllActiveTiles();

        GameSession.Ensure().SetData(SaveManager.LoadGame());
        if (gameData.tileData.Count == 0) GenerateInitialWorld();
        GenerateWorld(gameData.playerGridX, gameData.playerGridY);
        Save();
        OnWorldChanged?.Invoke();
    }

    /// <summary>Spustí novou hru v daném slotu (volá hlavní menu).</summary>
    public void NewGameSlot(int slot)
    {
        SaveManager.CurrentSlot = slot;
        PlayerPrefs.SetInt("LastSlot", slot);
        SaveManager.DeleteSave();

        DestroyAllActiveTiles();

        GameSession.Ensure().SetData(new GameData());
        gameData.shipLevel = 0;
        GenerateInitialWorld();
        GenerateWorld(0, 0);
        Save();
        OnWorldChanged?.Invoke();
    }

    // Zničí všechny existující 3D objekty políček (při načtení / nové hře).
    private void DestroyAllActiveTiles()
    {
        foreach (var kv in activeTiles) Destroy(kv.Value);
        activeTiles.Clear();

        foreach (var kv in islandTerrains) Destroy(kv.Value.go);
        islandTerrains.Clear();
        islandTilesWithTerrain.Clear();
    }
}
