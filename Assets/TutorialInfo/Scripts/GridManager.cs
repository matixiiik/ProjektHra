using UnityEngine;
using System.Collections.Generic;
using System; // kvůli Action

public class GridManager : MonoBehaviour
{
    public const int ACTIVE_GRID_SIZE = 15;

    public GameObject waterPrefab;
    public GameObject waterFishPrefab;
    public GameObject treasurePrefab;
    public GameObject harborPrefab;
    public GameObject pierPrefab; // ✅ přístav (černý blok)

    [HideInInspector] public GameData gameData;
    private Dictionary<string, GameObject> activeTiles = new Dictionary<string, GameObject>();

    public event Action OnWorldChanged;

    private int fogLayer;

    // ✅ Nastavení ostrovů
    private const int ISLAND_SIZE = 10;
    private const int ISLAND_PADDING = 1;       // nic “vedle” (okraj kolem ostrova)
    private const int MIN_ISLAND_DISTANCE = 50; // minimální vzdálenost mezi ostrovy

    void Awake()
    {
        fogLayer = LayerMask.NameToLayer("Fog");
        gameData = SaveManager.LoadGame();
        if (gameData.tileData.Count == 0) GenerateInitialWorld(0, 0);
        GenerateWorld(gameData.playerGridX, gameData.playerGridY);

        OnWorldChanged?.Invoke();
    }

    void OnApplicationQuit() => Save();

    public void Save()
    {
        CleanupWorldData();
        SaveManager.SaveGame(gameData);
    }

    private void CleanupWorldData()
    {
        int limit = 100;
        List<string> keysToRemove = new List<string>();

        foreach (var entry in gameData.tileData)
        {
            string[] coords = entry.Key.Split(',');
            int x = int.Parse(coords[0]);
            int y = int.Parse(coords[1]);

            bool isTooFar = Mathf.Abs(x - gameData.playerGridX) > limit || Mathf.Abs(y - gameData.playerGridY) > limit;
            bool isImportant = entry.Value.type == (int)TileType.Harbor || entry.Value.isExplored || entry.Value.type == (int)TileType.Pier;

            if (isTooFar && !isImportant) keysToRemove.Add(entry.Key);
        }

        foreach (string key in keysToRemove) gameData.tileData.Remove(key);
    }

    public void MarkTileExplored(int x, int y)
    {
        string key = $"{x},{y}";
        if (gameData.tileData.ContainsKey(key))
        {
            if (!gameData.tileData[key].isExplored)
            {
                gameData.tileData[key].isExplored = true;

                if (activeTiles.ContainsKey(key))
                {
                    Transform fog = activeTiles[key].transform.Find("FogVisual");
                    if (fog != null) fog.gameObject.SetActive(false);
                }

                OnWorldChanged?.Invoke();
            }
        }
    }

    public void GenerateWorld(int centerX, int centerY)
    {
        ClearOldTiles(centerX, centerY);

        for (int x = centerX - ACTIVE_GRID_SIZE; x <= centerX + ACTIVE_GRID_SIZE; x++)
        {
            for (int y = centerY - ACTIVE_GRID_SIZE; y <= centerY + ACTIVE_GRID_SIZE; y++)
            {
                string key = $"{x},{y}";
                if (!gameData.tileData.ContainsKey(key)) CheckAndGenerateArea(x, y);
                if (!activeTiles.ContainsKey(key)) InstantiateTile(x, y, gameData.tileData[key]);
            }
        }

        OnWorldChanged?.Invoke();
    }

    private void CheckAndGenerateArea(int x, int y)
    {
        // ✅ ostrov: 10% na bodech násobků 20
        if (x % 20 == 0 && y % 20 == 0 && UnityEngine.Random.value < 0.1f)
        {
            // ✅ generuj jen když se vejde, nezasáhne nic okolo a je daleko od jiných ostrovů
            if (CanPlaceIsland(x, y))
            {
                GenerateIsland(x, y);
                return;
            }
        }

        string key = $"{x},{y}";
        if (!gameData.tileData.ContainsKey(key))
            gameData.tileData.Add(key, new TileStatus((int)GenerateRandomSeaType()));
    }

    // ✅ kontrola, jestli je bezpečné ostrov položit
    private bool CanPlaceIsland(int startX, int startY)
    {
        // 1) minimální vzdálenost od jiných ostrovů
        if (IsAnotherIslandTooClose(startX, startY)) return false;

        // 2) v oblasti ostrova + padding nesmí být Harbor/Pier (aby se nepřekrýval)
        int fromX = startX - ISLAND_PADDING;
        int toX = startX + ISLAND_SIZE - 1 + ISLAND_PADDING;
        int fromY = startY - ISLAND_PADDING;
        int toY = startY + ISLAND_SIZE - 1 + ISLAND_PADDING;

        for (int x = fromX; x <= toX; x++)
        {
            for (int y = fromY; y <= toY; y++)
            {
                string key = $"{x},{y}";
                if (!gameData.tileData.ContainsKey(key)) continue;

                int t = gameData.tileData[key].type;
                if (t == (int)TileType.Harbor || t == (int)TileType.Pier)
                    return false;
            }
        }

        return true;
    }

    // ✅ zjistí, jestli už existuje ostrov (Harbor/Pier) blíž než 50 bloků
    private bool IsAnotherIslandTooClose(int startX, int startY)
    {
        // střed kandidátního ostrova
        float cx = startX + (ISLAND_SIZE - 1) * 0.5f;
        float cy = startY + (ISLAND_SIZE - 1) * 0.5f;

        // hrubý filtr kvůli výkonu
        int max = MIN_ISLAND_DISTANCE + ISLAND_SIZE;

        foreach (var kv in gameData.tileData)
        {
            int t = kv.Value.type;
            if (t != (int)TileType.Harbor && t != (int)TileType.Pier) continue;

            string[] c = kv.Key.Split(',');
            int x = int.Parse(c[0]);
            int y = int.Parse(c[1]);

            if (Mathf.Abs(x - cx) > max || Mathf.Abs(y - cy) > max) continue;

            float dx = x - cx;
            float dy = y - cy;
            if ((dx * dx + dy * dy) < (MIN_ISLAND_DISTANCE * MIN_ISLAND_DISTANCE))
                return true;
        }

        return false;
    }

    // ✅ 10x10 ostrov + 2x1 pier na okraji, ostrov je VŽDY celý (přepisuje se)
    private void GenerateIsland(int startX, int startY)
    {
        int size = ISLAND_SIZE;

        // 1) ostrov 10x10 (Harbor) – VŽDY přepsat, aby nebyl děravý
        for (int ix = 0; ix < size; ix++)
        {
            for (int iy = 0; iy < size; iy++)
            {
                string key = $"{startX + ix},{startY + iy}";

                // zachovej explored, pokud už to někdy bylo odkryté
                bool explored = gameData.tileData.ContainsKey(key) && gameData.tileData[key].isExplored;

                gameData.tileData[key] = new TileStatus((int)TileType.Harbor)
                {
                    isExplored = explored
                };
            }
        }

        // 2) random pier 2x1 na okraji
        int side = UnityEngine.Random.Range(0, 4); // 0 bottom, 1 top, 2 left, 3 right

        int px1 = 0, py1 = 0;
        int px2 = 0, py2 = 0;

        if (side == 0) // bottom edge
        {
            int x = UnityEngine.Random.Range(startX, startX + size - 1);
            px1 = x; py1 = startY;
            px2 = x + 1; py2 = startY;
        }
        else if (side == 1) // top edge
        {
            int x = UnityEngine.Random.Range(startX, startX + size - 1);
            px1 = x; py1 = startY + size - 1;
            px2 = x + 1; py2 = startY + size - 1;
        }
        else if (side == 2) // left edge
        {
            int y = UnityEngine.Random.Range(startY, startY + size - 1);
            px1 = startX; py1 = y;
            px2 = startX; py2 = y + 1;
        }
        else // right edge
        {
            int y = UnityEngine.Random.Range(startY, startY + size - 1);
            px1 = startX + size - 1; py1 = y;
            px2 = startX + size - 1; py2 = y + 1;
        }

        gameData.tileData[$"{px1},{py1}"] = new TileStatus((int)TileType.Pier);
        gameData.tileData[$"{px2},{py2}"] = new TileStatus((int)TileType.Pier);
    }

    private TileType GenerateRandomSeaType()
    {
        float roll = UnityEngine.Random.value * 100f;

        if (roll < 0.5f) return TileType.Treasure;
        if (roll < 1.0f) return TileType.Water_Fish;
        return TileType.Water;
    }

    private void InstantiateTile(int x, int y, TileStatus status)
    {
        GameObject prefab = GetPrefabForType((TileType)status.type);
        if (prefab == null) return;

        Vector3 pos = new Vector3(x, -0.1f, y);
        GameObject newTile = Instantiate(prefab, pos, Quaternion.identity, transform);
        activeTiles.Add($"{x},{y}", newTile);

        Transform fog = newTile.transform.Find("FogVisual");
        if (fog != null)
        {
            fog.gameObject.layer = fogLayer;
            fog.gameObject.SetActive(!status.isExplored);
        }

        Transform icon = newTile.transform.Find("MapIcon");
        if (icon != null)
        {
            icon.gameObject.layer = LayerMask.NameToLayer("MinimapOnly");
        }
    }

    private void ClearOldTiles(int centerX, int centerY)
    {
        List<string> keysToRemove = new List<string>();
        int dist = ACTIVE_GRID_SIZE + 2;

        foreach (var tile in activeTiles)
        {
            string[] c = tile.Key.Split(',');
            if (Mathf.Abs(int.Parse(c[0]) - centerX) > dist || Mathf.Abs(int.Parse(c[1]) - centerY) > dist)
                keysToRemove.Add(tile.Key);
        }

        foreach (string k in keysToRemove)
        {
            Destroy(activeTiles[k]);
            activeTiles.Remove(k);
        }
    }

    public TileType GetTileType(int x, int y)
    {
        string key = $"{x},{y}";
        return gameData.tileData.ContainsKey(key) ? (TileType)gameData.tileData[key].type : TileType.Water;
    }

    private void GenerateInitialWorld(int cx, int cy)
    {
        // startovní ostrov vždy 10x10 od (0,0)
        int startX = 0;
        int startY = 0;
        int size = 10;

        // 1) ostrov 10x10 (Harbor) + explored
        for (int ix = 0; ix < size; ix++)
        {
            for (int iy = 0; iy < size; iy++)
            {
                string key = $"{startX + ix},{startY + iy}";
                gameData.tileData[key] = new TileStatus((int)TileType.Harbor) { isExplored = true };
            }
        }

        // 2) přístav 2x1 (Pier) – dáme ho fixně na spodní okraj doprostřed
        int px1 = startX + (size / 2) - 1;
        int py1 = startY; // spodní okraj
        int px2 = px1 + 1;
        int py2 = py1;

        gameData.tileData[$"{px1},{py1}"] = new TileStatus((int)TileType.Pier) { isExplored = true };
        gameData.tileData[$"{px2},{py2}"] = new TileStatus((int)TileType.Pier) { isExplored = true };

        // 3) spawn hráče na Pier
        gameData.playerGridX = px1;
        gameData.playerGridY = py1;

        // 4) odkryj okolí (aby minimapa hned ukázala start)
        for (int x = startX - 2; x <= startX + size + 1; x++)
            for (int y = startY - 2; y <= startY + size + 1; y++)
                MarkTileExplored(x, y);
    }

    private GameObject GetPrefabForType(TileType t)
    {
        switch (t)
        {
            case TileType.Water: return waterPrefab;
            case TileType.Water_Fish: return waterFishPrefab;
            case TileType.Treasure: return treasurePrefab;
            case TileType.Harbor: return harborPrefab;
            case TileType.Pier: return pierPrefab;
            default: return null;
        }
    }

    public void SetTileType(int x, int y, TileType newType)
    {
        string key = $"{x},{y}";
        if (gameData.tileData.ContainsKey(key))
        {
            gameData.tileData[key].type = (int)newType;

            if (activeTiles.ContainsKey(key))
            {
                Destroy(activeTiles[key]);
                activeTiles.Remove(key);
                InstantiateTile(x, y, gameData.tileData[key]);
            }

            OnWorldChanged?.Invoke();
        }
    }
    public void NewGameReset()
    {
        // 1) Smaž save soubor
        SaveManager.DeleteSave();

        // 2) Reset GameData do čistého stavu
        gameData = new GameData();
        gameData.playerGridX = 0;
        gameData.playerGridY = 0;
        gameData.coins = 0;
        gameData.hasRodUpgrade = false;
        gameData.hasSpeedUpgrade = false;
        gameData.tileData.Clear();

        // 3) Vyčisti aktivní tile objekty ze scény
        foreach (var kv in activeTiles)
            Destroy(kv.Value);
        activeTiles.Clear();

        // 4) Vytvoř nový startovní svět a načti okolí
        GenerateInitialWorld(0, 0);
        GenerateWorld(0, 0);

        // 5) Ulož nový čistý save
        Save();

        // 6) Refresh UI/minimapy
        OnWorldChanged?.Invoke();
    }
}