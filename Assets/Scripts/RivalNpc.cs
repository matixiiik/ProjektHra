using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  RivalNpc.cs
//  Dědův starší bratr — sok příběhu, ostrov 3 "Kde to začalo" (Krok 7, viz
//  .claude/story-plan.md §1). Stejný princip jako StoryNpc (dialog, hint),
//  jen menší — jedna dlouhá řada replik a na konci volba hráče:
//    A — ušetřit (storyEnding = 1): bratr jede s hráčem domů, teplejší konec.
//    B — zabít    (storyEnding = 2): hráč si vezme dědictví sám, melancholický konec.
//  Obě volby: storyDone = true, stejná odměna (EconomyConfig.FamilyTreasureReward)
//  — schválně stejná, ať hra nenutí hráče k temnější volbě kvůli mincím.
//
//  Vytváří ho MegaIslandMarker.BuildConfrontation() na pevné políčko, až padne
//  bratrova hlídková loď (megaTask 0→1). Po volbě zůstává na místě (jen s
//  jinými, kratšími replikami — viz BuildDialog), ať hráč může znovu promluvit
//  bez pádu hry.
// ─────────────────────────────────────────────────────────────────────────────

public class RivalNpc : MonoBehaviour
{
    private const string NpcName   = "Starší bratr";
    private const float  HintRange = 2.4f;

    public static RivalNpc Instance { get; private set; }

    private GridManager gridManager;
    private Vector2Int  tilePos;
    private bool        placed;
    private int         talkingWith = -1; // 0 = P1, 1 = P2, -1 = nikdo
    private int         line;
    private string[]    activeLines;
    private bool        showChoiceButtons; // volba A/B na poslední replice
    private float       ignoreKeyUntil;
    private float       reopenAllowedAt;

    private GUIStyle nameStyle, textStyle, hintStyle, spareStyle, killStyle;
    private bool     stylesReady;

    void Awake()     { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    private GameData Data => gridManager.gameData;

    /// <summary>Postaví bratra na dané políčko (deterministicky vybrané
    /// MegaIslandMarker.BuildConfrontation, stejně jako strážci na ostrově 1).</summary>
    public static RivalNpc Spawn(Vector2Int tile)
    {
        var go = new GameObject("RivalNpc");
        go.transform.position = new Vector3(tile.x, 0.02f, tile.y);

        var npc = go.AddComponent<RivalNpc>();
        npc.gridManager = FindFirstObjectByType<GridManager>();
        npc.tilePos     = tile;
        npc.placed      = true;
        npc.BuildFigure();
        return npc;
    }

    public bool IsAt(int x, int y) => placed && x == tilePos.x && y == tilePos.y;
    public bool IsTalkingWith(int playerIndex) => talkingWith == playerIndex;

    public void StartTalk(int playerIndex)
    {
        if (talkingWith != -1) return;
        if (Time.unscaledTime < reopenAllowedAt) return;
        talkingWith    = playerIndex;
        line           = 0;
        BuildDialog();
        ignoreKeyUntil = Time.unscaledTime + 0.25f;
        SoundManager.PlayClick();
    }

    // ── Dialog ────────────────────────────────────────────────────────────
    private void BuildDialog()
    {
        showChoiceButtons = false;

        // Po volbě: krátká reakce místo celého monologu, ať se dá znovu promluvit bez pádu hry.
        if (Data.storyDone)
        {
            activeLines = Data.storyEnding == 1
                ? new[] { "Připrav loď. Pojedu s tebou." }
                : new[] { "Máš, cos chtěl. Zbytek je tvoje věc." };
            return;
        }

        activeLines = new[]
        {
            "Tak přece jsi přijel.",
            "Roky jsem čekal, že se pro mě někdo vrátí. Nikdo nepřijel.",
            "Otec s malým bratrem odpluli domů. Mě nechali na tom prokletém útesu.",
            "Vyrostl jsem tady sám. Živil se, jak se dalo. Zestárl jsem u moře, co mi vzalo rodinu.",
            "Rodina? Ta si po letech řekla, že jsem utonul. Pohřbili mě, jako bych byl nikdo.",
            "Potkal jsem cestou i jeho syna, víš. I toho si nakonec vzalo moře — bere si, co chce, a nikoho se neptá.",
            "Tohle je to, co jste s dědou celou dobu hledali — rodinné dědictví. Měl jsem ho u sebe celou dobu.",
            "Tak co bude, chlapče? Vezmeš mě domů, nebo si vezmeš jen tohle?",
        };
        showChoiceButtons = true;
    }

    private void ChooseSpare() => ResolveEnding(1);
    private void ChooseKill()  => ResolveEnding(2);

    private void ResolveEnding(int ending)
    {
        Data.storyDone   = true;
        Data.storyEnding = ending;
        if (talkingWith == 0) Data.coins        += EconomyConfig.FamilyTreasureReward;
        else                  Data.player2Coins += EconomyConfig.FamilyTreasureReward;
        gridManager.Save();
        gridManager.NotifyWorldChanged();
        SoundManager.PlayCoin();

        activeLines = ending == 1
            ? new[] { "Bratr mlčky přikývne a sedne si k veslu. \"Tak jedem,\" řekne nakonec." }
            : new[] { "Vezmeš dědictví. Za zády necháš jen šumění vody a prázdný útes." };
        line = 0;
        showChoiceButtons = false;
        ignoreKeyUntil = Time.unscaledTime + 0.2f;
    }

    // ── Panáček (stejný postup jako StoryNpc — Kenney model, jiný tón barvy,
    //    ať není vzhledově totožný s dědou). ──────────────────────────────
    private void BuildFigure()
    {
        var model = CharacterModel.TryBuild(transform, "character-male-c",
            CharacterModel.DEFAULT_SCALE, new Color(0.62f, 0.60f, 0.58f));
        if (model != null) return;

        Material coat = MakeMat(new Color(0.24f, 0.26f, 0.30f));
        Material skin = MakeMat(new Color(0.72f, 0.58f, 0.46f));
        Material hair = MakeMat(new Color(0.75f, 0.74f, 0.70f));

        AddPart(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.55f, 0f), new Vector3(0.44f, 0.42f, 0.44f), coat);
        AddPart(PrimitiveType.Sphere,  "Head", new Vector3(0f, 1.02f, 0f), new Vector3(0.40f, 0.38f, 0.40f), skin);
        AddPart(PrimitiveType.Cube,    "Beard", new Vector3(0f, 0.90f, 0.17f), new Vector3(0.26f, 0.26f, 0.12f), hair);
    }

    private void AddPart(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = mat;
    }

    private static Material MakeMat(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
        return m;
    }

    // ── Update / OnGUI (stejný vzor jako StoryNpc) ───────────────────────
    void Update()
    {
        if (talkingWith == -1) return;
        if (Time.unscaledTime < ignoreKeyUntil) return;

        bool advance = talkingWith == 0
            ? (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
            : (Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.Keypad0));
        bool close = talkingWith == 0
            ? Input.GetKeyDown(KeyCode.Escape)
            : Input.GetKeyDown(KeyCode.KeypadEnter);

        if (close) { EndTalk(); return; }
        if (!advance) return;

        // Na poslední replice s volbou A/B se klávesou dál neposouvá — hráč
        // musí kliknout na tlačítko (nebo zavřít Escapem beze změny).
        if (showChoiceButtons && line >= activeLines.Length - 1) return;

        line++;
        ignoreKeyUntil = Time.unscaledTime + 0.12f;
        if (line >= activeLines.Length) { EndTalk(); return; }
        SoundManager.PlayClick();
    }

    private void EndTalk()
    {
        talkingWith     = -1;
        line            = 0;
        reopenAllowedAt = Time.unscaledTime + 0.35f;
    }

    void OnGUI()
    {
        if (!placed) return;
        InitStyles();

        if (talkingWith != -1) { DrawDialog(talkingWith); return; }

        MaybeHint(0);
        if (MultiplayerManager.IsMultiplayer) MaybeHint(1);
    }

    private void MaybeHint(int playerIndex)
    {
        int px = playerIndex == 0 ? gridManager.gameData.playerGridX : gridManager.gameData.player2GridX;
        int py = playerIndex == 0 ? gridManager.gameData.playerGridY : gridManager.gameData.player2GridY;

        float dist = Mathf.Max(Mathf.Abs(px - tilePos.x), Mathf.Abs(py - tilePos.y));
        if (dist > HintRange) return;

        Rect half = HalfRect(playerIndex);
        string key = playerIndex == 0 ? "E" : "Numpad 1";
        var r = new Rect(half.x, half.yMax - 90f, half.width, 26f);
        GUI.Label(r, "[" + key + "]  promluv s bratrem", hintStyle);
    }

    private void DrawDialog(int playerIndex)
    {
        Rect half = HalfRect(playerIndex);

        float w = Mathf.Min(620f, half.width - 40f);
        float h = 130f;
        var box = new Rect(half.x + (half.width - w) / 2f, half.yMax - h - 38f, w, h);

        GUI.color = new Color(0.06f, 0.07f, 0.10f, 0.92f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = new Color(0.55f, 0.6f, 0.65f, 1f); // studenější proužek než u dědy — jiná nálada
        GUI.DrawTexture(new Rect(box.x, box.y, box.width, 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(box.x + 18f, box.y + 10f, box.width - 36f, 24f), NpcName, nameStyle);
        string txt = activeLines != null && activeLines.Length > 0
            ? activeLines[Mathf.Clamp(line, 0, activeLines.Length - 1)] : "";
        GUI.Label(new Rect(box.x + 18f, box.y + 40f, box.width - 36f, 60f), txt, textStyle);

        string key = playerIndex == 0 ? "E" : "Numpad 1";
        bool lastLine = activeLines == null || line >= activeLines.Length - 1;

        if (showChoiceButtons && lastLine)
        {
            float bw = 220f;
            var brSpare = new Rect(box.x + box.width / 2f - bw - 10f, box.yMax - 30f, bw, 24f);
            var brKill  = new Rect(box.x + box.width / 2f + 10f,      box.yMax - 30f, bw, 24f);
            if (SoundManager.Click(GUI.Button(brSpare, "Ušetřit — vzít domů", spareStyle))) ChooseSpare();
            if (SoundManager.Click(GUI.Button(brKill,  "Zabít — vzít dědictví", killStyle))) ChooseKill();
        }
        else
        {
            string more = lastLine ? "[" + key + "] konec" : "[" + key + "] dál";
            GUI.Label(new Rect(box.x + 18f, box.yMax - 24f, box.width - 36f, 20f), more, hintStyle);
        }
    }

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

        nameStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15, fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.75f, 0.85f, 0.95f) }
        };
        textStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16, wordWrap = true,
            normal = { textColor = new Color(0.95f, 0.95f, 0.95f) }
        };
        hintStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.85f, 0.85f, 0.7f) }
        };
        spareStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13, fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = Solid(new Color(0.2f, 0.5f, 0.25f)) },
            hover  = { textColor = Color.white, background = Solid(new Color(0.3f, 0.65f, 0.35f)) }
        };
        killStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 13, fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = Solid(new Color(0.5f, 0.18f, 0.16f)) },
            hover  = { textColor = Color.white, background = Solid(new Color(0.65f, 0.25f, 0.22f)) }
        };
        stylesReady = true;
    }

    private static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}
