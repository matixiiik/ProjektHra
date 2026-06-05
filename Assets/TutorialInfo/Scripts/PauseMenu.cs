using UnityEngine;

[DefaultExecutionOrder(-100)]
public class PauseMenu : MonoBehaviour
{
    [Header("Legacy UI root (bude skryt, nemusíš mazat)")]
    public GameObject menuRoot;

    private bool isOpen = false;
    private GridManager grid;
    private PlayerController player;

    private GUIStyle titleStyle, buttonStyle;
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
            else OpenMenu();
        }
    }

    void OnGUI()
    {
        if (!isOpen) return;
        InitStyles();

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float w = 320, h = 260;
        float px = (Screen.width - w) / 2f;
        float py = (Screen.height - h) / 2f;

        GUI.color = new Color(0.1f, 0.12f, 0.16f, 1f);
        GUI.DrawTexture(new Rect(px, py, w, h), Texture2D.whiteTexture);
        GUI.color = new Color(0.4f, 0.6f, 1f, 1f);
        GUI.DrawTexture(new Rect(px, py, w, 3), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(px + 20, py + 20, w - 40, h - 40));
        GUILayout.Label("PAUZA", titleStyle);
        GUILayout.Space(16);

        if (GUILayout.Button("Pokračovat", buttonStyle, GUILayout.Height(44)))
            ContinueGame();
        GUILayout.Space(8);
        if (GUILayout.Button("Nová hra", buttonStyle, GUILayout.Height(44)))
            NewGame();
        GUILayout.Space(8);
        if (GUILayout.Button("Hlavní menu", buttonStyle, GUILayout.Height(44)))
            GoToMainMenu();

        GUILayout.EndArea();
    }

    private void SetMenuOpen(bool open) => isOpen = open;

    public void OpenMenu()    { SetMenuOpen(true);  Time.timeScale = 0f; }
    public void ContinueGame(){ SetMenuOpen(false); Time.timeScale = 1f; }

    public void NewGame()
    {
        Time.timeScale = 1f;
        SetMenuOpen(false);
        if (grid != null) grid.NewGameReset();
        if (player != null) player.TeleportTo(grid.gameData.playerGridX, grid.gameData.playerGridY);
        FindFirstObjectByType<ShipModelSwitcher>()?.Apply();
    }

    public void GoToMainMenu()
    {
        SetMenuOpen(false);
        if (MultiplayerManager.IsMultiplayer) MultiplayerManager.Stop();
        MainMenuManager.Show();
    }

    // Ponecháno pro zpětnou kompatibilitu s Canvas tlačítky v editoru
    public void ExitGame() => GoToMainMenu();

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
        stylesReady = true;
    }

    private Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t;
    }
}
