using UnityEngine;
using UnityEngine.UI;

public class HUDCounter : MonoBehaviour
{
    private GridManager grid;
    private Text fishText;
    private Text treasureText;
    private Text coinsText;
    private GameObject questPanel;
    private Text questLine;

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
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
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
        var rt = questPanel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -16f);
        rt.sizeDelta = new Vector2(280f, 38f);

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
        acRt.pivot = new Vector2(0.5f, 1f);
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
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = (int)fontSize;
        t.fontStyle = style;
        t.color = color;
        t.alignment = align;
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.9f);
        shadow.effectDistance = new Vector2(1, -1);
        return t;
    }

    Text MakeRow(Transform parent, int index, Color color)
    {
        var go = new GameObject($"HUDRow{index}");
        go.transform.SetParent(parent, false);

        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = Vector2.one;
        rt.pivot = Vector2.one;
        rt.anchoredPosition = new Vector2(-20f, -20f - index * 40f);
        rt.sizeDelta = new Vector2(240f, 34f);

        var bg = new GameObject("BG");
        bg.transform.SetParent(go.transform, false);
        var bgRt = bg.AddComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
        var img = bg.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0.45f);

        var textGO = new GameObject("Label");
        textGO.transform.SetParent(go.transform, false);
        var trt = textGO.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = new Vector2(8, 2);
        trt.offsetMax = new Vector2(-8, -2);

        var text = textGO.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 22;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = TextAnchor.MiddleRight;

        var shadow = textGO.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.9f);
        shadow.effectDistance = new Vector2(1, -1);

        return text;
    }

    void Refresh()
    {
        if (grid == null) return;
        fishText.text     = $"Ryby: {grid.gameData.fishCount}";
        treasureText.text = $"Poklady: {grid.gameData.treasureCount}";
        coinsText.text    = $"Mince: {grid.gameData.coins}";
        RefreshQuest();
    }

    void RefreshQuest()
    {
        ActiveQuest q = grid.gameData.activeQuest;
        questPanel.SetActive(q.hasQuest);
        if (!q.hasQuest) return;

        if (q.IsComplete)
            questLine.text = $"<color=#ffcc00>{q.description}</color>  <color=#66ff66>SPLNENO!</color>";
        else
            questLine.text = $"<color=#ffcc00>{q.description}</color>  <color=#ffffff>{q.progress}/{q.target}</color>";
    }
}
