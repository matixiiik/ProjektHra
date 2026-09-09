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
    public int   borderPixels = 3;
    public Color borderColor  = new Color(0.72f, 0.55f, 0.30f, 1f); // teplý mosazný lem (ladí s HUD)

    [Header("Kompas k mega questu")]
    public Color compassColor = new Color(1f, 0.85f, 0.1f, 1f); // zlatá šipka k pokladu z mapy

    private GridManager grid;
    private Texture2D   tex;  // samotná textura minimapy
    private int         size; // šířka i výška textury v pixelech

    private RectTransform compassRT;      // zlatá šipka — směr k cíli mega questu
    private RectTransform waypointArrowRT; // azurová šipka — směr k cíli z velké mapy (waypoint)
    private static readonly Color WaypointArrowColor = new Color(0.3f, 0.9f, 0.9f, 1f);

    private RectTransform    playerArrowRT; // šipka uprostřed = kam míří loď / panáček
    private PlayerController  myPlayer;      // vlastní hráč (kvůli natočení šipky)

    // Health bary (loď + panáček) — děti minimapy, sedí přesně nad ní a jsou
    // stejně široké (takže se centrují na minimapu bez ohledu na škálování canvasu).
    private RectTransform boatHpFillRT, playerHpFillRT;
    private Text          boatHpLabel,  playerHpLabel;
    private static readonly Color HpBoatColor   = HudSkin.HpBoat;
    private static readonly Color HpPlayerColor = HudSkin.HpPlayer;
    private static readonly Color HpLowColor    = HudSkin.HpLow;

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

        CreateCompass();
        CreateWaypointArrow();
        CreatePlayerArrow();
        CreateCompassLabels();
        CreateHealthBars();

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

    // ── Health bary nad minimapou ───────────────────────────────────────────
    void CreateHealthBars()
    {
        // "Panáček" těsně nad minimapou, "Loď" nad ním.
        playerHpFillRT = MakeHpBar("HpPanacek", 4f,  HpPlayerColor, out playerHpLabel);
        boatHpFillRT   = MakeHpBar("HpLod",     24f, HpBoatColor,   out boatHpLabel);
    }

    // Jeden pruh: dítě minimapy, ukotvený k jejímu hornímu okraji, stejně široký.
    RectTransform MakeHpBar(string name, float yAboveMap, Color fillColor, out Text label)
    {
        var go = new GameObject(name);
        go.transform.SetParent(minimapImage.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.sizeDelta = new Vector2(0f, 17f);
        rt.anchoredPosition = new Vector2(0f, yAboveMap);

        var bg = new GameObject("BG");
        bg.transform.SetParent(go.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.sprite = HudSkin.Panel();
        bgImg.type   = Image.Type.Sliced;
        bgImg.raycastTarget = false;

        var fill = new GameObject("Fill");
        fill.transform.SetParent(go.transform, false);
        var fillRt = fill.AddComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0f, 0f);
        fillRt.anchorMax = new Vector2(1f, 1f); // šířka přes anchorMax.x v Refresh
        fillRt.offsetMin = new Vector2(2f, 2f);
        fillRt.offsetMax = new Vector2(0f, -2f);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = fillColor;
        fillImg.raycastTarget = false;

        var txtGo = new GameObject("Label");
        txtGo.transform.SetParent(go.transform, false);
        var txtRt = txtGo.AddComponent<RectTransform>();
        txtRt.anchorMin = Vector2.zero; txtRt.anchorMax = Vector2.one;
        txtRt.offsetMin = new Vector2(6f, 0f);
        txtRt.offsetMax = new Vector2(-4f, 0f);
        label = txtGo.AddComponent<Text>();
        label.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize  = 11;
        label.fontStyle = FontStyle.Bold;
        label.color     = Color.white;
        label.alignment = TextAnchor.MiddleLeft;
        label.raycastTarget = false;
        var sh = txtGo.AddComponent<Shadow>();
        sh.effectColor = new Color(0f, 0f, 0f, 0.9f);
        sh.effectDistance = new Vector2(1f, -1f);

        return fillRt;
    }

    void RefreshHealthBars()
    {
        GameData d = grid.gameData;
        int boatHp   = playerIndex == 0 ? d.boatHealth   : d.player2BoatHealth;
        int playerHp = playerIndex == 0 ? d.playerHealth : d.player2PlayerHealth;

        SetHpBar(boatHpFillRT,   boatHpLabel,   "Lod",     boatHp,   HpBoatColor);
        SetHpBar(playerHpFillRT, playerHpLabel, "Panacek", playerHp, HpPlayerColor);
    }

    void SetHpBar(RectTransform fillRT, Text label, string name, int hp, Color healthyColor)
    {
        if (fillRT == null) return;
        hp = Mathf.Clamp(hp, 0, 100);
        fillRT.anchorMax = new Vector2(hp / 100f, 1f);
        var img = fillRT.GetComponent<Image>();
        if (img != null) img.color = hp <= 30 ? HpLowColor : healthyColor;
        if (label != null) label.text = $"{name}  {hp}";
    }

    // ── Kompas k mega questu (zlatá šipka na okraji minimapy) ─────────────────
    void CreateCompass()
    {
        var arrowGO = new GameObject("MegaQuestCompass");
        arrowGO.transform.SetParent(minimapImage.transform, false);

        compassRT = arrowGO.AddComponent<RectTransform>();
        compassRT.sizeDelta   = new Vector2(16f, 16f);
        compassRT.anchorMin   = compassRT.anchorMax = new Vector2(0.5f, 0.5f);
        compassRT.pivot       = new Vector2(0.5f, 0.5f);
        compassRT.anchoredPosition = Vector2.zero;

        var img = arrowGO.AddComponent<Image>();
        img.sprite        = MakeArrowSprite();
        img.color         = compassColor;
        img.raycastTarget = false;

        arrowGO.SetActive(false);
    }

    // Azurová šipka k cíli (waypointu), který si hráč klikl na velké mapě.
    void CreateWaypointArrow()
    {
        var go = new GameObject("WaypointArrow");
        go.transform.SetParent(minimapImage.transform, false);

        waypointArrowRT = go.AddComponent<RectTransform>();
        waypointArrowRT.sizeDelta   = new Vector2(15f, 15f);
        waypointArrowRT.anchorMin   = waypointArrowRT.anchorMax = new Vector2(0.5f, 0.5f);
        waypointArrowRT.pivot       = new Vector2(0.5f, 0.5f);
        waypointArrowRT.anchoredPosition = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.sprite        = MakeArrowSprite();
        img.color         = WaypointArrowColor;
        img.raycastTarget = false;

        go.SetActive(false);
    }

    // Natočí a umístí azurovou šipku podle směru k waypointu z velké mapy.
    void UpdateWaypointArrow(bool hasWp, int wpX, int wpY, int cx, int cy)
    {
        if (waypointArrowRT == null) return;

        bool show = hasWp && (wpX != cx || wpY != cy);
        waypointArrowRT.gameObject.SetActive(show);
        if (!show) return;

        int dx = wpX - cx;
        int dy = wpY - cy;
        float bearing = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
        waypointArrowRT.localEulerAngles = new Vector3(0f, 0f, -bearing);

        float radius = minimapImage.rectTransform.rect.width * 0.5f - 22f; // o kousek blíž středu než kompas
        Vector2 dir = new Vector2(dx, dy).normalized;
        waypointArrowRT.anchoredPosition = dir * radius;
    }

    // Šipka uprostřed minimapy — ukazuje, kam je natočená loď / panáček (dá se
    // podle ní řídit). Otáčí ji Update().
    void CreatePlayerArrow()
    {
        var go = new GameObject("PlayerHeadingArrow");
        go.transform.SetParent(minimapImage.transform, false);

        playerArrowRT = go.AddComponent<RectTransform>();
        playerArrowRT.sizeDelta   = new Vector2(13f, 13f);
        playerArrowRT.anchorMin   = playerArrowRT.anchorMax = new Vector2(0.5f, 0.5f);
        playerArrowRT.pivot       = new Vector2(0.5f, 0.5f);
        playerArrowRT.anchoredPosition = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.sprite        = MakeArrowSprite();
        img.color         = playerColor;
        img.raycastTarget = false;
    }

    // Písmena světových stran (S / J / V / Z) při okraji minimapy zevnitř —
    // minimapa je v rohu obrazovky, ven by se místy nevešla.
    void CreateCompassLabels()
    {
        MakeCompassLabel("S", new Vector2(0.5f, 1f), new Vector2(0f,  -11f));
        MakeCompassLabel("J", new Vector2(0.5f, 0f), new Vector2(0f,   11f));
        MakeCompassLabel("V", new Vector2(1f, 0.5f), new Vector2(-11f,  0f));
        MakeCompassLabel("Z", new Vector2(0f, 0.5f), new Vector2( 11f,  0f));
    }

    void MakeCompassLabel(string letter, Vector2 anchor, Vector2 offset)
    {
        var go = new GameObject("Compass_" + letter);
        go.transform.SetParent(minimapImage.transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(16f, 16f);

        var txt = go.AddComponent<Text>();
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 13;
        txt.fontStyle = FontStyle.Bold;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = new Color(0.95f, 0.9f, 0.78f);
        txt.raycastTarget = false;

        var sh = go.AddComponent<Shadow>();
        sh.effectColor    = new Color(0f, 0f, 0f, 0.9f);
        sh.effectDistance = new Vector2(1f, -1f);
        txt.text = letter;
    }

    void Update()
    {
        if (playerArrowRT == null) return;

        if (myPlayer == null || !myPlayer)
        {
            foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (pc.playerIndex == playerIndex) { myPlayer = pc; break; }
        }
        if (myPlayer != null)
            playerArrowRT.localEulerAngles = new Vector3(0f, 0f, -myPlayer.HeadingDegrees);
    }

    // Vytvoří jednoduchou trojúhelníkovou šipku (mířící nahoru) jako sprite.
    Sprite MakeArrowSprite()
    {
        const int s = 20;
        Texture2D t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        t.filterMode = FilterMode.Bilinear;

        float center = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++)
        {
            // Nahoře (velké y) úzké, dole (malé y) široké — trojúhelník hrotem nahoru.
            float halfWidth = center * (1f - (float)y / (s - 1));
            for (int x = 0; x < s; x++)
            {
                bool inside = Mathf.Abs(x - center) <= halfWidth;
                t.SetPixel(x, y, inside ? Color.white : new Color(0f, 0f, 0f, 0f));
            }
        }
        t.Apply();

        return Sprite.Create(t, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
    }

    // Natočí a umístí šipku podle směru k rozdělanému (nevykopanému) mega questu.
    // Bez aktivního questu se šipka schová.
    void UpdateCompass(MegaQuest mq, int cx, int cy)
    {
        if (compassRT == null) return;

        bool show = mq != null && mq.active && !mq.dug && (mq.targetX != cx || mq.targetY != cy);
        compassRT.gameObject.SetActive(show);
        if (!show) return;

        int dx = mq.targetX - cx;
        int dy = mq.targetY - cy;

        // Sever (nahoru na minimapě) = 0°, ve směru hodinových ručiček k východu (doprava).
        float bearing = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
        compassRT.localEulerAngles = new Vector3(0f, 0f, -bearing);

        float radius = minimapImage.rectTransform.rect.width * 0.5f - 12f;
        Vector2 dir = new Vector2(dx, dy).normalized;
        compassRT.anchoredPosition = dir * radius;
    }

    // ── Překreslení textury ─────────────────────────────────────────────────
    void Refresh()
    {
        GameData d = grid.gameData;

        // Střed minimapy = pozice tohoto hráče.
        int cx = playerIndex == 0 ? d.playerGridX : d.player2GridX;
        int cy = playerIndex == 0 ? d.playerGridY : d.player2GridY;

        UpdateCompass(playerIndex == 0 ? d.megaQuest : d.player2MegaQuest, cx, cy);
        bool hasWp = playerIndex == 0 ? d.hasWaypoint : d.player2HasWaypoint;
        int  wpX   = playerIndex == 0 ? d.waypointX   : d.player2WaypointX;
        int  wpY   = playerIndex == 0 ? d.waypointY   : d.player2WaypointY;
        UpdateWaypointArrow(hasWp, wpX, wpY, cx, cy);
        RefreshHealthBars();

        // Projdi všechny pixely a obarvi je podle políčka, které leží pod nimi.
        for (int px = 0; px < size; px++)
            for (int py = 0; py < size; py++)
                tex.SetPixel(px, py, GetTileColor(cx + (px - viewRadius), cy + (py - viewRadius)));

        // Cíl z mapy (waypoint) — azurový bod, když je v dohledu minimapy.
        if (hasWp)
        {
            int wxp = wpX - cx + viewRadius;
            int wyp = wpY - cy + viewRadius;
            if (wxp >= 0 && wxp < size && wyp >= 0 && wyp < size)
                tex.SetPixel(wxp, wyp, WaypointArrowColor);
        }

        // Vlastní hráč = otáčivá šipka uprostřed (kreslí ji PlayerHeadingArrow,
        // ne textura) — sem jen ať střed nezůstane "prázdný" pod šipkou.

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

        MaskCircle();     // kulatá minimapa místo čtverce + kruhový rámeček
        tex.Apply(false); // promítni změny do textury
    }

    // Ořízne minimapu do kruhu (rohy zprůhlední) a po obvodu nakreslí rámeček.
    void MaskCircle()
    {
        float c  = (size - 1) * 0.5f;
        float rOuter = c;                              // vnější poloměr = okraj textury
        float rBorder = rOuter - Mathf.Clamp(borderPixels, 1, 8);

        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                if (d > rOuter)                         // roh mimo kruh → průhledné
                    tex.SetPixel(x, y, new Color(0, 0, 0, 0));
                else if (d > rBorder)                   // mezikruží → rámeček
                    tex.SetPixel(x, y, borderColor);
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
