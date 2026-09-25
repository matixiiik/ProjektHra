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
    }

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
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // vestavěný font
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
        text.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
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
        int fish     = playerIndex == 0 ? d.fishCount     : d.player2FishCount;
        int treasure = playerIndex == 0 ? d.treasureCount : d.player2TreasureCount;
        int coins    = playerIndex == 0 ? d.coins         : d.player2Coins;
        ActiveQuest q = playerIndex == 0 ? d.activeQuest   : d.player2ActiveQuest;

        MegaQuest mq = playerIndex == 0 ? d.megaQuest : d.player2MegaQuest;

        int gx = playerIndex == 0 ? d.playerGridX : d.player2GridX;
        int gy = playerIndex == 0 ? d.playerGridY : d.player2GridY;

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
