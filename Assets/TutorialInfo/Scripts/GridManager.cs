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
    public const int ACTIVE_GRID_SIZE = 15;

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

    // Veškerý stav hry. Fyzicky ho drží GameSession (přežívá i přechod do
    // scény majáku), GridManager k němu jen přistupuje přes tuhle zkratku.
    public GameData gameData => GameSession.Instance != null ? GameSession.Instance.Data : null;

    // Právě existující 3D objekty políček. Klíč "x,y" → objekt ve scéně.
    private Dictionary<string, GameObject> activeTiles = new Dictionary<string, GameObject>();

    /// <summary>Vyvolá se po každé změně světa (pohyb, těžba, nákup...). Poslouchá HUD a minimapa.</summary>
    public event Action OnWorldChanged;

    private int fogLayer;     // vrstva "Fog" (mlha se nekreslí do minimapy)
    private int minimapLayer; // vrstva "MinimapOnly" (ikony jen pro minimapu)

    // Parametry generování ostrovů.
    private const int ISLAND_SIZE         = 10; // "jmenovitá" velikost (kompatibilita se starým kódem)
    private const int ISLAND_CANVAS       = 14; // max rozměr organického ostrova (plátno, do kterého se vejde)
    private const int ISLAND_PADDING      = 1;  // volné pole kolem ostrova při kontrole místa
    private const int MIN_ISLAND_DISTANCE = 50; // minimální rozestup mezi ostrovy
    private const int CLEANUP_LIMIT       = 100;// políčka dál než tohle se ze save mažou

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

        GenerateWorld(gameData.playerGridX, gameData.playerGridY);
        OnWorldChanged?.Invoke();
    }

    // Při zavření hry ulož.
    void OnApplicationQuit() => Save();

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
            }
        }
    }

    // Rozhodne, co na daném (zatím prázdném) políčku vznikne: ostrov nebo moře.
    private void CheckAndGenerateArea(int x, int y)
    {
        // Ostrovy vznikají jen na mřížce každých 20 políček, s 10% pravděpodobností,
        // a jen když je kolem dost místa.
        if (x % 20 == 0 && y % 20 == 0 && UnityEngine.Random.value < 0.1f && CanPlaceIsland(x, y))
        {
            GenerateIsland(x, y);
            return;
        }

        // Jinak obyčejné mořské políčko (většinou voda, občas ryby / poklad).
        string key = GridKey(x, y);
        if (!gameData.tileData.ContainsKey(key))
        {
            TileType seaType = GenerateRandomSeaType();
            var status = new TileStatus((int)seaType);
            if (seaType == TileType.Water_Fish) status.fishRemaining = 3;
            gameData.tileData.Add(key, status);
        }
    }

    // Je kolem [startX,startY] volné místo na nový ostrov?
    private bool CanPlaceIsland(int startX, int startY)
    {
        if (IsAnotherIslandTooClose(startX, startY)) return false;

        int fromX = startX - ISLAND_PADDING;
        int toX   = startX + ISLAND_CANVAS - 1 + ISLAND_PADDING;
        int fromY = startY - ISLAND_PADDING;
        int toY   = startY + ISLAND_CANVAS - 1 + ISLAND_PADDING;

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

    // Je poblíž střed jiného ostrova (blíž než MIN_ISLAND_DISTANCE)?
    private bool IsAnotherIslandTooClose(int startX, int startY)
    {
        float cx = startX + (ISLAND_CANVAS - 1) * 0.5f;
        float cy = startY + (ISLAND_CANVAS - 1) * 0.5f;
        int   max = MIN_ISLAND_DISTANCE + ISLAND_CANVAS;

        foreach (var kv in gameData.tileData)
        {
            if (!IsIslandTile(kv.Value.type)) continue;

            var (x, y) = ParseGridKey(kv.Key);
            if (Mathf.Abs(x - cx) > max || Mathf.Abs(y - cy) > max) continue; // hrubý rychlý test

            float dx = x - cx, dy = y - cy;
            if (dx * dx + dy * dy < MIN_ISLAND_DISTANCE * MIN_ISLAND_DISTANCE) return true; // přesný test
        }
        return false;
    }

    // Patří tento typ políčka k ostrovu (pevnina / molo / obchod / maják)?
    private static bool IsIslandTile(int type)
        => type == (int)TileType.Harbor || type == (int)TileType.Pier
        || type == (int)TileType.UpgradeShop || type == (int)TileType.QuestShop
        || type == (int)TileType.Lighthouse || type == (int)TileType.Chest;

    // ── Organický (nepravidelný) ostrov ────────────────────────────────────
    // Ostrov není čtverec: pevné jádro (min. 3×3) + náhodné rozrůstání na okraj.
    // Vrací seznam souřadnic pevniny (Harbor).
    private List<(int x, int y)> StampOrganicLand(int startX, int startY, bool explored)
    {
        int cx = startX + ISLAND_CANVAS / 2;
        int cy = startY + ISLAND_CANVAS / 2;
        int half = ISLAND_CANVAS / 2 - 1; // meze plátna (nech okraj volný pro molo)

        var land = new HashSet<(int, int)>();

        // 1) Pevné jádro — náhodný obdélník 3..5 × 3..5 uprostřed (splňuje "min. 3×3").
        int cw = UnityEngine.Random.Range(3, 6);
        int ch = UnityEngine.Random.Range(3, 6);
        for (int x = cx - cw / 2; x <= cx - cw / 2 + cw - 1; x++)
            for (int y = cy - ch / 2; y <= cy - ch / 2 + ch - 1; y++)
                land.Add((x, y));

        // 2) Organické rozrůstání — opakovaně přilep náhodné políčko na okraj tvaru.
        var dirs = new (int dx, int dy)[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        var frontier = new List<(int, int)>(land);
        int grow = UnityEngine.Random.Range(40, 90);
        for (int i = 0; i < grow; i++)
        {
            var pick = frontier[UnityEngine.Random.Range(0, frontier.Count)];
            var d = dirs[UnityEngine.Random.Range(0, 4)];
            var n = (pick.Item1 + d.dx, pick.Item2 + d.dy);
            if (Mathf.Abs(n.Item1 - cx) > half || Mathf.Abs(n.Item2 - cy) > half) continue;
            if (land.Add(n)) frontier.Add(n);
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
        if (land.Count < 9) return; // pojistka

        PlaceEdgePier(land);
        PlaceLighthouse(land);
        MaybePlaceChest(land);
    }

    // Dvě políčka mola vedle sebe na okraji ostrova (obě mají "ven" vodu).
    private void PlaceEdgePier(List<(int x, int y)> land)
    {
        var set = new HashSet<(int, int)>(land);
        var dirs = new (int dx, int dy)[] { (0, -1), (0, 1), (-1, 0), (1, 0) };

        // Zamíchej strany, ať molo není vždy stejně.
        for (int i = 0; i < dirs.Length; i++)
        {
            int j = UnityEngine.Random.Range(i, dirs.Length);
            var t = dirs[i]; dirs[i] = dirs[j]; dirs[j] = t;
        }

        foreach (var d in dirs)
        {
            var perp = d.dx == 0 ? (dx: 1, dy: 0) : (dx: 0, dy: 1); // kolmo = "vedle sebe"

            // Zamíchané pořadí pevniny, ať molo není vždy v rohu.
            var shuffled = new List<(int x, int y)>(land);
            for (int i = 0; i < shuffled.Count; i++)
            {
                int j = UnityEngine.Random.Range(i, shuffled.Count);
                var t = shuffled[i]; shuffled[i] = shuffled[j]; shuffled[j] = t;
            }

            foreach (var p in shuffled)
            {
                if (set.Contains((p.x + d.dx, p.y + d.dy))) continue;          // p musí mít ven vodu
                var q = (p.x + perp.dx, p.y + perp.dy);
                if (!set.Contains(q)) continue;                                // vedlejší musí být pevnina
                if (set.Contains((q.Item1 + d.dx, q.Item2 + d.dy))) continue;  // a taky mít ven vodu

                gameData.tileData[GridKey(p.x, p.y)]           = new TileStatus((int)TileType.Pier);
                gameData.tileData[GridKey(q.Item1, q.Item2)]   = new TileStatus((int)TileType.Pier);
                return;
            }
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
                && IsHarborTile(p.x, p.y + 1) && IsHarborTile(p.x + 1, p.y + 1))
                spots.Add(p);
        }
        if (spots.Count == 0) return;

        var a = spots[UnityEngine.Random.Range(0, spots.Count)];
        for (int ix = 0; ix < 2; ix++)
            for (int iy = 0; iy < 2; iy++)
                gameData.tileData[GridKey(a.x + ix, a.y + iy)] = new TileStatus((int)TileType.Lighthouse);
    }

    // S 40% šancí položí jednu bednu na náhodné (volné) políčko pevniny.
    private void MaybePlaceChest(List<(int x, int y)> land)
    {
        if (UnityEngine.Random.value >= 0.4f) return;

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

    // Náhodný typ mořského políčka: 0,5 % poklad, 0,5 % ryby, zbytek voda.
    private TileType GenerateRandomSeaType()
    {
        float roll = UnityEngine.Random.value * 100f;
        if (roll < 0.5f) return TileType.Treasure;
        if (roll < 1.0f) return TileType.Water_Fish;
        return TileType.Water;
    }

    // ── Vytvoření / mazání 3D objektů políček ──────────────────────────────

    // Vytvoří 3D objekt jednoho políčka podle jeho typu.
    private void InstantiateTile(int x, int y, TileStatus status)
    {
        GameObject prefab = GetPrefabForType((TileType)status.type);
        if (prefab == null) return;

        Vector3 pos = new Vector3(x, -0.1f, y);
        GameObject newTile = Instantiate(prefab, pos, Quaternion.identity, transform);
        activeTiles.Add(GridKey(x, y), newTile);

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
                    tower.localScale    *= 1.6f;
                }
                else
                {
                    tower.gameObject.SetActive(false);
                }
            }
        }
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

    // ── Startovní ostrov (úplně nová hra) ──────────────────────────────────
    private void GenerateInitialWorld()
    {
        // Stejný organický generátor jako pro ostatní ostrovy, jen kolem počátku
        // a rovnou prozkoumaný.
        var land = StampOrganicLand(-ISLAND_CANVAS / 2, -ISLAND_CANVAS / 2, explored: true);
        PlaceEdgePier(land);
        PlaceLighthouse(land);
        MaybePlaceChest(land);

        // Postav hráče (v lodi) na jedno z políček mola.
        foreach (var kv in gameData.tileData)
        {
            if (kv.Value.type != (int)TileType.Pier) continue;
            var (px, py) = ParseGridKey(kv.Key);
            gameData.playerGridX = px;
            gameData.playerGridY = py;
            gameData.boatGridX   = px;
            gameData.boatGridY   = py;
            break;
        }

        MarkAreaExplored(gameData.playerGridX, gameData.playerGridY, ISLAND_CANVAS);
    }

    // Prefab pro daný typ políčka (obchody padají zpět na harborPrefab, když nejsou nastavené).
    private GameObject GetPrefabForType(TileType t)
    {
        switch (t)
        {
            case TileType.Water:       return waterPrefab;
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
    }
}
