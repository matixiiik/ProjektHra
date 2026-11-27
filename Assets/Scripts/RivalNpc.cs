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
//  bratrova hlídková loď (megaTask 0→1) — finále má tedy dvě fáze: bitva s lodí
//  (bratr volá z paluby, viz MegaIslandMarker.UpdateBrotherTaunts) a rozhovor na
//  břehu. Volba se ukáže jako pergamenový list se dvěma tlačítky (DrawChoiceLetter).
//  Po zavření dialogu se konec projeví ve světě (ApplyEndingToWorld): bratr zmizí —
//  buď odplul s hráčem domů k dědovi, nebo po něm zůstane hrob.
// ─────────────────────────────────────────────────────────────────────────────

public class RivalNpc : MonoBehaviour
{
    private static string NpcName => Loc.T("Starší bratr", "Elder Brother"); // jméno v rámečku dialogu
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
    private bool        endingApplied;     // konec už se projevil ve světě (jen jednou)

    private GUIStyle nameStyle, textStyle, hintStyle;
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
                ? Loc.Pick(new[] { "Připrav loď. Pojedu s tebou." },
                           new[] { "Get the boat ready. I'm coming with you." })
                : Loc.Pick(new[] { "Máš, cos chtěl. Zbytek je tvoje věc." },
                           new[] { "You have what you wanted. The rest is your business." });
            return;
        }

        activeLines = Loc.Pick(
            new[]
            {
                "Tak přece jsi přijel.",
                "Roky jsem čekal, že se pro mě někdo vrátí. Nikdo nepřijel.",
                "Otec s malým bratrem odpluli domů. Mě nechali na tom prokletém útesu.",
                "Vyrostl jsem tady sám a živil se, jak se dalo. Zestárl jsem u moře, které mi vzalo rodinu.",
                "Rodina? Ta si po letech řekla, že jsem utonul. Pohřbili mě, jako bych byl nikdo.",
                "Potkal jsem dokonce i jeho syna, víš. I toho si nakonec vzalo moře — bere si, co chce, a nikoho se neptá.",
                DiaryPages.Count(Data) >= DiaryPages.COUNT
                    ? "Četl jsi můj deník, viď? Všech šest stránek. Tak víš, jaké to bylo — nemusím ti nic vysvětlovat."
                    : "Tohle jste s dědou tak dlouho hledali — rodinné dědictví. Celé ty roky jsem ho měl u sebe.",
                "Tak co bude, chlapče? Vezmeš mě domů, nebo si vezmeš jen tohle?",
            },
            new[]
            {
                "So you came after all.",
                "For years I waited for someone to come back for me. Nobody came.",
                "Father and my little brother sailed home. They left me on this cursed cliff.",
                "I grew up here alone, living off whatever I could find. I grew old beside the sea that took my family.",
                "Family? After some years they decided I had drowned. They buried me as if I were nobody.",
                "I even met his son along the way, you know. The sea took him too in the end — it takes what it wants and asks no one.",
                DiaryPages.Count(Data) >= DiaryPages.COUNT
                    ? "You read my diary, didn't you? All six pages. Then you know what it was like — I don't have to explain a thing."
                    : "This is what you and Grandpa have been searching for all this time — the family inheritance. I've had it with me all these years.",
                "So what will it be, lad? Will you take me home, or will you take only this?",
            });
        showChoiceButtons = true;
    }

    private void ChooseSpare() => ResolveEnding(1);
    private void ChooseKill()  => ResolveEnding(2);

    private void ResolveEnding(int ending)
    {
        Data.storyDone   = true;
        Data.storyEnding = ending;
        Data.players[talkingWith].coins += EconomyConfig.FamilyTreasureReward;
        gridManager.Save();
        gridManager.NotifyWorldChanged();
        SoundManager.PlayCoin();

        activeLines = ending == 1
            ? Loc.Pick(new[] { "Bratr mlčky přikývne a sedne si k veslu. „Tak jedem,“ řekne nakonec." },
                       new[] { "The brother nods in silence and sits down at the oar. “Let’s go, then,” he says at last." })
            : Loc.Pick(new[] { "Vezmeš si dědictví. Za zády zůstane jen šumění vody a prázdný útes." },
                       new[] { "You take the inheritance. Behind you there is only the murmur of the water and an empty cliff." });
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
        int who = talkingWith;
        talkingWith     = -1;
        line            = 0;
        reopenAllowedAt = Time.unscaledTime + 0.35f;

        // Po volbě se konec projeví ve světě — až po zavření dialogu, ať hráč dočte repliku.
        if (Data.storyDone && !endingApplied) ApplyEndingToWorld(who);
    }

    // Viditelný konec (fáze C): šipka domů k dědovi, bratr zmizí (doma u dědy / hrob).
    private void ApplyEndingToWorld(int playerIndex)
    {
        endingApplied = true;

        // Oběma hráčům šipka na minimapě ke dědovi — HUD píše "vrať se za dědou".
        if (Data.storyNpcPlaced)
            foreach (var ps in Data.players)
            {
                ps.hasWaypoint = true;
                ps.waypointX   = Data.storyNpcX;
                ps.waypointY   = Data.storyNpcY;
            }
        gridManager.Save();
        gridManager.NotifyWorldChanged();

        if (StoryNpc.Instance != null) StoryNpc.Instance.RefreshCompanion(); // bratr se objeví doma (konec 1)
        if (CombatDirector.Instance != null)
            CombatDirector.Instance.Toast(Data.storyEnding == 1
                ? Loc.T("Bratr nastoupil k tobě na loď. Zavez ho domů za dědou — šipka na minimapě.",
                        "The brother has come aboard. Take him home to Grandpa — follow the minimap arrow.")
                : Loc.T("Na útesu zůstal jen hrob. Dědictví vezeš domů za dědou — šipka na minimapě.",
                        "Only a grave remains on the cliff. Take the inheritance home to Grandpa — follow the minimap arrow."), 6f);

        // Zničí i tenhle objekt (bratr z ostrova odejde) — proto až úplně na konci.
        if (MegaIslandMarker.Instance != null) MegaIslandMarker.Instance.ApplyEnding(Data.storyEnding);
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
        int px = gridManager.gameData.players[playerIndex].gridX;
        int py = gridManager.gameData.players[playerIndex].gridY;

        float dist = Mathf.Max(Mathf.Abs(px - tilePos.x), Mathf.Abs(py - tilePos.y));
        if (dist > HintRange) return;

        Rect half = HalfRect(playerIndex);
        string key = playerIndex == 0 ? "E" : "Numpad 1";
        var r = new Rect(half.x, half.yMax - 90f, half.width, 26f);
        GUI.Label(r, "[" + key + "]  " + Loc.T("promluv s bratrem", "talk to the brother"), hintStyle);
    }

    private void DrawDialog(int playerIndex)
    {
        Rect half = HalfRect(playerIndex);

        // Škálování podle výšky obrazovky — stejně jako dialog dědy (StoryNpc.DrawDialog).
        float s = HudSkin.GuiScale;
        nameStyle.fontSize  = Mathf.RoundToInt(20f * s);
        textStyle.fontSize  = Mathf.RoundToInt(21f * s);
        hintStyle.fontSize  = Mathf.RoundToInt(16f * s);

        float w = Mathf.Min(780f * s, half.width - 40f);
        float h = 200f * s;
        var box = new Rect(half.x + (half.width - w) / 2f, half.yMax - h - 38f, w, h);

        GUI.color = new Color(0.06f, 0.07f, 0.10f, 0.94f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = new Color(0.55f, 0.6f, 0.65f, 1f); // studenější proužek než u dědy — jiná nálada
        GUI.DrawTexture(new Rect(box.x, box.y, box.width, 3f * s), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float pad = 20f * s;
        GUI.Label(new Rect(box.x + pad, box.y + 10f * s, box.width - 2 * pad, 30f * s), NpcName, nameStyle);
        string txt = activeLines != null && activeLines.Length > 0
            ? activeLines[Mathf.Clamp(line, 0, activeLines.Length - 1)] : "";
        GUI.Label(new Rect(box.x + pad, box.y + 46f * s, box.width - 2 * pad, 100f * s), txt, textStyle);

        string key = playerIndex == 0 ? "E" : "Numpad 1";
        bool lastLine = activeLines == null || line >= activeLines.Length - 1;

        if (showChoiceButtons && lastLine)
        {
            DrawChoiceLetter(half, box, s);
        }
        else
        {
            string more = lastLine ? "[" + key + "] " + Loc.T("konec", "close")
                                   : "[" + key + "] " + Loc.T("dál",   "next");
            GUI.Label(new Rect(box.x + pad, box.yMax - 34f * s, box.width - 2 * pad, 26f * s), more, hintStyle);
        }
    }

    // Volba jako pergamenový list nad dialogem: krátké shrnutí a dvě tlačítka.
    // Schválně vážnější vzhled než běžná replika — tohle rozhodnutí nejde vzít zpět.
    private void DrawChoiceLetter(Rect half, Rect dialogBox, float s)
    {
        ShopUI.Prepare(s);
        float w = Mathf.Min(640f * s, half.width - 40f);
        float h = Mathf.Min(230f * s, dialogBox.y - half.y - 20f); // v malém okně se zmenší, ať nevyleze nahoru
        var sheet = new Rect(half.x + (half.width - w) / 2f, dialogBox.y - h - 12f * s, w, h);
        GUI.DrawTexture(sheet, HudSkin.Parchment(), ScaleMode.StretchToFill);

        float pad = 30f * s;
        GUILayout.BeginArea(new Rect(sheet.x + pad, sheet.y + 16f * s, sheet.width - 2f * pad, sheet.height - 28f * s));
        GUILayout.Label(Loc.T("Rozhodnutí", "The Choice"), ShopUI.Title);
        GUILayout.Label(Loc.T("Bratr čeká na odpověď. Tohle rozhodnutí už nepůjde vzít zpět.",
                              "The brother is waiting for your answer. This decision cannot be undone."), ShopUI.Row);
        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        if (SoundManager.Click(GUILayout.Button(Loc.T("Ušetřit ho — vzít ho domů za dědou", "Spare him — take him home to Grandpa"),
                                                ShopUI.ClaimButton, GUILayout.Height(40f * s)))) ChooseSpare();
        GUILayout.Space(12f * s);
        if (SoundManager.Click(GUILayout.Button(Loc.T("Zabít ho — vzít si dědictví", "Kill him — take the inheritance"),
                                                ShopUI.Button, GUILayout.Height(40f * s)))) ChooseKill();
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
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
        stylesReady = true;
    }
}
