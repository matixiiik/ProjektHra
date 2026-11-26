using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  ShopUI.cs
//  Sdílený vzhled obchodů v majáku (IMGUI) — UpgradeShopManager i
//  QuestShopManager kreslí stejný pergamen jako dopis od bratra (HudSkin.
//  Parchment), píšou stejným inkoustem a mají stejná tlačítka. Dřív měl každý
//  obchod vlastní tmavý panel a vlastní kopii stylů; teď je to tady jednou.
//
//  Použití v OnGUI obchodu:
//      Rect inner = ShopUI.BeginSheet(who, 600f, 640f, out bool close);
//      ... GUILayout.BeginArea(inner) ... styly ShopUI.Row, ShopUI.Button ...
// ─────────────────────────────────────────────────────────────────────────────

public static class ShopUI
{
    // ── Styly (vytvoří se jednou, velikost písma se každý snímek přepočítá) ──
    public static GUIStyle Title, Subtitle, Section, Row, Faint, Owned, Warn, Coins;
    public static GUIStyle Button, ClaimButton;
    private static GUIStyle xStyle;

    /// <summary>
    /// Nakreslí ztmavení obrazovky hráče (ve split screenu jen jeho půlky) a
    /// pergamen uprostřed. Vrací vnitřní plochu pro GUILayout.BeginArea.
    /// `close` = true, když hráč klikl na zavírací křížek.
    /// </summary>
    public static Rect BeginSheet(int who, float baseW, float baseH, out bool close)
    {
        EnsureStyles();
        HudSkin.UseUiFont();
        GUI.depth = -900; // nad HUD a ukazatelem zdraví bosse (CombatDirector), pod dopisem (-1000)

        // Plocha hráče: sólo celá obrazovka, split screen jeho půlka.
        float sx = 0f, sw = Screen.width;
        if (MultiplayerManager.IsMultiplayer)
        {
            sw = Screen.width * 0.5f;
            sx = who == 1 ? sw : 0f;
        }

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(sx, 0f, sw, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Měřítko jako u dopisu (Full HD a víc = větší písmo), ale panel se
        // musí vejít na obrazovku i ve split screenu.
        float s = Scale(sw);
        float w = Mathf.Min(baseW * s, sw - 24f);
        float h = Mathf.Min(baseH * s, Screen.height - 24f);
        var sheet = new Rect(sx + (sw - w) / 2f, (Screen.height - h) / 2f, w, h);

        GUI.DrawTexture(sheet, HudSkin.Parchment(), ScaleMode.StretchToFill);
        ApplyFontSizes(s);

        close = CloseButton(sheet.x, sheet.y, sheet.width);

        float pad = 34f * s;
        return new Rect(sheet.x + pad, sheet.y + 22f * s, sheet.width - 2f * pad, sheet.height - 40f * s);
    }

    /// <summary>Připraví sdílené styly v daném měřítku pro jiné "papírové" okno (např. Deník),
    /// které si pergamen kreslí samo.</summary>
    public static void Prepare(float scale)
    {
        EnsureStyles();
        ApplyFontSizes(scale);
    }

    /// <summary>Měřítko okna obchodu: jako ostatní dialogy (HudSkin.GuiScale), ve split screenu menší.</summary>
    public static float Scale(float availableWidth)
    {
        float s = HudSkin.GuiScale;
        if (MultiplayerManager.IsMultiplayer) s = Mathf.Min(s, availableWidth / 640f);
        return Mathf.Max(0.75f, s);
    }

    /// <summary>Šířka sloupce/tlačítka v obchodě se stejným měřítkem jako písmo.</summary>
    public static float Px(float basePx) => basePx * currentScale;
    private static float currentScale = 1f;

    /// <summary>
    /// Posouvání sortimentu obchodu myší: kolečko řeší sám ScrollView, tohle
    /// navíc umožní scrollovat i tažením (podrž levé tlačítko a táhni myší
    /// nahoru/dolů). Volá se uvnitř OnGUI obchodu, `scroll` je pozice ScrollView.
    /// </summary>
    public static void HandleDragScroll(ref Vector2 scroll)
    {
        Event e = Event.current;
        if (e == null) return;

        if (e.type == EventType.MouseDrag && e.button == 0)
        {
            scroll.y += e.delta.y; // táhneš myš dolů → posuneš se na spodek nabídky
            e.Use();
        }
    }

    /// <summary>Nakreslí zavírací křížek v pravém horním rohu panelu obchodu
    /// (panel je Rect(panelX, panelY, panelW, ...)). Vrací true, když se klikne.</summary>
    public static bool CloseButton(float panelX, float panelY, float panelW)
    {
        EnsureStyles();
        float size = 30f * currentScale;
        var r = new Rect(panelX + panelW - size - 10f, panelY + 10f, size, size);
        return SoundManager.Click(GUI.Button(r, "X", xStyle));
    }

    // Písmo podle měřítka (styly se sdílí, takže stačí nastavit před kreslením).
    private static void ApplyFontSizes(float s)
    {
        currentScale = s;
        Title.fontSize       = Mathf.RoundToInt(26f * s);
        Subtitle.fontSize    = Mathf.RoundToInt(15f * s);
        Section.fontSize     = Mathf.RoundToInt(17f * s);
        Row.fontSize         = Mathf.RoundToInt(16f * s);
        Faint.fontSize       = Mathf.RoundToInt(15f * s);
        Owned.fontSize       = Mathf.RoundToInt(15f * s);
        Warn.fontSize        = Mathf.RoundToInt(15f * s);
        Coins.fontSize       = Mathf.RoundToInt(19f * s);
        Button.fontSize      = Mathf.RoundToInt(15f * s);
        ClaimButton.fontSize = Mathf.RoundToInt(16f * s);
        xStyle.fontSize      = Mathf.RoundToInt(15f * s);
    }

    private static void EnsureStyles()
    {
        if (Title != null) return;

        Title    = Label(HudSkin.InkTitle, FontStyle.Bold,   TextAnchor.MiddleCenter);
        Subtitle = Label(HudSkin.InkFaint, FontStyle.Italic, TextAnchor.MiddleCenter);
        Section  = Label(HudSkin.InkTitle, FontStyle.Bold,   TextAnchor.MiddleLeft);
        Row      = Label(HudSkin.Ink,      FontStyle.Normal, TextAnchor.MiddleLeft);
        Faint    = Label(HudSkin.InkFaint, FontStyle.Italic, TextAnchor.MiddleLeft);
        Owned    = Label(HudSkin.InkGreen, FontStyle.Bold,   TextAnchor.MiddleCenter);
        Warn     = Label(HudSkin.InkRed,   FontStyle.Italic, TextAnchor.MiddleLeft);
        Coins    = Label(new Color(0.55f, 0.36f, 0.05f), FontStyle.Bold, TextAnchor.MiddleCenter);

        // Tlačítka: tmavé dřevo s krémovým písmem (jako lem HUD), odměna zlatá.
        Button = ButtonStyle(new Color(0.36f, 0.22f, 0.10f), new Color(0.50f, 0.32f, 0.14f), HudSkin.TextWarm);
        ClaimButton = ButtonStyle(new Color(0.66f, 0.46f, 0.08f), new Color(0.82f, 0.58f, 0.12f), Color.white);
        xStyle = ButtonStyle(new Color(0.48f, 0.16f, 0.10f), new Color(0.68f, 0.22f, 0.14f), Color.white);
    }

    private static GUIStyle Label(Color color, FontStyle style, TextAnchor anchor)
    {
        var st = new GUIStyle(GUI.skin.label) { fontStyle = style, alignment = anchor, wordWrap = true };
        return HudSkin.InkAllStates(st, color); // i pod kurzorem myši zůstane inkoustová
    }

    private static GUIStyle ButtonStyle(Color normal, Color hover, Color text)
    {
        var st = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
        st.normal.background = Solid(normal);
        st.hover.background  = Solid(hover);
        st.active.background = Solid(hover);
        st.normal.textColor = st.hover.textColor = st.active.textColor = text;
        return st;
    }

    private static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}
