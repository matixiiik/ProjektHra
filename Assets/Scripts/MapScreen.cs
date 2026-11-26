using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  MapScreen.cs
//  Velká mapa, kterou si hráč otevře klávesou M (P1) / Numpad 2 (P2) — v lodi
//  i pěšky na ostrově, jen když má koupenou "Mapu" v obchodě s vylepšeními.
//
//  • Uprostřed jsi ty, kolem se kreslí prozkoumané okolí stejně jako na minimapě
//    (co je v mlze, to je šedé). Vidíš, kde jsi už byl a kde jsou ostrovy.
//  • Tažením myši mapou se posouváš, kolečkem přibližuješ/oddaluješ.
//  • Klik do mapy = položíš si cíl (waypoint). Na minimapě tě k němu pak navádí
//    azurová šipka. Klik na cíl znovu (nebo na sebe) ho zruší.
//
//  Vzhled: stará námořní mapa na pergamenu (stejný papír jako dopis a obchody,
//  HudSkin.Parchment). Neprozkoumaná místa jsou prázdný papír, prozkoumané moře
//  a ostrovy jsou "vybarvené". V záhlaví jsou souřadnice hráče, u kurzoru
//  souřadnice políčka (hádanka z dopisu se hledá právě podle nich).
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

    private const int   TEX_H = 360;        // výška textury mapy v pixelech (šířka podle poměru stran okna)
    private const float MIN_ZOOM = 0.18f;   // políček na pixel (přiblíženo)
    private const float MAX_ZOOM = 3.0f;    // políček na pixel (oddáleno)

    private int         owner;              // 0 = P1, 1 = P2
    private GridManager grid;
    private Texture2D   tex;
    private Color[]     buf;
    private int         texW = TEX_H;       // šířka textury — mapa je obdélník jako okno, ne čtverec
    private Vector2     centerTile;         // souřadnice políčka uprostřed mapy
    private float       zoom = 0.45f;       // políček na jeden pixel
    private bool        dirty = true;

    private bool    dragging;
    private Vector2 dragLast;
    private bool    draggedFar;             // odlišení "táhnutí" od "kliknutí"

    private GUIStyle titleStyle, hintStyle, compassStyle, coordStyle, posStyle;

    // Barvy "staré mapy" — tlumené, ať ladí s pergamenem (význam stejný jako
    // na minimapě). Mlha je průhledná → neprozkoumané místo = čistý papír.
    private static readonly Color CWater  = new Color(0.47f, 0.64f, 0.66f);
    private static readonly Color CFish   = new Color(0.33f, 0.50f, 0.64f);
    private static readonly Color CTreas  = new Color(0.86f, 0.55f, 0.16f);
    private static readonly Color CLand   = new Color(0.52f, 0.60f, 0.32f);
    private static readonly Color CPier   = new Color(0.34f, 0.22f, 0.12f);
    private static readonly Color CLight  = new Color(0.78f, 0.20f, 0.14f);
    private static readonly Color CChest  = new Color(0.90f, 0.70f, 0.18f);
    private static readonly Color CFog    = new Color(0f, 0f, 0f, 0f);

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
        instance.centerTile = new Vector2(d.players[playerIndex].gridX, d.players[playerIndex].gridY);
        instance.zoom  = 0.45f;
        instance.dirty = true;
        IsOpen = true;
    }

    public static void Close() => IsOpen = false;

    void OnGUI()
    {
        if (!IsOpen || grid == null) return;
        GUI.depth = -900; // nad HUD a ukazatelem zdraví bosse, pod dopisem
        HudSkin.UseUiFont();
        EnsureStyles();

        // Kde se mapa kreslí: sólo = celá obrazovka, split screen = půlka hráče.
        float halfX = 0f, halfW = Screen.width;
        if (MultiplayerManager.IsMultiplayer)
        {
            halfW = Screen.width * 0.5f;
            halfX = owner == 1 ? Screen.width * 0.5f : 0f;
        }

        // Ztmavení hry + velký pergamen přes skoro celou plochu hráče.
        GUI.color = new Color(0f, 0f, 0f, 0.82f);
        GUI.DrawTexture(new Rect(halfX, 0, halfW, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float s = Mathf.Max(0.75f, Mathf.Min(HudSkin.GuiScale, halfW / 700f));
        ApplyFontSizes(s);

        var sheet = new Rect(halfX + 20f, 16f, halfW - 40f, Screen.height - 32f);
        GUI.DrawTexture(sheet, HudSkin.Parchment(), ScaleMode.StretchToFill);

        // Plocha samotné mapy: pod nadpisem, nad nápovědou, s okrajem papíru kolem.
        float pad    = 34f * s;
        float header = 64f * s;
        float footer = 64f * s;
        var mapRect = new Rect(sheet.x + pad, sheet.y + header, sheet.width - 2f * pad, sheet.height - header - footer);

        HandleInput(mapRect);
        if (!IsOpen) return; // zavřeno klávesou v HandleInput

        if (dirty || NeedsResize(mapRect)) Rebuild(mapRect);
        GUI.DrawTexture(mapRect, tex, ScaleMode.StretchToFill, true); // alfa: mlha = vidět papír

        // Dvojitý inkoustový rámeček jako na staré mapě.
        DrawFrame(mapRect, 2f, HudSkin.InkTitle);
        DrawFrame(new Rect(mapRect.x - 6f, mapRect.y - 6f, mapRect.width + 12f, mapRect.height + 12f), 1f, HudSkin.InkFaint);

        // Světové strany u okrajů mapy (sladěno s minimapou: S nahoře, V vpravo).
        DrawCompass(mapRect);

        // Souřadnice políčka pod kurzorem — pomůže při hledání cíle podle
        // souřadnic (např. z dopisu v trezoru).
        DrawCursorCoords(mapRect);

        // Nadpis + vlastní pozice (stejně jako souřadnice v HUD při hraní).
        var ps = grid.gameData.players[owner];
        GUI.Label(new Rect(sheet.x, sheet.y + 10f * s, sheet.width, 42f * s), Loc.T("MAPA", "MAP"), titleStyle);
        GUI.Label(new Rect(sheet.x + pad, sheet.y + 14f * s, 320f * s, 36f * s),
                  Loc.T($"Ty:  X: {ps.gridX}   Y: {ps.gridY}", $"You:  X: {ps.gridX}   Y: {ps.gridY}"), posStyle);

        // Nápověda pod mapou.
        GUI.Label(new Rect(sheet.x, mapRect.yMax + 10f * s, sheet.width, 24f * s),
            Loc.T("táhni myší = posun    kolečko = přiblížení    klik = cíl",
                  "drag = pan    wheel = zoom    click = set target"), hintStyle);

        // Klávesy, které mapu zavírají, jsou pro každého hráče jiné.
        string closeKeys = owner == 0 ? "M / Esc" : "Numpad2 / NumpadEnter";
        GUI.Label(new Rect(sheet.x, mapRect.yMax + 32f * s, sheet.width, 24f * s),
            ps.hasWaypoint ? Loc.T($"cíl nastaven — vede tě k němu azurová šipka na minimapě  ({closeKeys} = zavřít)",
                                   $"target set — the cyan arrow on the minimap leads you there  ({closeKeys} = close)")
                           : Loc.T($"klikni do mapy a nastav si cíl  ({closeKeys} = zavřít)",
                                   $"click the map to set a target  ({closeKeys} = close)"), hintStyle);
    }

    // Velikost písma podle měřítka obrazovky (jako dopis a obchody).
    void ApplyFontSizes(float s)
    {
        titleStyle.fontSize   = Mathf.RoundToInt(30f * s);
        posStyle.fontSize     = Mathf.RoundToInt(17f * s);
        hintStyle.fontSize    = Mathf.RoundToInt(14f * s);
        coordStyle.fontSize   = Mathf.RoundToInt(16f * s);
        compassStyle.fontSize = Mathf.RoundToInt(19f * s);
    }

    // Textura musí mít stejný poměr stran jako okno mapy, jinak by se políčka roztáhla.
    int WantedTexWidth(Rect mapRect)
        => Mathf.Clamp(Mathf.RoundToInt(TEX_H * mapRect.width / Mathf.Max(1f, mapRect.height)), 160, 900);

    bool NeedsResize(Rect mapRect) => tex == null || tex.width != WantedTexWidth(mapRect);

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

    // Pozice myši na obrazovce → políčko světa (stejný přepočet pro klik i pro popisek).
    void ScreenToTile(Vector2 mouse, Rect mapRect, out int tileX, out int tileY)
    {
        float sx = (mouse.x - mapRect.x) / mapRect.width;         // 0..1 zleva
        float sy = 1f - (mouse.y - mapRect.y) / mapRect.height;   // 0..1 zdola (obraceně než GUI)

        tileX = Mathf.FloorToInt(centerTile.x + (sx - 0.5f) * texW  * zoom);
        tileY = Mathf.FloorToInt(centerTile.y + (sy - 0.5f) * TEX_H * zoom);
    }

    // Popisek "X: … Y: …" v rohu mapy, jen když je kurzor nad mapou.
    void DrawCursorCoords(Rect mapRect)
    {
        Event e = Event.current;
        if (e == null || !mapRect.Contains(e.mousePosition)) return;

        ScreenToTile(e.mousePosition, mapRect, out int tx, out int ty);

        // Papírový štítek u kurzoru (kousek vpravo dole, ať nezakrývá místo, kam míříš).
        float bw = coordStyle.fontSize * 9.5f, bh = coordStyle.fontSize * 1.8f;
        var box = new Rect(e.mousePosition.x + 18f, e.mousePosition.y + 18f, bw, bh);
        if (box.xMax > mapRect.xMax) box.x = e.mousePosition.x - 18f - bw;
        if (box.yMax > mapRect.yMax) box.y = e.mousePosition.y - 18f - bh;

        GUI.color = new Color(0.95f, 0.88f, 0.70f, 0.95f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(box, 1f, HudSkin.InkTitle);
        GUI.Label(box, $"X: {tx}   Y: {ty}", coordStyle);
    }

    // Klik do mapy → přepočet na políčko → nastav / zruš cíl.
    void SetWaypointFromScreen(Vector2 mouse, Rect mapRect)
    {
        ScreenToTile(mouse, mapRect, out int tileX, out int tileY);

        var ps = grid.gameData.players[owner];
        bool hadWp = ps.hasWaypoint;
        int  wx    = ps.waypointX;
        int  wy    = ps.waypointY;

        // Klik na stávající cíl (±1) = zrušení.
        bool clear = hadWp && Mathf.Abs(tileX - wx) <= 1 && Mathf.Abs(tileY - wy) <= 1;

        ps.hasWaypoint = !clear;
        ps.waypointX   = tileX;
        ps.waypointY   = tileY;

        SoundManager.PlayClick();
        grid.Save();
        grid.NotifyWorldChanged();
    }

    // ── Vykreslení mapy do textury ──────────────────────────────────────────
    void Rebuild(Rect mapRect)
    {
        if (NeedsResize(mapRect))
        {
            if (tex != null) Destroy(tex);
            texW = WantedTexWidth(mapRect);
            tex = new Texture2D(texW, TEX_H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            buf = new Color[texW * TEX_H];
        }

        var d = grid.gameData;

        for (int py = 0; py < TEX_H; py++)
        {
            float wy = centerTile.y + (py - TEX_H * 0.5f) * zoom;
            int ty = Mathf.FloorToInt(wy);

            for (int px = 0; px < texW; px++)
            {
                float wx = centerTile.x + (px - texW * 0.5f) * zoom;
                int tx = Mathf.FloorToInt(wx);
                buf[py * texW + px] = TileColor(tx, ty);
            }
        }

        // Značky.
        StampCross(d.players[0].gridX, d.players[0].gridY, new Color(0.70f, 0.10f, 0.08f), 5); // P1 = červený křížek jako "tady jsi" na staré mapě
        if (MultiplayerManager.IsMultiplayer)
            StampCross(d.players[1].gridX, d.players[1].gridY, new Color(0.85f, 0.45f, 0.05f), 5);

        var ownerPs = d.players[owner];
        if (ownerPs.hasWaypoint)
            StampCross(ownerPs.waypointX, ownerPs.waypointY, new Color(0.05f, 0.55f, 0.60f), 6);

        tex.SetPixels(buf);
        tex.Apply(false);
        dirty = false;
    }

    Color TileColor(int x, int y)
    {
        string key = x + "," + y;
        // Ukaž políčko, když ho hráč prozkoumal, NEBO patří ostrovu zahlédnutému
        // na dálku (st.mapped) — díky tomu na mapě "vykoukne" blízký ostrov,
        // i když u něj hráč fyzicky nebyl.
        if (!grid.gameData.tileData.TryGetValue(key, out TileStatus st) || (!st.isExplored && !st.mapped))
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
            case TileType.MegaIsland: return MegaIslandColor(x, y);
            default:                  return CWater;
        }
    }

    // Stejné pravidlo jako na minimapě (MinimapUIRenderer.MegaIslandColor) —
    // aktuální mega ostrov je červenohnědý, dokud na něm hráč nesloží obranu
    // (megaTask==0), pak fialový jako dřív. Staré opuštěné mega ostrovy
    // (tileData jim zůstává napořád) se podle vzdálenosti od storyIslandX/Y
    // nepletou s tím aktuálním.
    private static readonly Color CMegaSafe   = new Color(0.55f, 0.35f, 0.7f);
    private static readonly Color CMegaDanger = new Color(0.85f, 0.30f, 0.15f);

    Color MegaIslandColor(int x, int y)
    {
        GameData d = grid.gameData;
        if (!d.storyIslandActive || d.megaTask != 0) return CMegaSafe;

        int dx = x - d.storyIslandX, dy = y - d.storyIslandY;
        return dx * dx + dy * dy <= 22 * 22 ? CMegaDanger : CMegaSafe;
    }

    // Křížek na políčku [tileX,tileY] o poloměru r pixelů.
    void StampCross(int tileX, int tileY, Color c, int r)
    {
        int cx = Mathf.RoundToInt(texW  * 0.5f + (tileX + 0.5f - centerTile.x) / zoom);
        int cy = Mathf.RoundToInt(TEX_H * 0.5f + (tileY + 0.5f - centerTile.y) / zoom);

        // Křížek "×" o tloušťce 2 px — tenký by na velké mapě zanikl.
        for (int i = -r; i <= r; i++)
        {
            Plot(cx + i, cy + i, c); Plot(cx + i + 1, cy + i, c);
            Plot(cx + i, cy - i, c); Plot(cx + i + 1, cy - i, c);
        }
    }

    void Plot(int px, int py, Color c)
    {
        if (px < 0 || px >= texW || py < 0 || py >= TEX_H) return;
        buf[py * texW + px] = c;
    }

    // Písmena světových stran (S / J / V / Z) zevnitř u okrajů mapy.
    void DrawCompass(Rect m)
    {
        // Česky S/J/V/Z, anglicky N/S/E/W (pozor: "S" je v češtině sever, v angličtině jih).
        float r = compassStyle.fontSize * 1.5f; // velikost kolečka pro písmeno
        CompassLetter(Loc.T("S", "N"), new Rect(m.center.x - r / 2f, m.y + 4f,            r, r));
        CompassLetter(Loc.T("J", "S"), new Rect(m.center.x - r / 2f, m.yMax - r - 4f,     r, r));
        CompassLetter(Loc.T("V", "E"), new Rect(m.xMax - r - 4f,     m.center.y - r / 2f, r, r));
        CompassLetter(Loc.T("Z", "W"), new Rect(m.x + 4f,            m.center.y - r / 2f, r, r));
    }

    // Písmeno inkoustem na světlém papírovém štítku, ať je čitelné nad mořem i nad papírem.
    void CompassLetter(string s, Rect r)
    {
        GUI.color = new Color(0.95f, 0.88f, 0.70f, 0.90f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
        DrawFrame(r, 1f, HudSkin.InkFaint);
        GUI.Label(r, s, compassStyle);
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
            fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = HudSkin.InkTitle }
        };
        posStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft,
            normal = { textColor = HudSkin.Ink }
        };
        hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = HudSkin.InkFaint }
        };
        coordStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = HudSkin.Ink }
        };
        compassStyle = new GUIStyle(GUI.skin.label)
        {
            fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = HudSkin.InkTitle }
        };

        // Stejná barva i pod kurzorem myši (výchozí hover je světle šedý → na papíře by zmizel).
        foreach (var st in new[] { titleStyle, posStyle, hintStyle, coordStyle, compassStyle })
            HudSkin.InkAllStates(st, st.normal.textColor);
    }
}
