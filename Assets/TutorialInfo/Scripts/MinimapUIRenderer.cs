using UnityEngine;
using UnityEngine.UI;

public class MinimapUIRenderer : MonoBehaviour
{
    public RawImage minimapImage;

    [Header("Kolik políček kolem hráče ukázat")]
    public int viewRadius = 25; // 25 => 51x51

    [Header("Barvy")]
    public Color waterColor = new Color(0.15f, 0.75f, 0.85f, 1f);
    public Color fishColor = new Color(0.1f, 0.35f, 0.85f, 1f);
    public Color treasureColor = new Color(0.95f, 0.65f, 0.1f, 1f);
    public Color harborColor = new Color(0.2f, 0.85f, 0.2f, 1f);

    // ✅ mlha šedá (můžeš si doladit)
    public Color fogColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    public Color playerColor = Color.white;

    [Header("Okraj minimapy")]
    public int borderPixels = 2;                 // ✅ malý okraj
    public Color borderColor = new Color(0.2f, 0.2f, 0.2f, 1f);

    private GridManager grid;
    private Texture2D tex;
    private int size;

    void Start()
    {
        grid = FindFirstObjectByType<GridManager>();
        if (grid == null)
        {
            Debug.LogError("MinimapUIRenderer: GridManager nenalezen.");
            enabled = false;
            return;
        }

        if (minimapImage == null)
        {
            Debug.LogError("MinimapUIRenderer: minimapImage (RawImage) není nastaven.");
            enabled = false;
            return;
        }

        size = viewRadius * 2 + 1;

        tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode = TextureWrapMode.Clamp;

        minimapImage.texture = tex;

        // ✅ důležité pro “neposunutou” minimapu v UI
        minimapImage.uvRect = new Rect(0, 0, 1, 1);

        grid.OnWorldChanged += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        if (grid != null) grid.OnWorldChanged -= Refresh;
    }

    void Refresh()
    {
        int cx = grid.gameData.playerGridX;
        int cy = grid.gameData.playerGridY;

        // Vyplň mapu z dat
        for (int px = 0; px < size; px++)
        {
            for (int py = 0; py < size; py++)
            {
                int tx = cx + (px - viewRadius);
                int ty = cy + (py - viewRadius);
                tex.SetPixel(px, py, GetTileColor(tx, ty));
            }
        }

        // ✅ hráč vždy doprostřed
        tex.SetPixel(viewRadius, viewRadius, playerColor);

        // ✅ nakresli okraj
        DrawBorder();

        tex.Apply(false);
    }

    void DrawBorder()
    {
        int b = Mathf.Clamp(borderPixels, 0, 10);
        if (b <= 0) return;

        // horní + dolní pruh
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < b; y++)
            {
                tex.SetPixel(x, y, borderColor);                 // dole
                tex.SetPixel(x, size - 1 - y, borderColor);      // nahoře
            }
        }

        // levý + pravý pruh
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < b; x++)
            {
                tex.SetPixel(x, y, borderColor);                 // vlevo
                tex.SetPixel(size - 1 - x, y, borderColor);      // vpravo
            }
        }
    }

    Color GetTileColor(int x, int y)
    {
        string key = $"{x},{y}";

        // neexistuje -> mlha
        if (!grid.gameData.tileData.ContainsKey(key))
            return fogColor;

        var st = grid.gameData.tileData[key];

        // neprozkoumáno -> mlha
        if (!st.isExplored)
            return fogColor;

        // prozkoumáno -> typ
        switch ((TileType)st.type)
        {
            case TileType.Water: return waterColor;
            case TileType.Water_Fish: return fishColor;
            case TileType.Treasure: return treasureColor;
            case TileType.Harbor: return harborColor;
            default: return waterColor;
        }
    }
}