using UnityEngine;
using System.Collections.Generic;
using System;

public class GridManager : MonoBehaviour
{
    public const int ACTIVE_GRID_SIZE = 15;

    public GameObject waterPrefab;
    public GameObject waterFishPrefab;
    public GameObject treasurePrefab;
    public GameObject harborPrefab;
    public GameObject pierPrefab;

    [HideInInspector] public GameData gameData;
    private Dictionary<string, GameObject> activeTiles = new Dictionary<string, GameObject>();

    public event Action OnWorldChanged;

    private int fogLayer;

    private const int ISLAND_SIZE = 10;
    private const int ISLAND_PADDING = 1;
    private const int MIN_ISLAND_DISTANCE = 50;
    private const int CLEANUP_LIMIT = 100;

    void Awake()
    {
        fogLayer = LayerMask.NameToLayer("Fog");
        gameData = SaveManager.LoadGame();
        if (gameData.tileData.Count == 0) GenerateInitialWorld();
        GenerateWorld(gameData.playerGridX, gameData.playerGridY);
        OnWorldChanged?.Invoke();
    }

    void OnApplicationQuit() => Save();

    public void Save()
    {
        CleanupWorldData();
        SaveManager.SaveGame(gameData);
    }

    private static string GridKey(int x, int y) => $"{x},{y}";

    private static (int x, int y) ParseGridKey(string key)
    {
        var p = key.Split(',');
        return (int.Parse(p[0]), int.Parse(p[1]));
    }

    private void CleanupWorldData()
    {
        var keysToRemove = new List<string>();
        foreach (var entry in gameData.tileData)
        {
            var (x, y) = ParseGridKey(entry.Key);
            bool isTooFar = Mathf.Abs(x - gameData.playerGridX) > CLEANUP_LIMIT
                         || Mathf.Abs(y - gameData.playerGridY) > CLEANUP_LIMIT;
            bool isImportant = entry.Value.type == (int)TileType.Harbor
                            || entry.Value.isExplored
                            || entry.Value.type == (int)TileType.Pier;
            if (isTooFar && !isImportant) keysToRemove.Add(entry.Key);
        }
        foreach (string key in keysToRemove) gameData.tileData.Remove(key);
    }

    public void MarkTileExplored(int x, int y)
    {
        string key = GridKey(x, y);
        if (!gameData.tileData.ContainsKey(key)) return;
        if (gameData.tileData[key].isExplored) return;

        gameData.tileData[key].isExplored = true;
        if (activeTiles.ContainsKey(key))
        {
            Transform fog = activeTiles[key].transform.Find("FogVisual");
            if (fog != null) fog.gameObject.SetActive(false);
        }
        OnWorldChanged?.Invoke();
    }

    public void MarkAreaExplored(int cx, int cy, int radius)
    {
        for (int x = -radius; x <= radius; x++)
            for (int y = -radius; y <= radius; y++)
            {
                string key = GridKey(cx + x, cy + y);
                if (!gameData.tileData.ContainsKey(key)) continue;
                if (gameData.tileData[key].isExplored) continue;
                gameData.tileData[key].isExplored = true;
                if (activeTiles.ContainsKey(key))
                {
                    Transform fog = activeTiles[key].transform.Find("FogVisual");
                    if (fog != null) fog.gameObject.SetActive(false);
                }
            }
        OnWorldChanged?.Invoke();
    }

    public void GenerateWorld(int centerX, int centerY)
    {
        ClearOldTiles(centerX, centerY);

        for (int x = centerX - ACTIVE_GRID_SIZE; x <= centerX + ACTIVE_GRID_SIZE; x++)
        {
            for (int y = centerY - ACTIVE_GRID_SIZE; y <= centerY + ACTIVE_GRID_SIZE; y++)
            {
                string key = GridKey(x, y);
                if (!gameData.tileData.ContainsKey(key)) CheckAndGenerateArea(x, y);
                if (!activeTiles.ContainsKey(key)) InstantiateTile(x, y, gameData.tileData[key]);
            }
        }

        OnWorldChanged?.Invoke();
    }

    private void CheckAndGenerateArea(int x, int y)
    {
        if (x % 20 == 0 && y % 20 == 0 && UnityEngine.Random.value < 0.1f && CanPlaceIsland(x, y))
        {
            GenerateIsland(x, y);
            return;
        }

        string key = GridKey(x, y);
        if (!gameData.tileData.ContainsKey(key))
        {
            TileType seaType = GenerateRandomSeaType();
            var status = new TileStatus((int)seaType);
            if (seaType == TileType.Water_Fish) status.fishRemaining = 3;
            gameData.tileData.Add(key, status);
        }
    }

    private bool CanPlaceIsland(int startX, int startY)
    {
        if (IsAnotherIslandTooClose(startX, startY)) return false;

        int fromX = startX - ISLAND_PADDING;
        int toX = startX + ISLAND_SIZE - 1 + ISLAND_PADDING;
        int fromY = startY - ISLAND_PADDING;
        int toY = startY + ISLAND_SIZE - 1 + ISLAND_PADDING;

        for (int x = fromX; x <= toX; x++)
        {
            for (int y = fromY; y <= toY; y++)
            {
                string key = GridKey(x, y);
                if (!gameData.tileData.ContainsKey(key)) continue;
                int t = gameData.tileData[key].type;
                if (t == (int)TileType.Harbor || t == (int)TileType.Pier) return false;
            }
        }
        return true;
    }

    private bool IsAnotherIslandTooClose(int startX, int startY)
    {
        float cx = startX + (ISLAND_SIZE - 1) * 0.5f;
        float cy = startY + (ISLAND_SIZE - 1) * 0.5f;
        int max = MIN_ISLAND_DISTANCE + ISLAND_SIZE;

        foreach (var kv in gameData.tileData)
        {
            int t = kv.Value.type;
            if (t != (int)TileType.Harbor && t != (int)TileType.Pier) continue;

            var (x, y) = ParseGridKey(kv.Key);
            if (Mathf.Abs(x - cx) > max || Mathf.Abs(y - cy) > max) continue;

            float dx = x - cx, dy = y - cy;
            if (dx * dx + dy * dy < MIN_ISLAND_DISTANCE * MIN_ISLAND_DISTANCE) return true;
        }
        return false;
    }

    private void StampHarborBlock(int startX, int startY, bool explored = false)
    {
        for (int ix = 0; ix < ISLAND_SIZE; ix++)
        {
            for (int iy = 0; iy < ISLAND_SIZE; iy++)
            {
                string key = GridKey(startX + ix, startY + iy);
                bool wasExplored = explored || (gameData.tileData.ContainsKey(key) && gameData.tileData[key].isExplored);
                gameData.tileData[key] = new TileStatus((int)TileType.Harbor) { isExplored = wasExplored };
            }
        }
    }

    private void GenerateIsland(int startX, int startY)
    {
        StampHarborBlock(startX, startY);

        int side = UnityEngine.Random.Range(0, 4);
        int px1, py1, px2, py2;

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
        activeTiles.Add(GridKey(x, y), newTile);

        Transform fog = newTile.transform.Find("FogVisual");
        if (fog != null)
        {
            fog.gameObject.layer = fogLayer;
            fog.gameObject.SetActive(!status.isExplored);
        }

        Transform icon = newTile.transform.Find("MapIcon");
        if (icon != null)
            icon.gameObject.layer = LayerMask.NameToLayer("MinimapOnly");
    }

    private void ClearOldTiles(int centerX, int centerY)
    {
        var keysToRemove = new List<string>();
        int dist = ACTIVE_GRID_SIZE + 2;

        foreach (var tile in activeTiles)
        {
            var (x, y) = ParseGridKey(tile.Key);
            if (Mathf.Abs(x - centerX) > dist || Mathf.Abs(y - centerY) > dist)
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
        string key = GridKey(x, y);
        return gameData.tileData.ContainsKey(key) ? (TileType)gameData.tileData[key].type : TileType.Water;
    }

    public TileStatus GetTileStatus(int x, int y)
    {
        string key = GridKey(x, y);
        return gameData.tileData.ContainsKey(key) ? gameData.tileData[key] : null;
    }

    public void NotifyWorldChanged() => OnWorldChanged?.Invoke();

    private void GenerateInitialWorld()
    {
        StampHarborBlock(0, 0, explored: true);

        int px1 = ISLAND_SIZE / 2 - 1;
        int py1 = 0;
        int px2 = px1 + 1;

        gameData.tileData[GridKey(px1, py1)] = new TileStatus((int)TileType.Pier) { isExplored = true };
        gameData.tileData[GridKey(px2, py1)] = new TileStatus((int)TileType.Pier) { isExplored = true };

        gameData.playerGridX = px1;
        gameData.playerGridY = py1;

        MarkAreaExplored(ISLAND_SIZE / 2, ISLAND_SIZE / 2, ISLAND_SIZE / 2 + 2);
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

    public void NewGameReset()
    {
        SaveManager.DeleteSave();
        gameData = new GameData();

        foreach (var kv in activeTiles) Destroy(kv.Value);
        activeTiles.Clear();

        GenerateInitialWorld();
        GenerateWorld(0, 0);
        Save();
        OnWorldChanged?.Invoke();
    }
}
