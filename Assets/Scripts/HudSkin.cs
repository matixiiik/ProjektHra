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

    // ── Písmo a měřítko pro IMGUI (OnGUI) ─────────────────────────────────
    private static Font uiFont;
    private static bool uiFontTried;

    /// <summary>Systémové písmo (Segoe UI → Calibri → Arial) — čitelnější než vestavěné.
    /// Když žádné není, vrací null a IMGUI zůstane u výchozího písma.</summary>
    public static Font UiFont
    {
        get
        {
            if (!uiFontTried)
            {
                uiFontTried = true;
                try { uiFont = Font.CreateDynamicFontFromOSFont(new[] { "Segoe UI", "Calibri", "Arial" }, 16); }
                catch (System.Exception) { uiFont = null; }
            }
            return uiFont;
        }
    }

    /// <summary>Nastaví systémové písmo jako výchozí pro celé IMGUI (volat z OnGUI;
    /// styly odvozené z GUI.skin ho pak použijí samy).</summary>
    public static void UseUiFont()
    {
        Font f = UiFont;
        if (f != null && GUI.skin.font != f) GUI.skin.font = f;
    }

    /// <summary>Měřítko IMGUI dialogů podle výšky obrazovky (900 px = 1×). Na malém okně
    /// v editoru zůstane 1, na Full HD / 4K text a okna narostou, ať se dobře čtou.</summary>
    public static float GuiScale => Mathf.Clamp(Screen.height / 900f, 1f, 2.2f);

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

    // ── Ikonky (28×28 malé, 64×64 velké pro hotbar) ──────────────────────
    public enum IconKind { Coin, Fish, Treasure, Ammo, Heart, Anchor, Rifle, Cannonball, Bullets }

    private static readonly System.Collections.Generic.Dictionary<IconKind, Sprite> iconCache
        = new System.Collections.Generic.Dictionary<IconKind, Sprite>();
    private static readonly System.Collections.Generic.Dictionary<IconKind, Sprite> iconLargeCache
        = new System.Collections.Generic.Dictionary<IconKind, Sprite>();

    public static Sprite Icon(IconKind kind) => GetIcon(kind, 28, iconCache);

    /// <summary>Velká verze ikony (64×64) — ostřejší při zvětšení, používá hotbar.</summary>
    public static Sprite IconLarge(IconKind kind) => GetIcon(kind, 64, iconLargeCache);

    // Vykreslí ikonu dané velikosti do textury a uloží do cache.
    private static Sprite GetIcon(IconKind kind, int S, System.Collections.Generic.Dictionary<IconKind, Sprite> cache)
    {
        if (cache.TryGetValue(kind, out var s)) return s;

        var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        for (int i = 0; i < S * S; i++) t.SetPixel(i % S, i / S, new Color(0, 0, 0, 0));

        switch (kind)
        {
            case IconKind.Coin:       DrawCoin(t, S);     break;
            case IconKind.Fish:       DrawFish(t, S);     break;
            case IconKind.Treasure:   DrawTreasure(t, S); break;
            case IconKind.Ammo:       DrawDisc(t, S, S * 0.32f, AmmoGrey); break;
            case IconKind.Heart:      DrawHeart(t, S);    break;
            case IconKind.Anchor:     DrawAnchor(t, S);   break;
            case IconKind.Rifle:      DrawRifle(t, S);    break;
            case IconKind.Cannonball: DrawCannonball(t, S); break;
            case IconKind.Bullets:    DrawBullets(t, S);  break;
        }
        t.Apply();

        s = Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        cache[kind] = s;
        return s;
    }

    // Puška: dřevěná pažba, tmavá spoušťová skříň a šedá hlaveň s muškou.
    private static void DrawRifle(Texture2D t, int S)
    {
        Color wood   = new Color(0.55f, 0.36f, 0.20f);
        Color metal  = new Color(0.62f, 0.64f, 0.70f);
        Color dark   = new Color(0.25f, 0.26f, 0.30f);

        FillRect(t, S, 0.06f, 0.38f, 0.34f, 0.58f, wood);   // pažba
        FillRect(t, S, 0.26f, 0.24f, 0.38f, 0.42f, wood);   // pistolová rukojeť
        FillRect(t, S, 0.32f, 0.44f, 0.58f, 0.62f, dark);   // spoušťová skříň
        FillRect(t, S, 0.58f, 0.51f, 0.94f, 0.57f, metal);  // hlaveň
        FillRect(t, S, 0.86f, 0.57f, 0.90f, 0.64f, metal);  // muška
        FillRect(t, S, 0.44f, 0.32f, 0.50f, 0.44f, dark);   // spoušťový krytec
    }

    // Dělová koule: tmavá koule s odleskem.
    private static void DrawCannonball(Texture2D t, int S)
    {
        DrawDisc(t, S, S * 0.36f, new Color(0.22f, 0.23f, 0.27f));
        DrawDisc(t, S, S * 0.09f, new Color(0.55f, 0.57f, 0.64f), S * 0.40f, S * 0.62f); // odlesk
    }

    // Náboje do pušky: dva mosazné náboje vedle sebe.
    private static void DrawBullets(Texture2D t, int S)
    {
        DrawOneBullet(t, S, 0.36f);
        DrawOneBullet(t, S, 0.64f);
    }

    private static void DrawOneBullet(Texture2D t, int S, float cx)
    {
        Color brass  = new Color(0.90f, 0.70f, 0.28f);
        Color casing = new Color(0.70f, 0.50f, 0.18f);
        Color tip    = new Color(0.72f, 0.45f, 0.22f);

        FillRect(t, S, cx - 0.09f, 0.18f, cx + 0.09f, 0.56f, brass);    // tělo nábojnice
        FillRect(t, S, cx - 0.09f, 0.18f, cx + 0.09f, 0.25f, casing);   // dno
        DrawDisc(t, S, S * 0.09f, tip, S * cx, S * 0.58f);              // zaoblená střela
    }

    // Vyplní obdélník zadaný zlomky velikosti textury (0..1).
    private static void FillRect(Texture2D t, int S, float x0, float y0, float x1, float y1, Color c)
    {
        for (int y = (int)(S * y0); y < (int)(S * y1); y++)
        for (int x = (int)(S * x0); x < (int)(S * x1); x++)
            t.SetPixel(x, y, c);
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
