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

    [HideInInspector] public GameData gameData; // veškerý stav hry

    // Právě existující 3D objekty políček. Klíč "x,y" → objekt ve scéně.
    private Dictionary<string, GameObject> activeTiles = new Dictionary<string, GameObject>();

    /// <summary>Vyvolá se po každé změně světa (pohyb, těžba, nákup...). Poslouchá HUD a minimapa.</summary>
    public event Action OnWorldChanged;

    private int fogLayer;     // vrstva "Fog" (mlha se nekreslí do minimapy)
    private int minimapLayer; // vrstva "MinimapOnly" (ikony jen pro minimapu)

    // Parametry generování ostrovů.
    private const int ISLAND_SIZE         = 10; // ostrov je 10×10 políček
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

        // Načti naposledy použitý slot a jeho data.
        SaveManager.CurrentSlot = PlayerPrefs.GetInt("LastSlot", 0);
        gameData = SaveManager.LoadGame();

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
        int toX   = startX + ISLAND_SIZE - 1 + ISLAND_PADDING;
        int fromY = startY - ISLAND_PADDING;
        int toY   = startY + ISLAND_SIZE - 1 + ISLAND_PADDING;

        // V ploše ostrova (+ okraj) nesmí být kus jiného ostrova.
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
        float cx = startX + (ISLAND_SIZE - 1) * 0.5f;
        float cy = startY + (ISLAND_SIZE - 1) * 0.5f;
        int   max = MIN_ISLAND_DISTANCE + ISLAND_SIZE;

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
        || type == (int)TileType.Lighthouse;

    // Vyplní čtverec 10×10 pevninou (Harbor). Zachová u políček dřívější "prozkoumáno".
    private void StampHarborBlock(int startX, int startY, bool explored = false)
    {
        for (int ix = 0; ix < ISLAND_SIZE; ix++)
        {
            for (int iy = 0; iy < ISLAND_SIZE; iy++)
            {
                string key = GridKey(startX + ix, startY + iy);
                bool wasExplored = explored
                    || (gameData.tileData.ContainsKey(key) && gameData.tileData[key].isExplored);
                gameData.tileData[key] = new TileStatus((int)TileType.Harbor) { isExplored = wasExplored };
            }
        }
    }

    // Vygeneruje celý ostrov: pevninu, dvě políčka mola na náhodné straně a dva obchody.
    private void GenerateIsland(int startX, int startY)
    {
        StampHarborBlock(startX, startY);

        int side = UnityEngine.Random.Range(0, 4); // 0=dole, 1=nahoře, 2=vlevo, 3=vpravo
        int px1, py1, px2, py2;                     // dvě políčka mola vedle sebe

        if (side == 0)
        {
            int x = UnityEngine.Random.Range(startX, startX + ISLAND_SIZE - 1);
            px1 = x; py1 = startY; px2 = x + 1; py2 = startY;
        }
        else if (side == 1)
        {
            int x = UnityEngine.Random.Range(startX, startX + ISLAND_SIZE - 1);
            px1 = x; py1 = startY + ISLAND_SIZE - 1; px2 = x + 1; py2 = startY + ISLAND_SIZE - 1;
        }
        else if (side == 2)
        {
            int y = UnityEngine.Random.Range(startY, startY + ISLAND_SIZE - 1);
            px1 = startX; py1 = y; px2 = startX; py2 = y + 1;
        }
        else
        {
            int y = UnityEngine.Random.Range(startY, startY + ISLAND_SIZE - 1);
            px1 = startX + ISLAND_SIZE - 1; py1 = y; px2 = startX + ISLAND_SIZE - 1; py2 = y + 1;
        }

        gameData.tileData[GridKey(px1, py1)] = new TileStatus((int)TileType.Pier);
        gameData.tileData[GridKey(px2, py2)] = new TileStatus((int)TileType.Pier);

        PlaceLighthouse(startX, startY, side);
    }

    // Umístí jedno políčko majáku na hranu ostrova naproti molu, zhruba doprostřed.
    // Na maják se nechodí — hráč u něj stojí pěšky na sousední pevnině a dá `E`.
    private void PlaceLighthouse(int startX, int startY, int side)
    {
        int mid = ISLAND_SIZE / 2;
        int lx, ly;

        if      (side == 0) { lx = startX + mid; ly = startY + ISLAND_SIZE - 2; } // molo dole → maják nahoře
        else if (side == 1) { lx = startX + mid; ly = startY + 1;              } // molo nahoře → maják dole
        else if (side == 2) { lx = startX + ISLAND_SIZE - 2; ly = startY + mid; } // molo vlevo → maják vpravo
        else                { lx = startX + 1;              ly = startY + mid; } // molo vpravo → maják vlevo

        gameData.tileData[GridKey(lx, ly)] = new TileStatus((int)TileType.Lighthouse);
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
        StampHarborBlock(0, 0, explored: true);

        // Molo uprostřed dolní hrany.
        int px1 = ISLAND_SIZE / 2 - 1;
        int py1 = 0;
        int px2 = px1 + 1;

        gameData.tileData[GridKey(px1, py1)] = new TileStatus((int)TileType.Pier) { isExplored = true };
        gameData.tileData[GridKey(px2, py1)] = new TileStatus((int)TileType.Pier) { isExplored = true };

        // Molo je dole (side = 0) → maják na horní hraně.
        PlaceLighthouse(0, 0, side: 0);

        gameData.playerGridX = px1;
        gameData.playerGridY = py1;

        MarkAreaExplored(ISLAND_SIZE / 2, ISLAND_SIZE / 2, ISLAND_SIZE / 2 + 2);
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
        gameData = new GameData();
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

        gameData = SaveManager.LoadGame();
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

        gameData = new GameData();
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
