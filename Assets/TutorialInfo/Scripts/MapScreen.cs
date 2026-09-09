using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  MapScreen.cs
//  Velká mapa, kterou si hráč otevře v lodi klávesou M (P1) / Numpad 2 (P2) —
//  ale jen když má koupenou "Mapu" v obchodě s vylepšeními.
//
//  • Uprostřed jsi ty, kolem se kreslí prozkoumané okolí stejně jako na minimapě
//    (co je v mlze, to je šedé). Vidíš, kde jsi už byl a kde jsou ostrovy.
//  • Tažením myši mapou se posouváš, kolečkem přibližuješ/oddaluješ.
//  • Klik do mapy = položíš si cíl (waypoint). Na minimapě tě k němu pak navádí
//    azurová šipka. Klik na cíl znovu (nebo na sebe) ho zruší.
//
//  Objekt se vytváří sám (MapScreen.Toggle). Nic se nezapojuje ve scéně.
//  Hru NEpauzuje (kvůli split screenu) — jen zmrazí toho hráče, co ji má
//  otevřenou (PlayerController se dívá na MapScreen.IsOpenFor).
// ─────────────────────────────────────────────────────────────────────────────

public class MapScreen : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    private static MapScreen instance;

    /// <summary>Má tenhle hráč zrovna otevřenou mapu? (blokuje mu pohyb)</summary>
    public static bool IsOpenFor(int playerIndex)
        => IsOpen && instance != null && instance.owner == playerIndex;

    private const int   TEX = 360;          // velikost textury mapy v pixelech
    private const float MIN_ZOOM = 0.18f;   // políček na pixel (přiblíženo)
    private const float MAX_ZOOM = 3.0f;    // políček na pixel (oddáleno)

    private int         owner;              // 0 = P1, 1 = P2
    private GridManager grid;
    private Texture2D   tex;
    private Color[]     buf;
    private Vector2     centerTile;         // souřadnice políčka uprostřed mapy
    private float       zoom = 0.45f;       // políček na jeden pixel
    private bool        dirty = true;

    private bool    dragging;
    private Vector2 dragLast;
    private bool    draggedFar;             // odlišení "táhnutí" od "kliknutí"

    private GUIStyle titleStyle, hintStyle;

    // Barvy — sladěné s minimapou.
    private static readonly Color CWater  = new Color(0.16f, 0.55f, 0.72f);
    private static readonly Color CFish   = new Color(0.12f, 0.40f, 0.85f);
    private static readonly Color CTreas  = new Color(0.95f, 0.65f, 0.10f);
    private static readonly Color CLand   = new Color(0.30f, 0.62f, 0.28f);
    private static readonly Color CPier   = new Color(0.12f, 0.12f, 0.12f);
    private static readonly Color CLight  = new Color(1f,    0.30f, 0.24f);
    private static readonly Color CChest  = new Color(1f,    0.78f, 0.18f);
    private static readonly Color CFog    = new Color(0.24f, 0.26f, 0.30f);

    /// <summary>Otevře / zavře mapu pro daného hráče.</summary>
    public static void Toggle(int playerIndex, GridManager g)
    {
        if (IsOpen)
        {
            if (instance != null && instance.owner == playerIndex) Close();
            return; // druhý hráč už mapu otevřenou má — počkej, až ji zavře
        }

        if (instance == null)
            instance = new GameObject("MapScreen").AddComponent<MapScreen>();

        instance.owner = playerIndex;
        instance.grid  = g;

        var d = g.gameData;
        instance.centerTile = new Vector2(
            playerIndex == 0 ? d.playerGridX : d.player2GridX,
            playerIndex == 0 ? d.playerGridY : d.player2GridY);
        instance.zoom  = 0.45f;
        instance.dirty = true;
        IsOpen = true;
    }

    public static void Close() => IsOpen = false;

    void OnGUI()
    {
        if (!IsOpen || grid == null) return;
        EnsureStyles();

        // Kde se mapa kreslí: sólo = celá obrazovka, split screen = půlka hráče.
        float halfX = 0f, halfW = Screen.width;
        if (MultiplayerManager.IsMultiplayer)
        {
            halfW = Screen.width * 0.5f;
            halfX = owner == 1 ? Screen.width * 0.5f : 0f;
        }

        // Tmavé pozadí.
        GUI.color = new Color(0.03f, 0.05f, 0.08f, 0.96f);
        GUI.DrawTexture(new Rect(halfX, 0, halfW, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float side = Mathf.Min(halfW - 60f, Screen.height - 150f);
        var mapRect = new Rect(halfX + (halfW - side) / 2f, 90f, side, side);

        HandleInput(mapRect);

        if (dirty) Rebuild();
        GUI.DrawTexture(mapRect, tex, ScaleMode.StretchToFill, false);

        // Rámeček mapy.
        DrawFrame(mapRect, 3f, new Color(0.55f, 0.42f, 0.24f));

        // Titulek + nápověda.
        GUI.Label(new Rect(halfX, 34f, halfW, 34f), "MAPA", titleStyle);
        GUI.Label(new Rect(halfX, mapRect.yMax + 10f, halfW, 26f),
            "táhni myší = posun    kolečko = přiblížení    klik = cíl", hintStyle);

        bool hasWp = owner == 0 ? grid.gameData.hasWaypoint : grid.gameData.player2HasWaypoint;
        GUI.Label(new Rect(halfX, mapRect.yMax + 34f, halfW, 24f),
            hasWp ? "cíl nastaven — na minimapě tě k němu vede azurová šipka  (M / Numpad2 zavře)"
                  : "klikni do mapy a nastav si cíl  (M / Numpad2 zavře)", hintStyle);
    }

    // ── Vstup (tažení / zoom / klik) ─────────────────────────────────────────
    void HandleInput(Rect mapRect)
    {
        Event e = Event.current;
        if (e == null) return;

        // Zavření: Esc (P1) / NumpadEnter (P2) — spotřebuj, ať nevyskočí pauza.
        if (e.type == EventType.KeyDown &&
            ((owner == 0 && e.keyCode == KeyCode.Escape) ||
             (owner == 1 && e.keyCode == KeyCode.KeypadEnter)))
        {
            Close();
            e.Use();
            return;
        }

        if (e.type == EventType.ScrollWheel && mapRect.Contains(e.mousePosition))
        {
            zoom = Mathf.Clamp(zoom * (1f + e.delta.y * 0.08f), MIN_ZOOM, MAX_ZOOM);
            dirty = true;
            e.Use();
        }

        if (e.type == EventType.MouseDown && e.button == 0 && mapRect.Contains(e.mousePosition))
        {
            dragging   = true;
            dragLast   = e.mousePosition;
            draggedFar = false;
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && dragging)
        {
            Vector2 dd = e.mousePosition - dragLast;
            dragLast = e.mousePosition;
            // Táhnutí doprava → mapa jede doprava (díváš se víc doleva).
            centerTile += new Vector2(-dd.x, dd.y) * zoom;
            if (dd.magnitude > 2f) draggedFar = true;
            dirty = true;
            e.Use();
        }
        else if (e.type == EventType.MouseUp && e.button == 0 && dragging)
        {
            dragging = false;
            if (!draggedFar && mapRect.Contains(e.mousePosition))
                SetWaypointFromScreen(e.mousePosition, mapRect);
            e.Use();
        }
    }

    // Klik do mapy → přepočet na políčko → nastav / zruš cíl.
    void SetWaypointFromScreen(Vector2 mouse, Rect mapRect)
    {
        float sx = (mouse.x - mapRect.x) / mapRect.width;         // 0..1 zleva
        float sy = 1f - (mouse.y - mapRect.y) / mapRect.height;   // 0..1 zdola (obraceně než GUI)

        int tileX = Mathf.FloorToInt(centerTile.x + (sx - 0.5f) * TEX * zoom);
        int tileY = Mathf.FloorToInt(centerTile.y + (sy - 0.5f) * TEX * zoom);

        var d = grid.gameData;
        bool hadWp = owner == 0 ? d.hasWaypoint : d.player2HasWaypoint;
        int  wx    = owner == 0 ? d.waypointX   : d.player2WaypointX;
        int  wy    = owner == 0 ? d.waypointY   : d.player2WaypointY;

        // Klik na stávající cíl (±1) = zrušení.
        bool clear = hadWp && Mathf.Abs(tileX - wx) <= 1 && Mathf.Abs(tileY - wy) <= 1;

        if (owner == 0)
        {
            d.hasWaypoint = !clear;
            d.waypointX   = tileX;
            d.waypointY   = tileY;
        }
        else
        {
            d.player2HasWaypoint = !clear;
            d.player2WaypointX   = tileX;
            d.player2WaypointY   = tileY;
        }

        SoundManager.PlayClick();
        grid.Save();
        grid.NotifyWorldChanged();
    }

    // ── Vykreslení mapy do textury ──────────────────────────────────────────
    void Rebuild()
    {
        if (tex == null)
        {
            tex = new Texture2D(TEX, TEX, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            buf = new Color[TEX * TEX];
        }

        var d = grid.gameData;

        for (int py = 0; py < TEX; py++)
        {
            float wy = centerTile.y + (py - TEX * 0.5f) * zoom;
            int ty = Mathf.FloorToInt(wy);

            for (int px = 0; px < TEX; px++)
            {
                float wx = centerTile.x + (px - TEX * 0.5f) * zoom;
                int tx = Mathf.FloorToInt(wx);
                buf[py * TEX + px] = TileColor(tx, ty);
            }
        }

        // Značky.
        StampCross(d.playerGridX, d.playerGridY, Color.white, 3);
        if (MultiplayerManager.IsMultiplayer)
            StampCross(d.player2GridX, d.player2GridY, new Color(1f, 0.5f, 0f), 3);

        bool hasWp = owner == 0 ? d.hasWaypoint : d.player2HasWaypoint;
        if (hasWp)
        {
            int wx = owner == 0 ? d.waypointX : d.player2WaypointX;
            int wy = owner == 0 ? d.waypointY : d.player2WaypointY;
            StampCross(wx, wy, new Color(0.3f, 0.95f, 0.95f), 4);
        }

        tex.SetPixels(buf);
        tex.Apply(false);
        dirty = false;
    }

    Color TileColor(int x, int y)
    {
        string key = x + "," + y;
        if (!grid.gameData.tileData.TryGetValue(key, out TileStatus st) || !st.isExplored)
            return CFog;

        switch ((TileType)st.type)
        {
            case TileType.Water:      return CWater;
            case TileType.Water_Fish: return CFish;
            case TileType.Treasure:   return CTreas;
            case TileType.Harbor:     return CLand;
            case TileType.Pier:       return CPier;
            case TileType.Lighthouse: return CLight;
            case TileType.Chest:      return CChest;
            default:                  return CWater;
        }
    }

    // Křížek na políčku [tileX,tileY] o poloměru r pixelů.
    void StampCross(int tileX, int tileY, Color c, int r)
    {
        int cx = Mathf.RoundToInt(TEX * 0.5f + (tileX + 0.5f - centerTile.x) / zoom);
        int cy = Mathf.RoundToInt(TEX * 0.5f + (tileY + 0.5f - centerTile.y) / zoom);

        for (int i = -r; i <= r; i++)
        {
            Plot(cx + i, cy, c);
            Plot(cx, cy + i, c);
        }
    }

    void Plot(int px, int py, Color c)
    {
        if (px < 0 || px >= TEX || py < 0 || py >= TEX) return;
        buf[py * TEX + px] = c;
    }

    // ── Pomůcky ─────────────────────────────────────────────────────────────
    void DrawFrame(Rect r, float t, Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(new Rect(r.x - t, r.y - t, r.width + 2 * t, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x - t, r.yMax, r.width + 2 * t, t), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.x - t, r.y, t, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax, r.y, t, r.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void EnsureStyles()
    {
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.9f, 0.65f) }
        };
        hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.75f, 0.78f, 0.82f) }
        };
    }
}
