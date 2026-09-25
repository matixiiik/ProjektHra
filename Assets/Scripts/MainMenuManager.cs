using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  MainMenuManager.cs
//  Hlavní menu, které se ukáže hned po spuštění hry (a taky po kliknutí na
//  "Hlavní menu" v pauze). Nabízí: Nová hra / Pokračovat / Multiplayer / Konec
//  a přepínač jazyka (čeština / angličtina, viz Loc).
//  Nová hra, Pokračovat i Multiplayer vedou na výběr jednoho ze 3 save slotů.
//
//  [DefaultExecutionOrder(-200)] → Awake běží úplně první, aby stihl zastavit
//  hru (Time.timeScale = 0) dřív, než se cokoli pohne.
//
//  Kreslí se přes IMGUI (OnGUI). Ostatní skripty se dívají na IsVisible a když
//  je menu vidět, blokují ovládání.
// ─────────────────────────────────────────────────────────────────────────────

[DefaultExecutionOrder(-200)]
public class MainMenuManager : MonoBehaviour
{
    /// <summary>Je hlavní menu právě vidět? (blokuje hru)</summary>
    public static bool IsVisible { get; private set; }

    private static MainMenuManager instance;

    // Která obrazovka menu se právě zobrazuje.
    private enum MenuPage { Main, NewGame, Continue, Multiplayer }
    private MenuPage currentPage = MenuPage.Main;

    private GridManager       grid;
    private PlayerController  player;
    private ShipModelSwitcher shipSwitcher;

    private GUIStyle titleStyle, buttonStyle, multiStyle, slotStyle, slotEmptyStyle, backStyle, langStyle;
    private bool     stylesReady;

    // Náhledy 3 slotů (mince/ryby/poklady). Načítají se JEN při otevření stránky se
    // sloty — OnGUI běží několikrát za snímek a číst přitom z disku několik MB
    // save souborů by menu zpomalilo na pár snímků za sekundu.
    private readonly SlotSummary[] slotPreviews = new SlotSummary[3];
    private void RefreshSlotPreviews()
    {
        for (int i = 0; i < 3; i++) slotPreviews[i] = SaveManager.PeekSlotSummary(i);
    }

    // Přepne stránku menu (a u stránek se sloty načte náhledy).
    private void GoTo(MenuPage page)
    {
        currentPage = page;
        if (page != MenuPage.Main) RefreshSlotPreviews();
    }

    void Awake()
    {
        instance = this;

        // Návrat z majáku → menu přeskoč, hra rovnou pokračuje z uloženého stavu.
        if (GameSession.ReturningFromLighthouse)
        {
            GameSession.ReturningFromLighthouse = false;
            IsVisible      = false;
            Time.timeScale = 1f;
            return;
        }

        IsVisible      = true;
        Time.timeScale = 0f; // zastav hru, dokud si hráč nevybere
    }

    void Start()
    {
        grid         = FindFirstObjectByType<GridManager>();
        player       = FindFirstObjectByType<PlayerController>();
        shipSwitcher = FindFirstObjectByType<ShipModelSwitcher>();
    }

    /// <summary>Znovu zobrazí hlavní menu (volá pauza přes "Hlavní menu").</summary>
    public static void Show()
    {
        if (instance == null) return;
        // Rozdělaný postup musí být na disku dřív, než se ze slotů čtou náhledy.
        if (instance.grid != null) instance.grid.FlushSaveBlocking();
        instance.currentPage = MenuPage.Main;
        IsVisible      = true;
        Time.timeScale = 0f;
    }

    void OnGUI()
    {
        if (!IsVisible) return;
        InitStyles();

        // Tmavě modré pozadí přes celou obrazovku.
        GUI.color = new Color(0f, 0.04f, 0.1f, 0.94f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Podle aktuální stránky vykresli buď hlavní tlačítka, nebo výběr slotu.
        switch (currentPage)
        {
            case MenuPage.Main:        DrawMain();                                                    break;
            case MenuPage.NewGame:     DrawSlots(Loc.T("NOVÁ HRA — VYBER SLOT",    "NEW GAME — CHOOSE A SLOT"),    SlotMode.NewGame);       break;
            case MenuPage.Continue:    DrawSlots(Loc.T("POKRAČOVAT — VYBER SLOT",  "CONTINUE — CHOOSE A SLOT"),    SlotMode.Continue);      break;
            case MenuPage.Multiplayer: DrawSlots(Loc.T("MULTIPLAYER — VYBER SLOT", "MULTIPLAYER — CHOOSE A SLOT"), SlotMode.Multiplayer);   break;
        }
    }

    // ── Hlavní stránka ──────────────────────────────────────────────────────
    void DrawMain()
    {
        float w = 380, h = 440;
        float px = (Screen.width  - w) / 2f;
        float py = (Screen.height - h) / 2f;

        GUILayout.BeginArea(new Rect(px, py, w, h));
        GUILayout.Label(Loc.T("LODNÍ DOBRODRUŽSTVÍ", "SEA ADVENTURE"), titleStyle);
        GUILayout.Space(20);

        if (SoundManager.Click(GUILayout.Button(Loc.T("Nová hra", "New Game"), buttonStyle, GUILayout.Height(50))))
            GoTo(MenuPage.NewGame);

        GUILayout.Space(10);

        // "Pokračovat" jde zmáčknout jen když existuje aspoň jeden save.
        bool hasSave = SaveManager.SlotExists(0) || SaveManager.SlotExists(1) || SaveManager.SlotExists(2);
        GUI.enabled = hasSave;
        if (SoundManager.Click(GUILayout.Button(Loc.T("Pokračovat", "Continue"), buttonStyle, GUILayout.Height(50))))
            GoTo(MenuPage.Continue);
        GUI.enabled = true;

        GUILayout.Space(10);

        if (SoundManager.Click(GUILayout.Button("🎮  Multiplayer (split screen)", multiStyle, GUILayout.Height(50))))
            GoTo(MenuPage.Multiplayer);

        GUILayout.Space(10);

        if (SoundManager.Click(GUILayout.Button(Loc.T("Konec", "Quit"), buttonStyle, GUILayout.Height(50))))
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false; // v editoru "Quit" jen zastaví Play
#endif
        }

        GUILayout.Space(22);

        // Přepínač jazyka — ukazuje, co je zapnuté, a co se zapne po kliknutí.
        string langLabel = Loc.En ? "Language: English  (→ Čeština)" : "Jazyk: Čeština  (→ English)";
        if (SoundManager.Click(GUILayout.Button(langLabel, langStyle, GUILayout.Height(38))))
        {
            Loc.Toggle();
            if (grid != null) grid.NotifyWorldChanged(); // HUD a minimapa si překreslí popisky
        }

        GUILayout.EndArea();
    }

    // ── Výběr slotu ─────────────────────────────────────────────────────────
    enum SlotMode { NewGame, Continue, Multiplayer }

    void DrawSlots(string title, SlotMode mode)
    {
        float w = 580, h = 360;
        float px = (Screen.width  - w) / 2f;
        float py = (Screen.height - h) / 2f;

        GUILayout.BeginArea(new Rect(px, py, w, h));
        GUILayout.Label(title, titleStyle);
        GUILayout.Space(18);

        for (int i = 0; i < 3; i++)
        {
            SlotSummary preview = slotPreviews[i]; // náhled slotu (nebo null), viz RefreshSlotPreviews
            bool        exists  = preview != null;

            // V režimu "Pokračovat" jdou zmáčknout jen sloty, které existují.
            bool canClick = mode != SlotMode.Continue || exists;

            string label = exists
                ? $"  Slot {i + 1}  |  🪙 {preview.coins}  🐟 {preview.fishCount}  💎 {preview.treasureCount}"
                : $"  Slot {i + 1}  —  " + Loc.T("prázdný", "empty");

            GUI.enabled = canClick;
            GUIStyle st = (exists || mode == SlotMode.Multiplayer) ? slotStyle : slotEmptyStyle;
            if (SoundManager.Click(GUILayout.Button(label, st, GUILayout.Height(58))))
            {
                switch (mode)
                {
                    case SlotMode.NewGame:     StartNewGame(i);              break;
                    case SlotMode.Continue:    LoadGame(i);                  break;
                    case SlotMode.Multiplayer: StartMultiplayer(i, exists);  break;
                }
            }
            GUI.enabled = true;
            GUILayout.Space(6);
        }

        GUILayout.Space(8);
        if (SoundManager.Click(GUILayout.Button(Loc.T("← Zpět", "← Back"), backStyle, GUILayout.Height(34))))
            currentPage = MenuPage.Main;

        GUILayout.EndArea();
    }

    // ── Co se stane po výběru slotu ─────────────────────────────────────────
    void StartNewGame(int slot)
    {
        grid.NewGameSlot(slot);      // smaž slot a vygeneruj nový svět
        player.ReloadFromData();     // postav hráče na start
        shipSwitcher?.Apply();
        HideMenu();
    }

    void LoadGame(int slot)
    {
        grid.LoadSlot(slot);         // načti existující svět
        player.ReloadFromData();
        shipSwitcher?.Apply();
        HideMenu();
    }

    void StartMultiplayer(int slot, bool exists)
    {
        // Existující slot = pokračuj, prázdný = založ novou hru.
        if (exists) grid.LoadSlot(slot);
        else        grid.NewGameSlot(slot);

        player.ReloadFromData();
        shipSwitcher?.Apply();
        MultiplayerManager.StartMultiplayer(); // rozděl obrazovku, přidej P2
        HideMenu();
    }

    void HideMenu()
    {
        IsVisible      = false;
        Time.timeScale = 1f; // rozjeď hru
    }

    // ── Styly (jen jednou) ──────────────────────────────────────────────────
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
        multiStyle = new GUIStyle(buttonStyle)
        {
            normal = { textColor = Color.white, background = MakeTex(new Color(0.45f, 0.2f,  0.55f)) },
            hover  = { textColor = Color.white, background = MakeTex(new Color(0.55f, 0.28f, 0.68f)) }
        };
        slotStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize  = 15, alignment = TextAnchor.MiddleLeft,
            normal    = { textColor = Color.white, background = MakeTex(new Color(0.1f,  0.25f, 0.45f)) },
            hover     = { textColor = Color.white, background = MakeTex(new Color(0.15f, 0.35f, 0.6f)) }
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
        langStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 15,
            normal   = { textColor = Color.white, background = MakeTex(new Color(0.12f, 0.32f, 0.34f)) },
            hover    = { textColor = Color.white, background = MakeTex(new Color(0.18f, 0.45f, 0.48f)) }
        };
        stylesReady = true;
    }

    private Texture2D MakeTex(Color c)
    {
        var t = new Texture2D(1, 1); t.SetPixel(0, 0, c); t.Apply(); return t;
    }
}
