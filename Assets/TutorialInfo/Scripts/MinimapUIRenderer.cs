using UnityEngine;
using UnityEngine.UI;

// ─────────────────────────────────────────────────────────────────────────────
//  MinimapUIRenderer.cs
//  Kreslí minimapu jako malou texturu (čtvereček pixelů), kde 1 pixel = 1 políčko
//  mapy. Střed textury je hráč, okolo se vykreslují políčka podle jejich typu
//  a podle toho, jestli je hráč už prozkoumal (jinak jsou "v mlze").
//
//  Překresluje se při každé změně světa (grid.OnWorldChanged), tj. při pohybu,
//  rybaření, nákupu atd.
//
//  V multiplayeru má P2 vlastní kopii tohoto skriptu — když nemá přiřazený
//  minimapImage, vytvoří si vlastní canvas v rohu obrazovky.
// ─────────────────────────────────────────────────────────────────────────────

public class MinimapUIRenderer : MonoBehaviour
{
    public RawImage minimapImage; // UI prvek, do kterého se kreslí (nastaví se v editoru pro P1)

    [HideInInspector] public int playerIndex = 0; // 0 = P1, 1 = P2

    [Header("Kolik políček kolem hráče ukázat")]
    public int viewRadius = 25; // výsledná mapa má rozměr (2*viewRadius + 1)

    [Header("Barvy")]
    public Color waterColor       = new Color(0.15f, 0.75f, 0.85f, 1f);
    public Color fishColor        = new Color(0.1f,  0.35f, 0.85f, 1f);
    public Color treasureColor    = new Color(0.95f, 0.65f, 0.1f,  1f);
    public Color harborColor      = new Color(0.2f,  0.85f, 0.2f,  1f);
    public Color pierColor        = new Color(0.1f,  0.1f,  0.1f,  1f);
    public Color lighthouseColor  = new Color(1f,    0.25f, 0.2f,  1f); // maják (červená)
    public Color chestColor       = new Color(1f,    0.75f, 0.15f, 1f); // bedna (zlatá)
    public Color fogColor         = new Color(0.35f, 0.35f, 0.35f, 1f); // neprozkoumáno
    public Color playerColor      = Color.white;                        // bod vlastního hráče
    public Color otherPlayerColor = new Color(1f, 0.5f, 0f, 1f);        // bod druhého hráče (oranžová)

    [Header("Okraj minimapy")]
    public int   borderPixels = 2;
    public Color borderColor  = new Color(0.2f, 0.2f, 0.2f, 1f);

    private GridManager grid;
    private Texture2D   tex;  // samotná textura minimapy
    private int         size; // šířka i výška textury v pixelech

    void Start()
    {
        grid = FindFirstObjectByType<GridManager>();
        if (grid == null)
        {
            Debug.LogError("MinimapUIRenderer: GridManager nenalezen.");
            enabled = false;
            return;
        }

        // P2 nemá minimapImage přiřazený v editoru → vytvoř si vlastní canvas.
        if (minimapImage == null)
            minimapImage = CreateMinimapCanvas();

        if (minimapImage == null) { enabled = false; return; }

        // Připrav texturu. Point filtr = ostré pixely (bez rozmazání).
        size = viewRadius * 2 + 1;
        tex  = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;
        tex.wrapMode   = TextureWrapMode.Clamp;

        minimapImage.texture = tex;
        minimapImage.uvRect  = new Rect(0, 0, 1, 1);

        // Překresli minimapu při každé změně světa.
        grid.OnWorldChanged += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        // Odhlaš se z události, jinak by se volala i po zničení objektu.
        if (grid != null) grid.OnWorldChanged -= Refresh;
    }

    // ── Automatické vytvoření canvasu (pro P2 / když není přiřazen) ───────────
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

        // P1 → levý dolní roh, P2 → pravý dolní roh.
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

    // ── Překreslení textury ─────────────────────────────────────────────────
    void Refresh()
    {
        GameData d = grid.gameData;

        // Střed minimapy = pozice tohoto hráče.
        int cx = playerIndex == 0 ? d.playerGridX : d.player2GridX;
        int cy = playerIndex == 0 ? d.playerGridY : d.player2GridY;

        // Projdi všechny pixely a obarvi je podle políčka, které leží pod nimi.
        for (int px = 0; px < size; px++)
            for (int py = 0; py < size; py++)
                tex.SetPixel(px, py, GetTileColor(cx + (px - viewRadius), cy + (py - viewRadius)));

        // Vlastní hráč — bílý bod přesně uprostřed.
        tex.SetPixel(viewRadius, viewRadius, playerColor);

        // Druhý hráč — oranžový bod (jen v multiplayeru a jen když je na mapě vidět).
        if (MultiplayerManager.IsMultiplayer)
        {
            int ox = playerIndex == 0 ? d.player2GridX : d.playerGridX;
            int oy = playerIndex == 0 ? d.player2GridY : d.playerGridY;
            int rx = ox - cx + viewRadius; // přepočet na pixel minimapy
            int ry = oy - cy + viewRadius;
            if (rx >= 0 && rx < size && ry >= 0 && ry < size)
                tex.SetPixel(rx, ry, otherPlayerColor);
        }

        DrawBorder();
        tex.Apply(false); // promítni změny do textury
    }

    // Nakreslí rámeček po obvodu minimapy.
    void DrawBorder()
    {
        int b = Mathf.Clamp(borderPixels, 0, 10);
        if (b <= 0) return;

        // Horní a dolní okraj.
        for (int x = 0; x < size; x++)
            for (int y = 0; y < b; y++)
            {
                tex.SetPixel(x, y, borderColor);
                tex.SetPixel(x, size - 1 - y, borderColor);
            }

        // Levý a pravý okraj.
        for (int y = b; y < size - b; y++)
            for (int x = 0; x < b; x++)
            {
                tex.SetPixel(x, y, borderColor);
                tex.SetPixel(size - 1 - x, y, borderColor);
            }
    }

    // Vrátí barvu pro políčko na souřadnicích [x, y].
    Color GetTileColor(int x, int y)
    {
        string key = $"{x},{y}";

        // Políčko ještě neexistuje (nevygenerované) → mlha.
        if (!grid.gameData.tileData.ContainsKey(key)) return fogColor;

        var st = grid.gameData.tileData[key];
        if (!st.isExplored) return fogColor; // existuje, ale hráč tam nebyl

        switch ((TileType)st.type)
        {
            case TileType.Water:      return waterColor;
            case TileType.Water_Fish: return fishColor;
            case TileType.Treasure:   return treasureColor;
            case TileType.Harbor:     return harborColor;
            case TileType.Pier:       return pierColor;
            case TileType.Lighthouse: return lighthouseColor;
            case TileType.Chest:      return chestColor;
            default:                  return waterColor; // staré shopy apod. bereme jako vodu
        }
    }
}
