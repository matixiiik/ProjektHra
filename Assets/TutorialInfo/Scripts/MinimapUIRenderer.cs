using UnityEngine;
using UnityEngine.UI;

public class MinimapUIRenderer : MonoBehaviour
{
    public RawImage minimapImage;

    [HideInInspector] public int playerIndex = 0;

    [Header("Kolik políček kolem hráče ukázat")]
    public int viewRadius = 25;

    [Header("Barvy")]
    public Color waterColor      = new Color(0.15f, 0.75f, 0.85f, 1f);
    public Color fishColor       = new Color(0.1f,  0.35f, 0.85f, 1f);
    public Color treasureColor   = new Color(0.95f, 0.65f, 0.1f,  1f);
    public Color harborColor     = new Color(0.2f,  0.85f, 0.2f,  1f);
    public Color pierColor       = new Color(0.1f,  0.1f,  0.1f,  1f);
    public Color fogColor        = new Color(0.35f, 0.35f, 0.35f, 1f);
    public Color playerColor     = Color.white;
    public Color otherPlayerColor = new Color(1f, 0.5f, 0f, 1f); // oranžová = druhý hráč

    [Header("Okraj minimapy")]
    public int   borderPixels = 2;
    public Color borderColor  = new Color(0.2f, 0.2f, 0.2f, 1f);

    private GridManager grid;
    private Texture2D   tex;
    private int         size;

    void Start()
    {
        grid = FindFirstObjectByType<GridManager>();
        if (grid == null)
        {
            Debug.LogError("MinimapUIRenderer: GridManager nenalezen.");
            enabled = false;
            return;
        }

        // Pokud není minimapImage přiřazen v editoru, vytvoř canvas automaticky
        if (minimapImage == null)
            minimapImage = CreateMinimapCanvas();

        if (minimapImage == null) { enabled = false; return; }

        size = viewRadius * 2 + 1;
        tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode   = TextureWrapMode.Clamp;

        minimapImage.texture = tex;
        minimapImage.uvRect  = new Rect(0, 0, 1, 1);

        grid.OnWorldChanged += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        if (grid != null) grid.OnWorldChanged -= Refresh;
    }

    // ── Automatické vytvoření canvasu pro P2 ─────────────────────────────────
    RawImage CreateMinimapCanvas()
    {
        var canvasGO = new GameObject($"MinimapCanvas_P{playerIndex + 1}");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var imgGO = new GameObject("MinimapImage");
        imgGO.transform.SetParent(canvasGO.transform, false);

        var rt = imgGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(170f, 170f);

        // P1 → levý dolní roh,  P2 → pravý dolní roh
        if (playerIndex == 0)
        {
            rt.anchorMin = rt.anchorMax = Vector2.zero;
            rt.pivot     = Vector2.zero;
            rt.anchoredPosition = new Vector2(10f, 10f);
        }
        else
        {
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-10f, 10f);
        }

        return imgGO.AddComponent<RawImage>();
    }

    // ── Překreslení textury ───────────────────────────────────────────────────
    void Refresh()
    {
        GameData d = grid.gameData;
        int cx = playerIndex == 0 ? d.playerGridX  : d.player2GridX;
        int cy = playerIndex == 0 ? d.playerGridY  : d.player2GridY;

        for (int px = 0; px < size; px++)
            for (int py = 0; py < size; py++)
                tex.SetPixel(px, py, GetTileColor(cx + (px - viewRadius), cy + (py - viewRadius)));

        // Vlastní hráč — bílý bod uprostřed
        tex.SetPixel(viewRadius, viewRadius, playerColor);

        // Druhý hráč — oranžový bod (jen v multiplayeru)
        if (MultiplayerManager.IsMultiplayer)
        {
            int ox = playerIndex == 0 ? d.player2GridX : d.playerGridX;
            int oy = playerIndex == 0 ? d.player2GridY : d.playerGridY;
            int rx = ox - cx + viewRadius;
            int ry = oy - cy + viewRadius;
            if (rx >= 0 && rx < size && ry >= 0 && ry < size)
                tex.SetPixel(rx, ry, otherPlayerColor);
        }

        DrawBorder();
        tex.Apply(false);
    }

    void DrawBorder()
    {
        int b = Mathf.Clamp(borderPixels, 0, 10);
        if (b <= 0) return;

        for (int x = 0; x < size; x++)
            for (int y = 0; y < b; y++)
            {
                tex.SetPixel(x, y, borderColor);
                tex.SetPixel(x, size - 1 - y, borderColor);
            }

        for (int y = b; y < size - b; y++)
            for (int x = 0; x < b; x++)
            {
                tex.SetPixel(x, y, borderColor);
                tex.SetPixel(size - 1 - x, y, borderColor);
            }
    }

    Color GetTileColor(int x, int y)
    {
        string key = $"{x},{y}";
        if (!grid.gameData.tileData.ContainsKey(key)) return fogColor;

        var st = grid.gameData.tileData[key];
        if (!st.isExplored) return fogColor;

        switch ((TileType)st.type)
        {
            case TileType.Water:      return waterColor;
            case TileType.Water_Fish: return fishColor;
            case TileType.Treasure:   return treasureColor;
            case TileType.Harbor:     return harborColor;
            case TileType.Pier:       return pierColor;
            default:                  return waterColor;
        }
    }
}
