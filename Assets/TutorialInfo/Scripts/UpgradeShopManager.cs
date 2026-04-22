using UnityEngine;

[DefaultExecutionOrder(100)]
public class UpgradeShopManager : MonoBehaviour
{
    public int speedUpgradeCost = 150;
    public int rodUpgradeCost = 100;
    public int miningUpgradeCost = 120;

    private GridManager gridManager;
    private bool isOpen;

    public bool IsOpen => isOpen;
    public static bool AnyShopOpen;

    private GUIStyle titleStyle;
    private GUIStyle rowStyle;
    private GUIStyle ownedStyle;
    private GUIStyle buyStyle;
    private GUIStyle closeStyle;
    private GUIStyle coinsStyle;
    private bool stylesReady;

    void Start()
    {
        gridManager = FindFirstObjectByType<GridManager>();
    }

    public void Open() { isOpen = true; AnyShopOpen = true; }

    void Update()
    {
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            isOpen = false;
            AnyShopOpen = false;
        }
    }

    private void InitStyles()
    {
        if (stylesReady) return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        rowStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
        };
        ownedStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.4f, 1f, 0.4f) }
        };
        buyStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = MakeTex(new Color(0.2f, 0.5f, 0.2f)) },
            hover = { textColor = Color.white, background = MakeTex(new Color(0.3f, 0.65f, 0.3f)) }
        };
        closeStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 15,
            normal = { textColor = Color.white, background = MakeTex(new Color(0.5f, 0.1f, 0.1f)) },
            hover = { textColor = Color.white, background = MakeTex(new Color(0.7f, 0.15f, 0.15f)) }
        };
        coinsStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.85f, 0.2f) }
        };

        stylesReady = true;
    }

    private Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }

    private bool TryBuy(ref bool flag, int cost)
    {
        if (flag || gridManager.gameData.coins < cost) return false;
        gridManager.gameData.coins -= cost;
        flag = true;
        gridManager.Save();
        gridManager.NotifyWorldChanged();
        return true;
    }

    void OnGUI()
    {
        if (!isOpen) return;
        InitStyles();

        // Fullscreen dark overlay
        GUI.color = new Color(0, 0, 0, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float w = 560, h = 370;
        float px = (Screen.width - w) / 2f;
        float py = (Screen.height - h) / 2f;

        // Panel background
        GUI.color = new Color(0.12f, 0.14f, 0.18f, 1f);
        GUI.DrawTexture(new Rect(px, py, w, h), Texture2D.whiteTexture);
        GUI.color = new Color(0.4f, 0.6f, 1f, 1f);
        GUI.DrawTexture(new Rect(px, py, w, 3), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(px + 25, py + 20, w - 50, h - 40));

        GUILayout.Label("OBCHOD S VYLEPSENIMI", titleStyle);
        GUILayout.Space(15);

        GameData d = gridManager.gameData;

        DrawRow("Rychlost lodi  —  pohyb 2x rychleji", speedUpgradeCost, d.hasSpeedUpgrade,
            () => TryBuy(ref gridManager.gameData.hasSpeedUpgrade, speedUpgradeCost));
        GUILayout.Space(8);
        DrawRow("Lepsi prud  —  chyta 2 ryby najednou", rodUpgradeCost, d.hasRodUpgrade,
            () => TryBuy(ref gridManager.gameData.hasRodUpgrade, rodUpgradeCost));
        GUILayout.Space(8);
        DrawRow("Rychlost tezby  —  tezba 2x rychleji", miningUpgradeCost, d.hasMiningUpgrade,
            () => TryBuy(ref gridManager.gameData.hasMiningUpgrade, miningUpgradeCost));

        GUILayout.Space(18);
        GUILayout.Label($"Mince: {d.coins}", coinsStyle);
        GUILayout.EndArea();
    }

    private void DrawRow(string label, int cost, bool owned, System.Action onBuy)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, rowStyle, GUILayout.ExpandWidth(true));
        if (owned)
            GUILayout.Label("Zakoupeno", ownedStyle, GUILayout.Width(120));
        else
        {
            GUILayout.Label($"{cost} minci", rowStyle, GUILayout.Width(90));
            GUI.enabled = gridManager.gameData.coins >= cost;
            if (GUILayout.Button("Koupit", buyStyle, GUILayout.Width(90), GUILayout.Height(28)))
                onBuy();
            GUI.enabled = true;
        }
        GUILayout.EndHorizontal();
    }
}
