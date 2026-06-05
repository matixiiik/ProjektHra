using UnityEngine;

[DefaultExecutionOrder(-200)]
public class MainMenuManager : MonoBehaviour
{
    public static bool IsVisible { get; private set; }

    private static MainMenuManager instance;

    private enum MenuPage { Main, NewGame, Continue }
    private MenuPage currentPage = MenuPage.Main;

    private GridManager    grid;
    private PlayerController player;
    private ShipModelSwitcher shipSwitcher;

    private GUIStyle titleStyle, buttonStyle, slotStyle, slotEmptyStyle, backStyle;
    private bool stylesReady;

    void Awake()
    {
        instance  = this;
        IsVisible = true;
        Time.timeScale = 0f;
    }

    void Start()
    {
        grid        = FindFirstObjectByType<GridManager>();
        player      = FindFirstObjectByType<PlayerController>();
        shipSwitcher = FindFirstObjectByType<ShipModelSwitcher>();
    }

    public static void Show()
    {
        if (instance == null) return;
        instance.currentPage = MenuPage.Main;
        IsVisible = true;
        Time.timeScale = 0f;
    }

    void OnGUI()
    {
        if (!IsVisible) return;
        InitStyles();

        // Tmavý overlay přes celou obrazovku
        GUI.color = new Color(0f, 0.04f, 0.1f, 0.94f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        switch (currentPage)
        {
            case MenuPage.Main:     DrawMain();                    break;
            case MenuPage.NewGame:  DrawSlots(isNewGame: true);    break;
            case MenuPage.Continue: DrawSlots(isNewGame: false);   break;
        }
    }

    // ── Hlavní stránka ───────────────────────────────────────────────────────
    void DrawMain()
    {
        float w = 380, h = 300;
        float px = (Screen.width  - w) / 2f;
        float py = (Screen.height - h) / 2f;

        GUILayout.BeginArea(new Rect(px, py, w, h));
        GUILayout.Label("LODNÍ DOBRODRUŽSTVÍ", titleStyle);
        GUILayout.Space(28);

        if (GUILayout.Button("Nová hra", buttonStyle, GUILayout.Height(50)))
            currentPage = MenuPage.NewGame;

        GUILayout.Space(10);

        bool hasSave = SaveManager.SlotExists(0) || SaveManager.SlotExists(1) || SaveManager.SlotExists(2);
        GUI.enabled = hasSave;
        if (GUILayout.Button("Pokračovat", buttonStyle, GUILayout.Height(50)))
            currentPage = MenuPage.Continue;
        GUI.enabled = true;

        GUILayout.Space(10);

        if (GUILayout.Button("Konec", buttonStyle, GUILayout.Height(50)))
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        GUILayout.EndArea();
    }

    // ── Výběr slotu ──────────────────────────────────────────────────────────
    void DrawSlots(bool isNewGame)
    {
        float w = 560, h = 340;
        float px = (Screen.width  - w) / 2f;
        float py = (Screen.height - h) / 2f;

        GUILayout.BeginArea(new Rect(px, py, w, h));
        GUILayout.Label(isNewGame ? "NOVÁ HRA — VYBER SLOT" : "POKRAČOVAT — VYBER SLOT", titleStyle);
        GUILayout.Space(18);

        for (int i = 0; i < 3; i++)
        {
            GameData preview = SaveManager.PeekSlot(i);
            bool exists = preview != null;

            if (!isNewGame && !exists)
            {
                GUI.enabled = false;
                GUILayout.Button($"  Slot {i + 1}  —  prázdný", slotEmptyStyle, GUILayout.Height(58));
                GUI.enabled = true;
            }
            else
            {
                string label = exists
                    ? $"  Slot {i + 1}  |  🪙 {preview.coins}  🐟 {preview.fishCount}  💎 {preview.treasureCount}"
                    : $"  Slot {i + 1}  —  prázdný";

                GUIStyle style = exists ? slotStyle : slotEmptyStyle;
                if (GUILayout.Button(label, style, GUILayout.Height(58)))
                {
                    if (isNewGame) StartNewGame(i);
                    else           LoadGame(i);
                }
            }
            GUILayout.Space(6);
        }

        GUILayout.Space(8);
        if (GUILayout.Button("← Zpět", backStyle, GUILayout.Height(34)))
            currentPage = MenuPage.Main;

        GUILayout.EndArea();
    }

    // ── Akce ─────────────────────────────────────────────────────────────────
    void StartNewGame(int slot)
    {
        grid.NewGameSlot(slot);
        player.ReloadFromData();
        shipSwitcher?.Apply();
        HideMenu();
    }

    void LoadGame(int slot)
    {
        grid.LoadSlot(slot);
        player.ReloadFromData();
        shipSwitcher?.Apply();
        HideMenu();
    }

    void HideMenu()
    {
        IsVisible = false;
        Time.timeScale = 1f;
    }

    // ── Styly ────────────────────────────────────────────────────────────────
    void InitStyles()
    {
        if (stylesReady) return;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 26, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = Color.white }
        };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 18, fontStyle = FontStyle.Bold,
            normal    = { textColor = Color.white, background = MakeTex(new Color(0.15f, 0.35f, 0.6f)) },
            hover     = { textColor = Color.white, background = MakeTex(new Color(0.2f,  0.45f, 0.75f)) }
        };
        slotStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 15, alignment = TextAnchor.MiddleLeft,
            normal    = { textColor = Color.white,                background = MakeTex(new Color(0.1f, 0.25f, 0.45f)) },
            hover     = { textColor = Color.white,                background = MakeTex(new Color(0.15f, 0.35f, 0.6f)) }
        };
        slotEmptyStyle = new GUIStyle(slotStyle)
        {
            normal = { textColor = new Color(0.45f, 0.45f, 0.45f), background = MakeTex(new Color(0.08f, 0.1f, 0.14f)) },
            hover  = { textColor = new Color(0.45f, 0.45f, 0.45f), background = MakeTex(new Color(0.08f, 0.1f, 0.14f)) }
        };
        backStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14,
            normal   = { textColor = Color.white, background = MakeTex(new Color(0.3f, 0.12f, 0.12f)) },
            hover    = { textColor = Color.white, background = MakeTex(new Color(0.5f, 0.18f, 0.18f)) }
        };
        stylesReady = true;
    }

    private Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t;
    }
}
