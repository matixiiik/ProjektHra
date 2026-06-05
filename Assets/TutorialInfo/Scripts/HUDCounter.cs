using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class HUDCounter : MonoBehaviour
{
    [HideInInspector] public int playerIndex = 0;

    private GridManager grid;
    private Text fishText;
    private Text treasureText;
    private Text coinsText;
    private GameObject questPanel;
    private Text questLine;

    private List<RectTransform> rowRTs      = new List<RectTransform>();
    private RectTransform       questPanelRT;

    void Start()
    {
        grid = FindFirstObjectByType<GridManager>();
        BuildHUD();
        grid.OnWorldChanged += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        if (grid != null) grid.OnWorldChanged -= Refresh;
    }

    void BuildHUD()
    {
        var canvasGO = new GameObject("HUDCanvas");
        var canvas   = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode        = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasGO.AddComponent<GraphicRaycaster>();

        fishText     = MakeRow(canvasGO.transform, 0, new Color(0.3f, 0.8f, 1f));
        treasureText = MakeRow(canvasGO.transform, 1, new Color(1f, 0.85f, 0.2f));
        coinsText    = MakeRow(canvasGO.transform, 2, new Color(0.9f, 0.7f, 0.1f));

        BuildQuestPanel(canvasGO.transform);
        questPanel.SetActive(false);
    }

    void BuildQuestPanel(Transform parent)
    {
        questPanel = new GameObject("QuestPanel");
        questPanel.transform.SetParent(parent, false);

        questPanelRT            = questPanel.AddComponent<RectTransform>();
        questPanelRT.anchorMin  = new Vector2(0.5f, 1f);
        questPanelRT.anchorMax  = new Vector2(0.5f, 1f);
        questPanelRT.pivot      = new Vector2(0.5f, 1f);
        questPanelRT.anchoredPosition = new Vector2(0, -16f);
        questPanelRT.sizeDelta  = new Vector2(280f, 38f);

        var bg = new GameObject("BG");
        bg.transform.SetParent(questPanel.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0, 0, 0, 0.52f);

        var accent = new GameObject("Accent");
        accent.transform.SetParent(questPanel.transform, false);
        var acRt = accent.AddComponent<RectTransform>();
        acRt.anchorMin = new Vector2(0, 1); acRt.anchorMax = new Vector2(1, 1);
        acRt.pivot     = new Vector2(0.5f, 1f);
        acRt.sizeDelta = new Vector2(0, 3);
        accent.AddComponent<Image>().color = new Color(1f, 0.6f, 0.1f);

        questLine = MakeText(questPanel.transform,
            new Vector2(10, 0), new Vector2(-10, 0),
            Vector2.zero, Vector2.one,
            18f, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        questLine.supportRichText = true;
    }

    Text MakeText(Transform parent, Vector2 offsetMin, Vector2 offsetMax,
        Vector2 anchorMin, Vector2 anchorMax, float fontSize, Color color, FontStyle style, TextAnchor align)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        var t = go.AddComponent<Text>();
        t.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize  = (int)fontSize;
        t.fontStyle = style;
        t.color     = color;
        t.alignment = align;
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor    = new Color(0, 0, 0, 0.9f);
        shadow.effectDistance = new Vector2(1, -1);
        return t;
    }

    Text MakeRow(Transform parent, int index, Color color)
    {
        var go = new GameObject($"HUDRow{index}");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = Vector2.one;
        rt.pivot     = Vector2.one;
        rt.anchoredPosition = new Vector2(-20f, -20f - index * 40f);
        rt.sizeDelta = new Vector2(240f, 34f);
        rowRTs.Add(rt);

        var bg = new GameObject("BG");
        bg.transform.SetParent(go.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        bg.AddComponent<Image>().color = new Color(0, 0, 0, 0.45f);

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        var trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(8, 2);
        trt.offsetMax = new Vector2(-8, -2);

        var text = textGO.AddComponent<Text>();
        text.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize  = 22;
        text.fontStyle = FontStyle.Bold;
        text.color     = color;
        text.alignment = TextAnchor.MiddleRight;

        var shadow = textGO.AddComponent<Shadow>();
        shadow.effectColor    = new Color(0, 0, 0, 0.9f);
        shadow.effectDistance = new Vector2(1, -1);

        return text;
    }

    // ── Layout pro split screen ───────────────────────────────────────────────
    public void UpdateLayout(bool isSplit)
    {
        // P1 v split: kotva na (0.5,1) = pravý kraj levé poloviny
        // P2 vždy: (1,1) = pravý край pravé části / plné obrazovky
        float rowAnchorX = (playerIndex == 0 && isSplit) ? 0.5f : 1f;

        // Quest panel: P1 split → 25%, P2 → 75%, single → 50%
        float questAnchorX = (playerIndex == 0 && isSplit) ? 0.25f
                           : (playerIndex == 1)            ? 0.75f
                           : 0.5f;

        for (int i = 0; i < rowRTs.Count; i++)
        {
            rowRTs[i].anchorMin = rowRTs[i].anchorMax = new Vector2(rowAnchorX, 1f);
            rowRTs[i].pivot     = Vector2.one;
            rowRTs[i].anchoredPosition = new Vector2(-20f, -20f - i * 40f);
        }

        if (questPanelRT != null)
        {
            questPanelRT.anchorMin = questPanelRT.anchorMax = new Vector2(questAnchorX, 1f);
            questPanelRT.pivot     = new Vector2(0.5f, 1f);
            questPanelRT.anchoredPosition = new Vector2(0, -16f);
        }
    }

    // ── Data refresh ─────────────────────────────────────────────────────────
    void Refresh()
    {
        if (grid == null) return;
        GameData d = grid.gameData;

        int fish     = playerIndex == 0 ? d.fishCount     : d.player2FishCount;
        int treasure = playerIndex == 0 ? d.treasureCount : d.player2TreasureCount;
        int coins    = playerIndex == 0 ? d.coins         : d.player2Coins;
        ActiveQuest q = playerIndex == 0 ? d.activeQuest  : d.player2ActiveQuest;

        fishText.text     = $"Ryby: {fish}";
        treasureText.text = $"Poklady: {treasure}";
        coinsText.text    = $"Mince: {coins}";
        RefreshQuest(q);
    }

    void RefreshQuest(ActiveQuest q)
    {
        questPanel.SetActive(q.hasQuest);
        if (!q.hasQuest) return;

        if (q.IsComplete)
            questLine.text = $"<color=#ffcc00>{q.description}</color>  <color=#66ff66>SPLNENO!</color>";
        else
            questLine.text = $"<color=#ffcc00>{q.description}</color>  <color=#ffffff>{q.progress}/{q.target}</color>";
    }
}
