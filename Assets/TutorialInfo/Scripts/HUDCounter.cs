using UnityEngine;
using UnityEngine.UI;

public class HUDCounter : MonoBehaviour
{
    private GridManager grid;
    private Text fishText;
    private Text treasureText;
    private Text coinsText;

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
    }
}
