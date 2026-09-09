using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  HudSkin.cs
//  Společná "grafika" HUD — panely a ikonky. Textury se negenerují ze souborů,
//  ale přímo v kódu (Texture2D + kreslení), aby se nemusela řešit licence ani
//  import. Stejný princip jako SoundManager u zvuků.
//
//  Styl: tmavé dřevo + teplý mosazný lem, ať to ladí ke Kenney grafice hry.
//  Vytvoří se jednou a sdílí (statické cache).
// ─────────────────────────────────────────────────────────────────────────────

public static class HudSkin
{
    // ── Paleta ────────────────────────────────────────────────────────────
    public static readonly Color PanelFill  = new Color(0.10f, 0.08f, 0.06f, 0.82f); // tmavé dřevo, průsvitné
    public static readonly Color PanelEdge   = new Color(0.72f, 0.55f, 0.30f, 1f);    // teplý mosazný lem
    public static readonly Color TextWarm    = new Color(0.97f, 0.93f, 0.84f, 1f);    // krémový text
    public static readonly Color Gold        = new Color(1f,    0.82f, 0.28f, 1f);
    public static readonly Color FishBlue    = new Color(0.45f, 0.80f, 1f,    1f);
    public static readonly Color TreasureTan = new Color(0.90f, 0.68f, 0.28f, 1f);
    public static readonly Color AmmoGrey    = new Color(0.80f, 0.82f, 0.88f, 1f);
    public static readonly Color HpBoat      = new Color(0.45f, 0.72f, 1f,    1f);
    public static readonly Color HpPlayer    = new Color(0.55f, 0.85f, 0.45f, 1f);
    public static readonly Color HpLow       = new Color(0.92f, 0.34f, 0.28f, 1f);

    // ── Panel (9-slice, zaoblené rohy + lem) ──────────────────────────────
    private static Sprite panelSprite;

    public static Sprite Panel()
    {
        if (panelSprite != null) return panelSprite;

        const int S = 48, R = 12, B = 3;
        var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };

        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float edgeDist = RoundedRectEdgeDistance(x + 0.5f, y + 0.5f, S, S, R);
            Color c;
            if (edgeDist < 0f)            c = new Color(0, 0, 0, 0);        // mimo panel
            else if (edgeDist < B)        c = PanelEdge;                    // lem
            else                          c = PanelFill;                    // výplň
            t.SetPixel(x, y, c);
        }
        t.Apply();

        panelSprite = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(R, R, R, R));
        return panelSprite;
    }

    // Kladná hodnota = vzdálenost bodu od nejbližšího okraje zaobleného
    // obdélníku (uvnitř), záporná = bod je venku.
    private static float RoundedRectEdgeDistance(float px, float py, float w, float h, float r)
    {
        float dx = Mathf.Min(px, w - px);
        float dy = Mathf.Min(py, h - py);

        // V rohové oblasti měř vzdálenost od středu rohového oblouku.
        if (dx < r && dy < r)
        {
            float cx = px < r ? r : w - r;
            float cy = py < r ? r : h - r;
            return r - Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
        }
        return Mathf.Min(dx, dy);
    }

    // ── Bílý sprite (pro Image.Type.Filled u health barů) ────────────────
    private static Sprite whiteSprite;
    public static Sprite White()
    {
        if (whiteSprite != null) return whiteSprite;
        var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        for (int i = 0; i < 16; i++) t.SetPixel(i % 4, i / 4, Color.white);
        t.Apply();
        whiteSprite = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
        return whiteSprite;
    }

    // ── Ikonky (28×28) ───────────────────────────────────────────────────
    public enum IconKind { Coin, Fish, Treasure, Ammo, Heart, Anchor }

    private static readonly System.Collections.Generic.Dictionary<IconKind, Sprite> iconCache
        = new System.Collections.Generic.Dictionary<IconKind, Sprite>();

    public static Sprite Icon(IconKind kind)
    {
        if (iconCache.TryGetValue(kind, out var s)) return s;

        const int S = 28;
        var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int i = 0; i < S * S; i++) t.SetPixel(i % S, i / S, new Color(0, 0, 0, 0));

        switch (kind)
        {
            case IconKind.Coin:     DrawCoin(t, S);     break;
            case IconKind.Fish:     DrawFish(t, S);     break;
            case IconKind.Treasure: DrawTreasure(t, S); break;
            case IconKind.Ammo:     DrawDisc(t, S, S * 0.32f, AmmoGrey); break;
            case IconKind.Heart:    DrawHeart(t, S);    break;
            case IconKind.Anchor:   DrawAnchor(t, S);   break;
        }
        t.Apply();

        s = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        iconCache[kind] = s;
        return s;
    }

    private static void DrawCoin(Texture2D t, int S)
    {
        DrawDisc(t, S, S * 0.40f, Gold);
        DrawRing(t, S, S * 0.28f, S * 0.36f, new Color(0.75f, 0.55f, 0.12f));
    }

    private static void DrawFish(Texture2D t, int S)
    {
        float cx = S * 0.55f, cy = S * 0.5f;
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float ex = (x - cx) / (S * 0.34f);
            float ey = (y - cy) / (S * 0.22f);
            bool body = ex * ex + ey * ey <= 1f;
            // ocas: trojúhelník vlevo
            bool tail = x < S * 0.28f && Mathf.Abs(y - cy) < (S * 0.28f - x) * 0.7f;
            if (body || tail) t.SetPixel(x, y, FishBlue);
        }
        // oko
        DrawDisc(t, S, 1.6f, new Color(0.05f, 0.1f, 0.2f), S * 0.70f, S * 0.56f);
    }

    private static void DrawTreasure(Texture2D t, int S)
    {
        Color wood = new Color(0.5f, 0.33f, 0.18f);
        for (int y = (int)(S * 0.24f); y < (int)(S * 0.72f); y++)
        for (int x = (int)(S * 0.14f); x < (int)(S * 0.86f); x++)
            t.SetPixel(x, y, wood);
        for (int x = (int)(S * 0.14f); x < (int)(S * 0.86f); x++)
            t.SetPixel(x, (int)(S * 0.47f), Gold);           // kovový pás
        DrawDisc(t, S, 1.8f, Gold, S * 0.5f, S * 0.47f);     // zámek
    }

    private static void DrawHeart(Texture2D t, int S)
    {
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float nx = (x - S * 0.5f) / (S * 0.42f);
            float ny = (S * 0.60f - y) / (S * 0.42f);
            float v = (nx * nx + ny * ny - 1f);
            if (v * v * v - nx * nx * ny * ny * ny <= 0f) t.SetPixel(x, y, HpLow);
        }
    }

    private static void DrawAnchor(Texture2D t, int S)
    {
        Color m = AmmoGrey;
        float mid = S * 0.5f;

        for (int y = (int)(S * 0.18f); y < (int)(S * 0.82f); y++)                 // svislý dřík
            for (int d = -1; d <= 1; d++) t.SetPixel((int)mid + d, y, m);

        for (int x = (int)(S * 0.30f); x < (int)(S * 0.70f); x++)                 // příčka nahoře
            t.SetPixel(x, (int)(S * 0.30f), m);

        DrawRing(t, S, 1.6f, 3.2f, m, mid, S * 0.20f);                            // ouško

        // Spodní oblouk (jen dolní polovina prstence).
        float cy = S * 0.62f, rIn = S * 0.24f, rOut = S * 0.34f;
        for (int y = 0; y < (int)cy; y++)
        for (int x = 0; x < S; x++)
        {
            float d2 = (x - mid) * (x - mid) + (y - cy) * (y - cy);
            if (d2 >= rIn * rIn && d2 <= rOut * rOut) t.SetPixel(x, y, m);
        }
    }

    // ── Kreslicí pomůcky ─────────────────────────────────────────────────
    private static void DrawDisc(Texture2D t, int S, float radius, Color c)
        => DrawDisc(t, S, radius, c, S * 0.5f, S * 0.5f);

    private static void DrawDisc(Texture2D t, int S, float radius, Color c, float cx, float cy)
    {
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
            if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= radius * radius)
                t.SetPixel(x, y, c);
    }

    private static void DrawRing(Texture2D t, int S, float rIn, float rOut, Color c)
        => DrawRing(t, S, rIn, rOut, c, S * 0.5f, S * 0.5f);

    private static void DrawRing(Texture2D t, int S, float rIn, float rOut, Color c, float cx, float cy)
    {
        for (int y = 0; y < S; y++)
        for (int x = 0; x < S; x++)
        {
            float d2 = (x - cx) * (x - cx) + (y - cy) * (y - cy);
            if (d2 >= rIn * rIn && d2 <= rOut * rOut) t.SetPixel(x, y, c);
        }
    }
}
