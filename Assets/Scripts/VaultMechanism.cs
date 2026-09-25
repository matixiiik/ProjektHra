using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  VaultMechanism.cs
//  Puzzle na trezoru ostrova 1 (mega ostrov, viz .claude/story-plan.md §3
//  varianta 4): tři ozubená kola vedle sebe, každé s 5 symboly. Otočení
//  jednoho kola o 1 pozici dopředu otočí OBA sousedy o 1 pozici dozadu (do
//  sebe zapadají — prostřední kolo ovlivňuje obě krajní, krajní jen prostřední,
//  je to řetězec, ne kruh). Cíl: všechna tři kola ukazují stejný symbol.
//  Z libovolného zamíchaného stavu je cíl vždy dosažitelný (ověřeno jinde
//  simulací), průměrně stačí ~6 kliknutí.
//
//  Vstup je bez myši, ať funguje i pro P2 ve split screenu (stejně jako
//  zbytek hry): E vedle trezoru puzzle otevře (MegaIslandMarker.TryInteract).
//  P1: A/D přepíná, KTERÉ kolo je vybrané (pohyb po řádku tří kol), W/S otočí
//  vybrané kolo dopředu/dozadu (pohyb ve sloupci pěti symbolů), Escape zavře.
//  P2 (numpad má stejné rozložení): Numpad 4/6 = výběr kola, Numpad 8/2 =
//  otočení, NumpadEnter zavře. Dokud je puzzle otevřené, hráč se nehýbe
//  (stejná brána jako u obchodů/dialogu — viz VaultMechanism.IsOpenFor
//  v PlayerController).
// ─────────────────────────────────────────────────────────────────────────────

public class VaultMechanism : MonoBehaviour
{
    private const int WHEEL_COUNT  = 3;
    private const int SYMBOL_COUNT = 5;
    private static readonly string[] SYMBOL_NAMES    = { "KOTVA", "LEBKA", "KOMPAS", "VLNA", "MINCE" };
    private static readonly string[] SYMBOL_NAMES_EN = { "ANCHOR", "SKULL", "COMPASS", "WAVE", "COIN" };
    private static readonly Color[]  SYMBOL_COLORS =
    {
        new Color(0.30f, 0.55f, 0.85f), // kotva  — modrá
        new Color(0.85f, 0.85f, 0.82f), // lebka  — bílá
        new Color(0.85f, 0.70f, 0.20f), // kompas — zlatá
        new Color(0.25f, 0.75f, 0.75f), // vlna   — tyrkysová
        new Color(0.90f, 0.80f, 0.15f), // mince  — žlutá
    };

    /// <summary>Otevřený puzzle pro daného hráče, nebo null nikdo.</summary>
    public static bool IsOpenFor(int playerIndex) => openInstance != null && openInstance.openFor == playerIndex;
    private static VaultMechanism openInstance;

    private GridManager gridManager;
    private Vector2Int tile;                       // políčko, na kterém trezor stojí (nehýbe se)
    private int[]  ringPos = new int[WHEEL_COUNT]; // 0-4, aktuální symbol každého kola
    private int    openFor = -1;                   // -1 = zavřeno, jinak index hráče (0/1)
    private int    selected;                       // které kolo (0-2) zrovna vybírá A/D (resp. Numpad 4/6)
    private bool   solved;
    private Transform[] wheelVisual = new Transform[WHEEL_COUNT];

    private GUIStyle boxStyle, wheelStyle, hintStyle, doneStyle;
    private bool     stylesReady;

    /// <summary>Postaví trezor na dané políčko. `alreadySolved` = obnovení po
    /// načtení save, kdy už byl dřív vyřešený (jen se ukáže vyřešený, žádná
    /// odměna se nedává podruhé — o to se ale stará megaTask, ne tohle pole).</summary>
    public static VaultMechanism Spawn(Vector2Int tile, bool alreadySolved)
    {
        var root = new GameObject("Vault");
        root.transform.position = new Vector3(tile.x, 0f, tile.y);

        var v = root.AddComponent<VaultMechanism>();
        v.gridManager = FindFirstObjectByType<GridManager>();
        v.tile        = tile;
        v.BuildVisual();

        if (alreadySolved)
        {
            v.solved = true;
            for (int i = 0; i < WHEEL_COUNT; i++) v.ringPos[i] = 0; // vyřešeno = všechny na stejném symbolu
        }
        else
        {
            // Zamíchej do řešitelného, ale ne rovnou vyhraného stavu (deterministicky z pozice).
            int seed = Mathf.Abs(unchecked(tile.x * 92821 ^ tile.y * 68917));
            v.ringPos[0] = seed % SYMBOL_COUNT;
            v.ringPos[1] = (seed / SYMBOL_COUNT) % SYMBOL_COUNT;
            v.ringPos[2] = (seed / (SYMBOL_COUNT * SYMBOL_COUNT)) % SYMBOL_COUNT;
            if (v.ringPos[0] == v.ringPos[1] && v.ringPos[1] == v.ringPos[2])
                v.ringPos[0] = (v.ringPos[0] + 1) % SYMBOL_COUNT;
        }
        v.RefreshVisual();
        return v;
    }

    void OnDestroy() { if (openInstance == this) openInstance = null; }

    // Kamenná bedna + tři barevné disky (kola) — přesné symboly se ukazují
    // v IMGUI puzzlu, disky ve světě jen barevně naznačí aktuální stav.
    private void BuildVisual()
    {
        Material stone = MakeMat(new Color(0.40f, 0.38f, 0.36f));

        Box("Chest", new Vector3(0f, 0.35f, 0f), new Vector3(1.6f, 0.7f, 1.0f), stone);

        for (int i = 0; i < WHEEL_COUNT; i++)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            disc.name = "Wheel" + i;
            var col = disc.GetComponent<Collider>();
            if (col != null) Destroy(col);
            disc.transform.SetParent(transform, false);
            disc.transform.localPosition    = new Vector3(-0.5f + i * 0.5f, 0.75f, 0.42f);
            disc.transform.localScale       = new Vector3(0.18f, 0.03f, 0.18f);
            disc.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            wheelVisual[i] = disc.transform;
        }
    }

    private void RefreshVisual()
    {
        for (int i = 0; i < WHEEL_COUNT; i++)
        {
            if (wheelVisual[i] == null) continue;
            var mr = wheelVisual[i].GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = MakeMat(SYMBOL_COLORS[ringPos[i]]);
        }
    }

    /// <summary>Stojí trezor na tomhle políčku? (pro hák z PlayerController přes marker)</summary>
    public bool IsAt(int x, int y) => tile.x == x && tile.y == y;

    /// <summary>Je puzzle už vyřešené? (marker podle toho pozná, že má nabídnout přečtení vzkazu)</summary>
    public bool Solved => solved;

    /// <summary>Otevře puzzle pro hráče (volá MegaIslandMarker.TryInteract). Když je
    /// už vyřešeno, jen ukáže hlášku a nic neotvírá.</summary>
    public bool Open(int playerIndex)
    {
        if (solved)
        {
            if (CombatDirector.Instance != null) CombatDirector.Instance.Toast(Loc.T("Trezor je už otevřený.", "The vault is already open."));
            return true;
        }
        if (openFor != -1) return true; // někdo (jiný hráč) už ho má otevřený

        openFor  = playerIndex;
        selected = 0;
        openInstance = this;
        return true;
    }

    void Update()
    {
        if (openFor == -1 || solved) return;

        bool p1 = openFor == 0;
        int selectDir = 0; // A/D (resp. Numpad 4/6) — pohyb výběru po řádku kol
        int turnDir   = 0; // W/S (resp. Numpad 8/2) — otočení vybraného kola ve sloupci symbolů
        bool close;

        if (p1)
        {
            if      (Input.GetKeyDown(KeyCode.A)) selectDir = -1;
            else if (Input.GetKeyDown(KeyCode.D)) selectDir = 1;
            if      (Input.GetKeyDown(KeyCode.W)) turnDir = 1;
            else if (Input.GetKeyDown(KeyCode.S)) turnDir = -1;
            close = Input.GetKeyDown(KeyCode.Escape);
        }
        else
        {
            if      (Input.GetKeyDown(KeyCode.Keypad4)) selectDir = -1;
            else if (Input.GetKeyDown(KeyCode.Keypad6)) selectDir = 1;
            if      (Input.GetKeyDown(KeyCode.Keypad8)) turnDir = 1;
            else if (Input.GetKeyDown(KeyCode.Keypad2)) turnDir = -1;
            close = Input.GetKeyDown(KeyCode.KeypadEnter);
        }

        if (close) { CloseFor(openFor); return; }
        if (selectDir != 0) selected = (selected + selectDir + WHEEL_COUNT) % WHEEL_COUNT;
        if (turnDir   != 0) TurnWheel(selected, turnDir);
    }

    private void CloseFor(int playerIndex)
    {
        openFor = -1;
        if (openInstance == this) openInstance = null;
    }

    // Otoč kolo `i` o 1 pozici ve směru `dir` (+1/-1), sousedy o 1 pozici
    // opačným směrem (zapadají do sebe — stejné řetězové pravidlo jako dřív,
    // jen teď jde otáčet oběma směry, ne jen dopředu).
    private void TurnWheel(int i, int dir)
    {
        ringPos[i] = (ringPos[i] + dir + SYMBOL_COUNT) % SYMBOL_COUNT;
        if (i - 1 >= 0)          ringPos[i - 1] = (ringPos[i - 1] - dir + SYMBOL_COUNT) % SYMBOL_COUNT;
        if (i + 1 < WHEEL_COUNT) ringPos[i + 1] = (ringPos[i + 1] - dir + SYMBOL_COUNT) % SYMBOL_COUNT;
        RefreshVisual();
        SoundManager.PlayClick();

        if (ringPos[0] == ringPos[1] && ringPos[1] == ringPos[2]) OnSolved();
    }

    private void OnSolved()
    {
        solved = true;
        int wasOpenFor = openFor;
        openFor = -1;
        if (openInstance == this) openInstance = null;

        // < 2, ne == 1 — trezor teď jde vyřešit i s obranou ještě naživu
        // (megaTask pořád 0), takže sem hráč může dorazit s oběma hodnotami.
        if (gridManager != null && gridManager.gameData.megaTask < 2)
        {
            gridManager.gameData.megaTask = 2;
            gridManager.Save();
            gridManager.NotifyWorldChanged();
        }
        SoundManager.PlayCoin();
        if (CombatDirector.Instance != null) CombatDirector.Instance.Toast(Loc.T("Trezor otevřen!", "Vault opened!"));
    }

    // ── UI ─────────────────────────────────────────────────────────────────
    void OnGUI()
    {
        if (openFor == -1) return;
        InitStyles();

        Rect half = HalfRect(openFor);
        float w = Mathf.Min(520f, half.width - 40f);
        float h = 170f;
        var box = new Rect(half.x + (half.width - w) / 2f, half.y + (half.height - h) / 2f, w, h);

        GUI.color = new Color(0.06f, 0.07f, 0.10f, 0.92f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(box.x, box.y + 10f, box.width, 24f),
            Loc.T("Trezor — sjednoť tři kola na stejný symbol", "Vault — line up all three wheels on the same symbol"), boxStyle);

        float wheelW = box.width / 3f;
        for (int i = 0; i < WHEEL_COUNT; i++)
        {
            // Vybrané kolo (to, co teď otáčí W/S resp. Numpad 8/2) zvýrazni rámečkem.
            if (i == selected)
            {
                var frame = new Rect(box.x + i * wheelW + 6f, box.y + 50f, wheelW - 12f, 58f);
                GUI.color = new Color(1f, 0.85f, 0.45f, 0.5f);
                GUI.DrawTexture(frame, Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            var wr = new Rect(box.x + i * wheelW, box.y + 55f, wheelW, 50f);
            GUI.Label(wr, Loc.T(SYMBOL_NAMES[ringPos[i]], SYMBOL_NAMES_EN[ringPos[i]]), wheelStyle);
        }

        string keySelect = openFor == 0 ? "A / D" : "Numpad 4 / 6";
        string keyTurn   = openFor == 0 ? "W / S" : "Numpad 8 / 2";
        string keyClose  = openFor == 0 ? "Esc"   : "Numpad Enter";
        GUI.Label(new Rect(box.x, box.y + 115f, box.width, 24f),
            Loc.T("Vyber kolo: ", "Select wheel: ") + keySelect + Loc.T("   Otoč: ", "   Turn: ") + keyTurn, hintStyle);
        GUI.Label(new Rect(box.x, box.y + 140f, box.width, 24f), Loc.T("Zavřít: ", "Close: ") + keyClose, hintStyle);
    }

    // Obrazovka celá (sólo) nebo levá / pravá půlka (split screen).
    private Rect HalfRect(int playerIndex)
    {
        if (!MultiplayerManager.IsMultiplayer)
            return new Rect(0f, 0f, Screen.width, Screen.height);
        float w = Screen.width * 0.5f;
        return new Rect(playerIndex == 1 ? w : 0f, 0f, w, Screen.height);
    }

    private void InitStyles()
    {
        if (stylesReady) return;
        boxStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.85f, 0.45f) }
        };
        wheelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white }
        };
        hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.8f, 0.8f, 0.75f) }
        };
        stylesReady = true;
    }

    // ── pomůcky ────────────────────────────────────────────────────────────
    private void Box(string name, Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    private static Material MakeMat(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
        return m;
    }
}
