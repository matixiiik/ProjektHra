using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
//  HUDCounter.cs
//  Ukazatele v rohu obrazovky: počet ryb, pokladů, mincí + panel s aktivním
//  questem nahoře uprostřed.
//
//  Celé UI si skript staví sám kódem při startu (nevytváří se v editoru).
//  Překresluje se při každé změně světa (grid.OnWorldChanged).
//
//  V multiplayeru běží dvě kopie: P1 (playerIndex 0) a P2 (playerIndex 1).
//  UpdateLayout() přesouvá prvky podle toho, jestli je obrazovka rozdělená.
// ─────────────────────────────────────────────────────────────────────────────

public class HUDCounter : MonoBehaviour
{
    [HideInInspector] public int playerIndex = 0; // 0 = P1, 1 = P2

    // Rozměry horních panelů (v jednotkách canvasu 1920×1080).
    private const float QUEST_W_SOLO     = 460f;  // quest panel — celá obrazovka
    private const float QUEST_W_SPLIT    = 420f;  // quest panel — půlka obrazovky
    private const float QUEST_H_1LINE    = 50f;   // výška s jedním řádkem
    private const float QUEST_H_PER_LINE = 30f;   // každý další řádek
    private const float STORY_W_SOLO     = 640f;  // příběhový cíl (main quest)
    private const float STORY_W_SPLIT    = 470f;
    private const float STORY_H          = 66f;   // místo na dva řádky textu

    private GridManager grid;
    private Text        fishText;
    private Text        treasureText;
    private Text        coinsText;
    private Text        coordText;   // souřadnice hráče (levý horní roh)
    private GameObject  questPanel;
    private Text        questLine;
    private GameObject  storyPanel;   // příběhový cíl (starý námořník) — nahoře uprostřed
    private Text        storyLine;

    // Odkazy na RectTransformy prvků, abychom s nimi mohli hýbat při split screenu.
    private List<RectTransform> rowRTs = new List<RectTransform>();
    private RectTransform        questPanelRT;
    private RectTransform        storyPanelRT;
    private RectTransform        coordRT;

    // ── Hotbar (dole uprostřed): Zbraň · Boat ammo · Rifle ammo · Historický poklad ──
    private const float HOTBAR_SLOT = 88f;   // velikost jednoho políčka
    private const float HOTBAR_GAP  = 8f;    // mezera mezi políčky
    private RectTransform  hotbarRT;
    private Image[]        hbFrame = new Image[4]; // zlatý rámeček vybraného slotu
    private Image[]        hbBg    = new Image[4];
    private Image[]        hbIcon  = new Image[4];
    private Text[]         hbKey   = new Text[4];  // klávesa slotu (vlevo nahoře)
    private Text[]         hbCount = new Text[4];  // počet / název (dole)
    private PlayerController hbPlayer;             // hráč, kterému hotbar patří
    // Poslední zobrazený stav — hotbar se překresluje jen při změně.
    private int  hbLastBoatAmmo = -1, hbLastHandAmmo = -1, hbLastSlot = -99;
    private bool hbLastWeapon, hbLastCannon, hbLastTreasure, hbLastFoot, hbLastEn;
    private bool hbDirty = true;

    void Start()
    {
        grid = FindFirstObjectByType<GridManager>();
        if (grid == null)
        {
            Debug.LogError("HUDCounter: GridManager nenalezen.");
            enabled = false;
            return;
        }

        BuildHUD();

        // Ve split screenu rovnou přesuň prvky na svoji půlku (P2 HUD vzniká až
        // po zapnutí multiplayeru, tak si to musí udělat sám při Startu).
        if (MultiplayerManager.IsMultiplayer) UpdateLayout(true);

        grid.OnWorldChanged += Refresh; // překresli při každé změně
        Refresh();
    }

    void OnDestroy()
    {
        if (grid != null) grid.OnWorldChanged -= Refresh;
    }

    // ── Stavba UI ────────────────────────────────────────────────────────────
    void BuildHUD()
    {
        // Canvas přes celou obrazovku.
        var canvasGO = new GameObject("HUDCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        // Škálování podle rozlišení (návrh dělaný pro 1920×1080).
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGO.AddComponent<GraphicRaycaster>();

        // Tři řádky ukazatelů v pravém horním rohu (ikonka + hodnota). Munice
        // (lodní i pěší) se od teď ukazuje v hotbaru (PlayerController.OnGUI),
        // ne tady — tenhle roh je jen "ekonomika" (mince/ryby/poklady).
        fishText     = MakeRow(canvasGO.transform, 0, HudSkin.FishBlue,    HudSkin.IconKind.Fish);
        treasureText = MakeRow(canvasGO.transform, 1, HudSkin.TreasureTan, HudSkin.IconKind.Treasure);
        coinsText    = MakeRow(canvasGO.transform, 2, HudSkin.Gold,        HudSkin.IconKind.Coin);

        BuildCoordLabel(canvasGO.transform);

        BuildQuestPanel(canvasGO.transform);
        questPanel.SetActive(false); // schovaný, dokud hráč nemá quest

        BuildStoryPanel(canvasGO.transform);
        storyPanel.SetActive(false); // schovaný, dokud není příběhový cíl

        BuildHotbar(canvasGO.transform); // vidět od začátku hry, i když je zatím prázdný
    }

    // ── Hotbar ───────────────────────────────────────────────────────────────
    // Čtyři dřevěná políčka dole uprostřed: [1] zbraň, boat ammo, [2] rifle ammo,
    // [3] historický poklad. Munice jsou jen zobrazení (dvě oddělená streliva).
    void BuildHotbar(Transform parent)
    {
        var root = new GameObject("Hotbar");
        root.transform.SetParent(parent, false);
        hotbarRT = root.AddComponent<RectTransform>();
        hotbarRT.anchorMin = hotbarRT.anchorMax = new Vector2(0.5f, 0f);
        hotbarRT.pivot     = new Vector2(0.5f, 0f);
        hotbarRT.anchoredPosition = new Vector2(0f, 22f);
        hotbarRT.sizeDelta = new Vector2(4 * HOTBAR_SLOT + 3 * HOTBAR_GAP, HOTBAR_SLOT);

        HudSkin.IconKind[] icons =
        {
            HudSkin.IconKind.Rifle, HudSkin.IconKind.Cannonball,
            HudSkin.IconKind.Bullets, HudSkin.IconKind.Treasure,
        };

        for (int i = 0; i < 4; i++)
        {
            var slot = new GameObject($"Slot{i}");
            slot.transform.SetParent(root.transform, false);
            var srt = slot.AddComponent<RectTransform>();
            srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f);
            srt.pivot     = new Vector2(0f, 0.5f);
            srt.anchoredPosition = new Vector2(i * (HOTBAR_SLOT + HOTBAR_GAP), 0f);
            srt.sizeDelta = new Vector2(HOTBAR_SLOT, HOTBAR_SLOT);

            // Zlatý rámeček (jen u vybraného slotu) — o kousek větší než panel.
            hbFrame[i] = MakeSlotImage(slot.transform, "Frame", HudSkin.Panel(), Image.Type.Sliced, -5f);
            hbFrame[i].color   = HudSkin.Gold;
            hbFrame[i].enabled = false;

            // Dřevěný panel slotu.
            hbBg[i] = MakeSlotImage(slot.transform, "BG", HudSkin.Panel(), Image.Type.Sliced, 0f);

            // Ikona uprostřed, nad ní klávesa a pod ní počet.
            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(slot.transform, false);
            var irt = iconGO.AddComponent<RectTransform>();
            irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
            irt.pivot     = new Vector2(0.5f, 0.5f);
            irt.anchoredPosition = new Vector2(0f, 8f);
            irt.sizeDelta = new Vector2(52f, 52f);
            hbIcon[i] = iconGO.AddComponent<Image>();
            hbIcon[i].sprite = HudSkin.IconLarge(icons[i]);
            hbIcon[i].raycastTarget = false;

            hbKey[i] = MakeText(slot.transform, new Vector2(7, 0), new Vector2(-4, -4),
                Vector2.zero, Vector2.one, 16f, HudSkin.TextWarm, FontStyle.Bold, TextAnchor.UpperLeft);
            hbCount[i] = MakeText(slot.transform, new Vector2(2, 3), new Vector2(-2, 0),
                Vector2.zero, Vector2.one, 19f, Color.white, FontStyle.Bold, TextAnchor.LowerCenter);
        }
    }

    // Pomocná: jeden Image slotu roztažený na celý slot (s okrajem `grow` navíc).
    Image MakeSlotImage(Transform parent, string name, Sprite sprite, Image.Type type, float grow)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(grow, grow);
        rt.offsetMax = new Vector2(-grow, -grow);
        var img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.type   = type;
        img.raycastTarget = false;
        return img;
    }

    void Update()
    {
        RefreshHotbar(); // výběr slotu se mění klávesou bez události světa → kontrola každý snímek (levná)
    }

    // Přečte stav hráče a překreslí hotbar, jen když se něco změnilo.
    void RefreshHotbar()
    {
        if (hotbarRT == null || grid == null) return;

        if (hbPlayer == null)
            foreach (var pc in PlayerController.All)
                if (pc.playerIndex == playerIndex) { hbPlayer = pc; break; }

        GameData d = grid.gameData;
        PlayerState ps = d.players[playerIndex];
        bool onFoot    = hbPlayer != null ? hbPlayer.IsOnFoot : ps.isOnFoot;
        bool hasWeapon = ps.hasHandWeapon;
        bool hasCannon = BoatStats.HasCannon(ps.shipLevel);
        bool treasure  = d.hasHistoricalTreasure;
        int  boatAmmo  = ps.ammo;
        int  handAmmo  = ps.handAmmo;
        // V lodi nemá hráč nic v ruce (kód ovládání slot v lodi vynuluje).
        int  slot      = onFoot ? ps.activeHotbarSlot : -1;

        if (!hbDirty && hasWeapon == hbLastWeapon && hasCannon == hbLastCannon && treasure == hbLastTreasure
            && onFoot == hbLastFoot && boatAmmo == hbLastBoatAmmo && handAmmo == hbLastHandAmmo
            && slot == hbLastSlot && Loc.En == hbLastEn) return;

        hbDirty = false;
        hbLastWeapon = hasWeapon; hbLastCannon = hasCannon; hbLastTreasure = treasure; hbLastFoot = onFoot;
        hbLastBoatAmmo = boatAmmo; hbLastHandAmmo = handAmmo; hbLastSlot = slot; hbLastEn = Loc.En;

        // Vnitřní čísla slotů (activeHotbarSlot): 0 = zbraň, 1 = munice v ruce, 2 = poklad.
        // Políčka na obrazovce: 0 zbraň, 1 boat ammo, 2 rifle ammo, 3 poklad.
        int[] slotOfBox = { 0, -2, 1, 2 };            // -2 = boat ammo (nejde vybrat)
        bool[] owned    = { hasWeapon, hasCannon, hasWeapon, treasure };
        // Použitelnost teď: pěšky = zbraň/rifle/poklad, v lodi = jen boat ammo.
        bool[] usable   = { hasWeapon && onFoot, hasCannon && !onFoot, hasWeapon && onFoot, treasure && onFoot };
        string[] keys   = { Key(0), "", Key(1), Key(2) };
        string[] counts =
        {
            Loc.T("Zbraň", "Rifle"),
            hasCannon ? boatAmmo.ToString() : "–",
            hasWeapon ? handAmmo.ToString() : "–",
            Loc.T("Poklad", "Relic"),
        };

        for (int i = 0; i < 4; i++)
        {
            float a = usable[i] ? 1f : (owned[i] ? 0.55f : 0.28f);
            hbBg[i].color   = new Color(1f, 1f, 1f, usable[i] ? 1f : (owned[i] ? 0.8f : 0.5f));
            hbIcon[i].color = new Color(1f, 1f, 1f, a);
            hbCount[i].color = usable[i] ? Color.white : new Color(0.75f, 0.75f, 0.75f, 0.7f);
            hbKey[i].color   = new Color(HudSkin.TextWarm.r, HudSkin.TextWarm.g, HudSkin.TextWarm.b, usable[i] ? 1f : 0.5f);
            hbCount[i].text  = counts[i];
            hbKey[i].text    = keys[i];
            hbFrame[i].enabled = slotOfBox[i] >= 0 && slotOfBox[i] == slot;
        }
    }

    // Popisek klávesy slotu — P1 klávesnice 1/2/3, P2 horní řada numpadu 7/8/9.
    string Key(int slotNo) => playerIndex == 0 ? (slotNo + 1).ToString() : "Np" + (7 + slotNo);

    // Příběhový cíl — jeden řádek nahoře uprostřed pod quest panelem.
    void BuildStoryPanel(Transform parent)
    {
        storyPanel = new GameObject("StoryPanel");
        storyPanel.transform.SetParent(parent, false);

        storyPanelRT = storyPanel.AddComponent<RectTransform>();
        float ax = playerIndex == 1 ? 0.75f : 0.5f;
        storyPanelRT.anchorMin = storyPanelRT.anchorMax = new Vector2(ax, 1f);
        storyPanelRT.pivot     = new Vector2(0.5f, 1f);
        storyPanelRT.anchoredPosition = new Vector2(0f, -62f);
        storyPanelRT.sizeDelta        = new Vector2(STORY_W_SOLO, STORY_H);

        var bg = new GameObject("BG");
        bg.transform.SetParent(storyPanel.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.sprite = HudSkin.Panel();
        bgImg.type   = Image.Type.Sliced;

        storyLine = MakeText(storyPanel.transform,
            new Vector2(12, 0), new Vector2(-12, 0),
            Vector2.zero, Vector2.one,
            21f, new Color(1f, 0.9f, 0.65f), FontStyle.Bold, TextAnchor.MiddleCenter);
        storyLine.horizontalOverflow = HorizontalWrapMode.Wrap; // dlouhý úkol se zalomí na 2 řádky
    }

    // Příběhový panel pod quest panelem: pozice podle aktuální výšky quest panelu,
    // ať se nepřekrývají. Když quest panel není vidět, sedí těsně pod horním okrajem
    // (ne výš než souřadnice hráče vlevo nahoře).
    void PlaceStoryPanel()
    {
        if (storyPanelRT == null) return;
        float top = (questPanel != null && questPanel.activeSelf)
            ? 16f + questPanelRT.sizeDelta.y + 8f
            : 16f;
        storyPanelRT.anchoredPosition = new Vector2(0f, -Mathf.Max(62f, top));
    }

    // (Health bary lodě a hráče kreslí MinimapUIRenderer — sedí nad minimapou.)

    // Souřadnice hráče v levém horním rohu ("X: 5   Y: 8").
    void BuildCoordLabel(Transform parent)
    {
        var go = new GameObject("CoordLabel");
        go.transform.SetParent(parent, false);

        coordRT = go.AddComponent<RectTransform>();
        coordRT.anchorMin = coordRT.anchorMax = new Vector2(0f, 1f); // levý horní roh
        coordRT.pivot     = new Vector2(0f, 1f);
        coordRT.anchoredPosition = new Vector2(20f, -20f);
        coordRT.sizeDelta = new Vector2(200f, 34f);

        var bg = new GameObject("BG");
        bg.transform.SetParent(go.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        var coordBg = bg.AddComponent<Image>();
        coordBg.sprite = HudSkin.Panel();
        coordBg.type   = Image.Type.Sliced;

        coordText = MakeText(go.transform,
            new Vector2(12, 2), new Vector2(-10, -2),
            Vector2.zero, Vector2.one,
            22f, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
    }

    void BuildQuestPanel(Transform parent)
    {
        questPanel = new GameObject("QuestPanel");
        questPanel.transform.SetParent(parent, false);

        questPanelRT = questPanel.AddComponent<RectTransform>();
        // P2 je vždy v pravé polovině → kotva 0.75; P1 sám → 0.5 (UpdateLayout to případně přesune).
        float qax = playerIndex == 1 ? 0.75f : 0.5f;
        questPanelRT.anchorMin        = new Vector2(qax, 1f);
        questPanelRT.anchorMax        = new Vector2(qax, 1f);
        questPanelRT.pivot            = new Vector2(0.5f, 1f);
        questPanelRT.anchoredPosition = new Vector2(0, -16f);
        questPanelRT.sizeDelta        = new Vector2(QUEST_W_SOLO, QUEST_H_1LINE);

        // Dřevěný panel na pozadí.
        var bg = new GameObject("BG");
        bg.transform.SetParent(questPanel.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        var qBg = bg.AddComponent<Image>();
        qBg.sprite = HudSkin.Panel();
        qBg.type   = Image.Type.Sliced;

        // Oranžový proužek nahoře (kousek pod horním lemem panelu).
        var accent = new GameObject("Accent");
        accent.transform.SetParent(questPanel.transform, false);
        var acRt = accent.AddComponent<RectTransform>();
        acRt.anchorMin = new Vector2(0, 1); acRt.anchorMax = new Vector2(1, 1);
        acRt.pivot     = new Vector2(0.5f, 1f);
        acRt.offsetMin = new Vector2(10f, -7f);
        acRt.offsetMax = new Vector2(-10f, -4f);
        accent.AddComponent<Image>().color = new Color(1f, 0.6f, 0.1f);

        // Text questu.
        questLine = MakeText(questPanel.transform,
            new Vector2(10, 0), new Vector2(-10, 0),
            Vector2.zero, Vector2.one,
            21f, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        questLine.supportRichText = true;
        questLine.horizontalOverflow = HorizontalWrapMode.Wrap;
    }

    // Pomocná: vytvoří jeden Text s daným umístěním a stínem.
    Text MakeText(Transform parent, Vector2 offsetMin, Vector2 offsetMax,
        Vector2 anchorMin, Vector2 anchorMax, float fontSize, Color color, FontStyle style, TextAnchor align)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;

        var t = go.AddComponent<Text>();
        t.font      = HudSkin.UiFont != null ? HudSkin.UiFont
                    : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // systémové písmo, jinak vestavěné
        t.fontSize  = (int)fontSize;
        t.fontStyle = style;
        t.color     = color;
        t.alignment = align;

        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor    = new Color(0, 0, 0, 0.9f);
        shadow.effectDistance = new Vector2(1, -1);
        return t;
    }

    // Pomocná: vytvoří jeden řádek ukazatele (dřevěný panel + ikonka + hodnota).
    Text MakeRow(Transform parent, int index, Color color, HudSkin.IconKind icon)
    {
        var go = new GameObject($"HUDRow{index}");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = Vector2.one; // pravý horní roh
        rt.pivot     = Vector2.one;
        rt.anchoredPosition = new Vector2(-20f, -18f - index * 44f); // každý řádek o kus níž
        rt.sizeDelta = new Vector2(210f, 38f);
        rowRTs.Add(rt);

        // Zaoblený dřevěný panel na pozadí.
        var bg = new GameObject("BG");
        bg.transform.SetParent(go.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        var bgImg = bg.AddComponent<Image>();
        bgImg.sprite = HudSkin.Panel();
        bgImg.type   = Image.Type.Sliced;
        bgImg.color  = Color.white;

        // Ikonka vlevo.
        var iconGO = new GameObject("Icon");
        iconGO.transform.SetParent(go.transform, false);
        var iconRt = iconGO.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot     = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(7f, 0f);
        iconRt.sizeDelta = new Vector2(24f, 24f);
        var iconImg = iconGO.AddComponent<Image>();
        iconImg.sprite = HudSkin.Icon(icon);
        iconImg.raycastTarget = false;

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        var trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(36, 2);
        trt.offsetMax = new Vector2(-12, -2);

        var text = textGO.AddComponent<Text>();
        text.font      = HudSkin.UiFont != null ? HudSkin.UiFont
                       : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize  = 21;
        text.fontStyle = FontStyle.Bold;
        text.color     = color;
        text.alignment = TextAnchor.MiddleRight;

        var shadow = textGO.AddComponent<Shadow>();
        shadow.effectColor    = new Color(0, 0, 0, 0.9f);
        shadow.effectDistance = new Vector2(1, -1);

        return text;
    }

    // ── Přemístění prvků pro split screen ────────────────────────────────────
    public void UpdateLayout(bool isSplit)
    {
        // Řádky: P1 při splitu ke středu (0.5), jinak k pravému kraji (1).
        float rowAnchorX = (playerIndex == 0 && isSplit) ? 0.5f : 1f;

        // Quest panel: P1 split → 25 %, P2 → 75 %, jinak → 50 % šířky obrazovky.
        float questAnchorX = (playerIndex == 0 && isSplit) ? 0.25f
                           : (playerIndex == 1)            ? 0.75f
                           : 0.5f;

        for (int i = 0; i < rowRTs.Count; i++)
        {
            rowRTs[i].anchorMin = rowRTs[i].anchorMax = new Vector2(rowAnchorX, 1f);
            rowRTs[i].pivot     = Vector2.one;
            rowRTs[i].anchoredPosition = new Vector2(-20f, -18f - i * 44f);
        }

        if (questPanelRT != null)
        {
            questPanelRT.anchorMin = questPanelRT.anchorMax = new Vector2(questAnchorX, 1f);
            questPanelRT.pivot     = new Vector2(0.5f, 1f);
            questPanelRT.anchoredPosition = new Vector2(0, -16f);
            // V split screenu je půlka obrazovky užší → užší panel.
            questPanelRT.sizeDelta = new Vector2(isSplit ? QUEST_W_SPLIT : QUEST_W_SOLO, questPanelRT.sizeDelta.y);
        }

        // Hotbar: uprostřed dole na půlce svého hráče.
        if (hotbarRT != null)
        {
            hotbarRT.anchorMin = hotbarRT.anchorMax = new Vector2(questAnchorX, 0f);
            hotbarRT.pivot     = new Vector2(0.5f, 0f);
            hotbarRT.anchoredPosition = new Vector2(0f, 22f);
        }

        if (storyPanelRT != null)
        {
            storyPanelRT.anchorMin = storyPanelRT.anchorMax = new Vector2(questAnchorX, 1f);
            storyPanelRT.pivot     = new Vector2(0.5f, 1f);
            storyPanelRT.sizeDelta = new Vector2(isSplit ? STORY_W_SPLIT : STORY_W_SOLO, STORY_H);
            PlaceStoryPanel();
        }

        // Souřadnice: P1 vlevo nahoře (0), P2 při splitu na začátek pravé půlky (0.5).
        if (coordRT != null)
        {
            float coordAnchorX = (playerIndex == 1 && isSplit) ? 0.5f : 0f;
            coordRT.anchorMin = coordRT.anchorMax = new Vector2(coordAnchorX, 1f);
            coordRT.pivot     = new Vector2(0f, 1f);
            coordRT.anchoredPosition = new Vector2(20f, -20f);
        }
    }

    // ── Aktualizace textů ────────────────────────────────────────────────────
    void Refresh()
    {
        if (grid == null) return;
        GameData d = grid.gameData;

        // Vyber čísla podle toho, jestli jsme P1 nebo P2.
        PlayerState ps = d.players[playerIndex];
        int fish      = ps.fishCount;
        int treasure  = ps.treasureCount;
        int coins     = ps.coins;
        ActiveQuest q = ps.activeQuest;

        MegaQuest mq = ps.megaQuest;

        int gx = ps.gridX;
        int gy = ps.gridY;

        fishText.text     = Loc.T("Ryby: ",   "Fish: ")     + fish;
        treasureText.text = Loc.T("Poklady: ", "Treasure: ") + treasure;
        coinsText.text    = Loc.T("Mince: ",  "Coins: ")    + coins;
        coordText.text    = $"X: {gx}   Y: {gy}";
        RefreshQuest(q, mq);
        RefreshStory(d);
    }

    // Příběhový cíl podle gameData.storyStep.
    void RefreshStory(GameData d)
    {
        if (storyPanel == null) return;

        string txt = "";
        if (d.storyDone)
        {
            // Ostrov 3 dohraný (Krok 7) — storyStep dál neputuje, ať se cíl
            // nezasekne navěky na "probojuj se ostrovem 3".
            txt = "";
        }
        else switch (d.storyStep)
        {
            case 1:
                txt = Loc.T("Úkol: přines dědovi 1000 mincí a historický poklad",
                            "Quest: bring the old sailor 1,000 coins and a historic treasure");
                break;
            case 2:
                if (d.megaIndex == 1 && d.hasLetter)
                    // Souřadnice druhého ostrova jsou hádanka v dopise — HUD je neprozradí.
                    txt = Loc.T("Úkol: vyřeš hádanku z dopisu a najdi další ostrov (dopis je v Deníku)",
                                "Quest: solve the riddle in the letter and find the next island (the letter is in the Journal)");
                else
                    txt = Loc.T($"Úkol: dopluj k ostrovu na  [{d.storyIslandX}, {d.storyIslandY}]",
                                $"Quest: sail to the island at  [{d.storyIslandX}, {d.storyIslandY}]");
                break;
            case 3:
                // Názvy ostrovů stejné jako v deníku (JournalScreen).
                if (d.megaTask >= 3)
                    txt = Loc.T("Úkol: vrať se za dědou", "Quest: return to the old sailor");
                else if (d.megaIndex == 0)
                    txt = Loc.T("Úkol: probojuj se Pirátským ostrovem a najdi, co skrývá",
                                "Quest: fight your way across Pirate Island and find what it hides");
                else if (d.megaIndex == 1)
                    txt = Loc.T("Úkol: prozkoumej Hřbitov lodí a najdi, co skrývá",
                                "Quest: explore the Ship Graveyard and find what it hides");
                else
                    txt = Loc.T("Úkol: prozkoumej Poslední ostrov a najdi, co skrývá",
                                "Quest: explore the Final Island and find what it hides");
                break;
        }

        bool show = txt != "";
        storyPanel.SetActive(show);
        if (show) storyLine.text = txt;
    }

    void RefreshQuest(ActiveQuest q, MegaQuest mq)
    {
        bool show = q.hasQuest || mq.active;
        questPanel.SetActive(show);
        if (!show) { PlaceStoryPanel(); return; }

        var lines = new List<string>();

        if (q.hasQuest)
        {
            // Popis se skládá z typu a cíle (ne z textu uloženého v savu), ať sedí na jazyk.
            string desc = QuestShopManager.DescribeQuest(q.questType, q.target);
            lines.Add(q.IsComplete
                ? $"<color=#ffcc00>{desc}</color>  <color=#66ff66>" + Loc.T("SPLNĚNO!", "COMPLETE!") + "</color>"
                : $"<color=#ffcc00>{desc}</color>  <color=#ffffff>{q.progress}/{q.target}</color>");
        }

        if (mq.active)
        {
            // Mega quest se vyplácí ve VÝKUPNĚ v majáku (ne v obchodě s questy).
            lines.Add(mq.dug
                ? "<color=#66ff66>" + Loc.T("Poklad vykopán!", "Treasure dug up!") + "</color>  <color=#ffcc00>"
                    + Loc.T("Vyplať ho ve výkupně", "Cash it in at the trading post") + "</color>"
                : "<color=#ffcc00>" + Loc.T($"Mapa: poklad na [{mq.targetX}, {mq.targetY}]",
                                            $"Map: treasure at [{mq.targetX}, {mq.targetY}]") + "</color>");
        }

        questLine.text = string.Join("\n", lines.ToArray());

        // Panel povyroste podle počtu řádků a příběhový panel se posune pod něj.
        if (questPanelRT != null)
        {
            float h = QUEST_H_1LINE + (lines.Count - 1) * QUEST_H_PER_LINE;
            questPanelRT.sizeDelta = new Vector2(questPanelRT.sizeDelta.x, h);
            PlaceStoryPanel();
        }
    }
}
