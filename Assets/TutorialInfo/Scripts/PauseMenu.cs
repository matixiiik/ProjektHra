using UnityEngine;

[DefaultExecutionOrder(-100)]
public class PauseMenu : MonoBehaviour
{
    [Header("Legacy UI root (bude skryt, nemusíš mazat)")]
    public GameObject menuRoot;

    private bool   isOpen  = false;
    private string transferAmount = "100";

    private GridManager      grid;
    private PlayerController player;

    private GUIStyle titleStyle, buttonStyle, transferTitleStyle, transferBtnStyle, coinInfoStyle, inputStyle;
    private bool stylesReady;

    void Start()
    {
        grid   = FindFirstObjectByType<GridManager>();
        player = FindFirstObjectByType<PlayerController>();
        if (menuRoot != null) menuRoot.SetActive(false);
    }

    void Update()
    {
        if (MainMenuManager.IsVisible) return;

        bool pausePressed = Input.GetKeyDown(KeyCode.Escape)
                         || (MultiplayerManager.IsMultiplayer && Input.GetKeyDown(KeyCode.KeypadEnter));
        if (pausePressed)
        {
            if (UpgradeShopManager.AnyShopOpen) return;
            if (isOpen) ContinueGame();
            else        OpenMenu();
        }
    }

    void OnGUI()
    {
        if (!isOpen) return;
        InitStyles();

        bool mp  = MultiplayerManager.IsMultiplayer;
        float w  = 360;
        float h  = mp ? 420 : 260;

        // Tmavý overlay
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float px = (Screen.width  - w) / 2f;
        float py = (Screen.height - h) / 2f;

        // Panel
        GUI.color = new Color(0.1f, 0.12f, 0.16f, 1f);
        GUI.DrawTexture(new Rect(px, py, w, h), Texture2D.whiteTexture);
        GUI.color = new Color(0.4f, 0.6f, 1f, 1f);
        GUI.DrawTexture(new Rect(px, py, w, 3), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(px + 20, py + 20, w - 40, h - 40));

        GUILayout.Label("PAUZA", titleStyle);
        GUILayout.Space(14);

        if (GUILayout.Button("Pokračovat", buttonStyle, GUILayout.Height(44)))
            ContinueGame();
        GUILayout.Space(8);
        if (GUILayout.Button("Nová hra", buttonStyle, GUILayout.Height(44)))
            NewGame();
        GUILayout.Space(8);
        if (GUILayout.Button("Hlavní menu", buttonStyle, GUILayout.Height(44)))
            GoToMainMenu();

        // ── Sekce převodu peněz (jen v multiplayeru) ─────────────────────────
        if (mp && grid != null)
        {
            GUILayout.Space(14);
            // Oddělovač
            GUI.color = new Color(1f, 0.85f, 0.2f, 0.5f);
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
            GUI.color = Color.white;

            GUILayout.Space(6);
            GUILayout.Label("PŘEVOD PENĚZ", transferTitleStyle);
            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            GUILayout.Label($"P1: {grid.gameData.coins} mincí", coinInfoStyle, GUILayout.ExpandWidth(true));
            GUILayout.Label($"P2: {grid.gameData.player2Coins} mincí", coinInfoStyle, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            transferAmount = GUILayout.TextField(transferAmount, 8, inputStyle, GUILayout.Width(90), GUILayout.Height(34));
            GUILayout.Space(6);
            if (GUILayout.Button("P1 → P2", transferBtnStyle, GUILayout.Height(34), GUILayout.ExpandWidth(true)))
                TryTransfer(0, 1);
            GUILayout.Space(4);
            if (GUILayout.Button("P2 → P1", transferBtnStyle, GUILayout.Height(34), GUILayout.ExpandWidth(true)))
                TryTransfer(1, 0);
            GUILayout.EndHorizontal();
        }

        GUILayout.EndArea();
    }

    // ── Akce ─────────────────────────────────────────────────────────────────

    public void OpenMenu()    { isOpen = true;  Time.timeScale = 0f; }
    public void ContinueGame(){ isOpen = false; Time.timeScale = 1f; }

    public void NewGame()
    {
        Time.timeScale = 1f;
        isOpen = false;
        if (grid   != null) grid.NewGameReset();
        if (player != null) player.TeleportTo(grid.gameData.playerGridX, grid.gameData.playerGridY);
        FindFirstObjectByType<ShipModelSwitcher>()?.Apply();
    }

    public void GoToMainMenu()
    {
        isOpen = false;
        if (MultiplayerManager.IsMultiplayer) MultiplayerManager.Stop();
        MainMenuManager.Show();
    }

    // Ponecháno pro zpětnou kompatibilitu s Canvas tlačítky v editoru
    public void ExitGame() => GoToMainMenu();

    // ── Převod peněz ─────────────────────────────────────────────────────────
    private void TryTransfer(int from, int to)
    {
        if (!int.TryParse(transferAmount, out int amount) || amount <= 0) return;

        int fromCoins = from == 0 ? grid.gameData.coins : grid.gameData.player2Coins;
        if (fromCoins < amount) return;

        if (from == 0) { grid.gameData.coins -= amount;         grid.gameData.player2Coins += amount; }
        else           { grid.gameData.player2Coins -= amount;  grid.gameData.coins += amount; }

        grid.Save();
        grid.NotifyWorldChanged();
    }

    // ── Styly ────────────────────────────────────────────────────────────────
    private void InitStyles()
    {
        if (stylesReady) return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 22, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = Color.white }
        };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 16, fontStyle = FontStyle.Bold,
            normal    = { textColor = Color.white, background = MakeTex(new Color(0.15f, 0.35f, 0.6f)) },
            hover     = { textColor = Color.white, background = MakeTex(new Color(0.2f,  0.45f, 0.75f)) }
        };
        transferTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 15, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(1f, 0.85f, 0.2f) }
        };
        transferBtnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 14, fontStyle = FontStyle.Bold,
            normal    = { textColor = Color.white, background = MakeTex(new Color(0.35f, 0.25f, 0.55f)) },
            hover     = { textColor = Color.white, background = MakeTex(new Color(0.5f,  0.35f, 0.75f)) }
        };
        coinInfoStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 14,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = new Color(0.9f, 0.9f, 0.9f) }
        };
        inputStyle = new GUIStyle(GUI.skin.textField)
        {
            fontSize  = 16, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = Color.white }
        };

        stylesReady = true;
    }

    private Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t;
    }
}
