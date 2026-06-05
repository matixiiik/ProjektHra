using UnityEngine;

[DefaultExecutionOrder(100)]
public class UpgradeShopManager : MonoBehaviour
{
    public int speedUpgradeCost  = 150;
    public int rodUpgradeCost    = 100;
    public int miningUpgradeCost = 120;
    public int shipMediumCost    = 300;
    public int shipLargeCost     = 800;

    private GridManager gridManager;
    private bool isOpen;
    private int  buyerIndex;

    public bool IsOpen => isOpen;
    public static bool AnyShopOpen;

    private GUIStyle titleStyle, rowStyle, ownedStyle, buyStyle, closeStyle, coinsStyle;
    private bool stylesReady;

    void Start() { gridManager = FindFirstObjectByType<GridManager>(); }

    public void Open(int playerIndex = 0)
    {
        buyerIndex  = playerIndex;
        isOpen      = true;
        AnyShopOpen = true;
    }

    void Update()
    {
        if (isOpen && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        { isOpen = false; AnyShopOpen = false; }
    }

    // ── Per-buyer helpers ─────────────────────────────────────────────────────
    int  Coins()         => buyerIndex == 0 ? gridManager.gameData.coins               : gridManager.gameData.player2Coins;
    void SetCoins(int v) { if (buyerIndex == 0) gridManager.gameData.coins = v;          else gridManager.gameData.player2Coins = v; }

    // upgradeType: 0=speed, 1=rod, 2=mining
    bool GetUpgrade(int t)
        => t == 0 ? (buyerIndex == 0 ? gridManager.gameData.hasSpeedUpgrade  : gridManager.gameData.player2HasSpeedUpgrade)
         : t == 1 ? (buyerIndex == 0 ? gridManager.gameData.hasRodUpgrade    : gridManager.gameData.player2HasRodUpgrade)
         :          (buyerIndex == 0 ? gridManager.gameData.hasMiningUpgrade : gridManager.gameData.player2HasMiningUpgrade);

    void SetUpgrade(int t, bool v)
    {
        if (buyerIndex == 0)
        {
            if (t == 0) gridManager.gameData.hasSpeedUpgrade  = v;
            else if (t == 1) gridManager.gameData.hasRodUpgrade    = v;
            else             gridManager.gameData.hasMiningUpgrade = v;
        }
        else
        {
            if (t == 0) gridManager.gameData.player2HasSpeedUpgrade  = v;
            else if (t == 1) gridManager.gameData.player2HasRodUpgrade    = v;
            else             gridManager.gameData.player2HasMiningUpgrade = v;
        }
    }

    int  ShipLevel()        => buyerIndex == 0 ? gridManager.gameData.shipLevel          : gridManager.gameData.player2ShipLevel;
    void SetShipLevel(int v) { if (buyerIndex == 0) gridManager.gameData.shipLevel = v;    else gridManager.gameData.player2ShipLevel = v; }

    // ── Nákup ─────────────────────────────────────────────────────────────────
    private bool TryBuyUpgrade(int upgradeType, int cost)
    {
        if (GetUpgrade(upgradeType) || Coins() < cost) return false;
        SetCoins(Coins() - cost);
        SetUpgrade(upgradeType, true);
        gridManager.Save();
        gridManager.NotifyWorldChanged();
        return true;
    }

    // ── GUI ───────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        if (!isOpen) return;
        InitStyles();

        GUI.color = new Color(0, 0, 0, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float w = 560, h = 460;
        float px = (Screen.width  - w) / 2f;
        float py = (Screen.height - h) / 2f;

        GUI.color = new Color(0.12f, 0.14f, 0.18f, 1f);
        GUI.DrawTexture(new Rect(px, py, w, h), Texture2D.whiteTexture);
        GUI.color = new Color(0.4f, 0.6f, 1f, 1f);
        GUI.DrawTexture(new Rect(px, py, w, 3), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(px + 25, py + 20, w - 50, h - 40));

        string playerLabel = MultiplayerManager.IsMultiplayer
            ? (buyerIndex == 0 ? "  —  HRÁČ 1" : "  —  HRÁČ 2")
            : "";
        GUILayout.Label($"OBCHOD S VYLEPSENIMI{playerLabel}", titleStyle);
        GUILayout.Space(15);

        DrawRow("Rychlost lodi  —  pohyb 2x rychleji",  speedUpgradeCost,  GetUpgrade(0), () => TryBuyUpgrade(0, speedUpgradeCost));
        GUILayout.Space(8);
        DrawRow("Lepsi prud  —  chyta 2 ryby najednou",  rodUpgradeCost,   GetUpgrade(1), () => TryBuyUpgrade(1, rodUpgradeCost));
        GUILayout.Space(8);
        DrawRow("Rychlost tezby  —  tezba 2x rychleji", miningUpgradeCost, GetUpgrade(2), () => TryBuyUpgrade(2, miningUpgradeCost));
        GUILayout.Space(8);
        DrawShipRow("Lod stredni  —  lepsi vzhled",  shipMediumCost, 1);
        GUILayout.Space(8);
        DrawShipRow("Lod velka  —  nejlepsi vzhled", shipLargeCost,  2);

        GUILayout.Space(18);
        GUILayout.Label($"Mince: {Coins()}", coinsStyle);
        GUILayout.EndArea();
    }

    private void DrawShipRow(string label, int cost, int requiredLevel)
    {
        int  cur    = ShipLevel();
        bool owned  = cur >= requiredLevel;
        bool canBuy = cur == requiredLevel - 1;
        DrawRow(label, cost, owned, () =>
        {
            if (!canBuy || Coins() < cost) return;
            SetCoins(Coins() - cost);
            SetShipLevel(requiredLevel);
            gridManager.Save();
            gridManager.NotifyWorldChanged();
            // Aktualizuj model lode pro správného hráče
            var switchers = FindObjectsByType<ShipModelSwitcher>(FindObjectsSortMode.None);
            foreach (var s in switchers)
            {
                var pc = s.GetComponent<PlayerController>() ?? s.GetComponentInParent<PlayerController>();
                if (pc != null && pc.playerIndex == buyerIndex) { s.Apply(); break; }
            }
        });
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
            GUI.enabled = Coins() >= cost;
            if (GUILayout.Button("Koupit", buyStyle, GUILayout.Width(90), GUILayout.Height(28)))
                onBuy();
            GUI.enabled = true;
        }
        GUILayout.EndHorizontal();
    }

    private void InitStyles()
    {
        if (stylesReady) return;
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        rowStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16, alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.9f, 0.9f, 0.9f) }
        };
        ownedStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.4f, 1f, 0.4f) }
        };
        buyStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 15, fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = MakeTex(new Color(0.2f, 0.5f, 0.2f)) },
            hover  = { textColor = Color.white, background = MakeTex(new Color(0.3f, 0.65f, 0.3f)) }
        };
        closeStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 15,
            normal = { textColor = Color.white, background = MakeTex(new Color(0.5f, 0.1f, 0.1f)) },
            hover  = { textColor = Color.white, background = MakeTex(new Color(0.7f, 0.15f, 0.15f)) }
        };
        coinsStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.85f, 0.2f) }
        };
        stylesReady = true;
    }

    private Texture2D MakeTex(Color c) { var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t; }
}
