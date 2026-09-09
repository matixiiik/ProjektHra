using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  StoryNpc.cs
//  Příběhové NPC — starý námořník, který sedí na startovním ostrově. Vede hráče
//  příběhem po krocích (gameData.storyStep):
//    0 = start: řekne, ať si koupí aspoň malou loď a vrátí se
//    1 = má loď: chce se prokázat — 1000 mincí + historický poklad
//    2 = dostal souřadnice: navádí k příběhovému mega ostrovu (šipka na minimapě)
//    3 = hráč tam byl: poklad už někdo vykopal, ale nechal stopu
//    4+ = pokračování (doplní se později)
//
//  Objekt "StoryNpc" je v SampleScene. Panáčka i jeho umístění si vytvoří sám
//  v Start(). V majáku / jiných scénách není.
// ─────────────────────────────────────────────────────────────────────────────

public class StoryNpc : MonoBehaviour
{
    private const string NpcName   = "Starý námořník";
    private const float  HintRange = 2.4f; // na kolik políček se ukáže nápověda "zmáčkni E"

    public static StoryNpc Instance { get; private set; }

    private const int   PROVE_COST = 1000; // kolik mincí chce starý námořník

    private GridManager gridManager;
    private Vector2Int  tilePos;      // políčko, na kterém děda sedí
    private bool        placed;
    private int         talkingWith = -1; // 0 = P1, 1 = P2, -1 = nikdo
    private int         line;
    private string[]    activeLines;  // repliky pro aktuální rozhovor (podle storyStep)
    private bool        showGiveButton; // v kroku 1 se splněnými podmínkami
    private float       ignoreKeyUntil;
    private float       reopenAllowedAt;

    private GUIStyle nameStyle, textStyle, hintStyle, giveStyle;
    private bool     stylesReady;

    void Awake()  { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    private GameData Data => gridManager.gameData;
    private int StoryStep => Data.storyStep;

    // ── Dotazy pro PlayerController ─────────────────────────────────────────
    /// <summary>Sedí děda na tomhle políčku?</summary>
    public bool IsAt(int x, int y) => placed && x == tilePos.x && y == tilePos.y;

    /// <summary>Mluví zrovna tenhle hráč s dědou? (blokuje mu pohyb)</summary>
    public bool IsTalkingWith(int playerIndex) => talkingWith == playerIndex;

    /// <summary>Mluví s dědou někdo?</summary>
    public bool IsTalking => talkingWith != -1;

    /// <summary>Spustí dialog pro daného hráče (volá PlayerController po zmáčknutí E vedle dědy).</summary>
    public void StartTalk(int playerIndex)
    {
        if (talkingWith != -1) return;
        if (Time.time < reopenAllowedAt) return; // právě jsme dialog zavřeli — nech E "vyprchat"
        talkingWith    = playerIndex;
        line           = 0;
        BuildDialogForStep();
        ignoreKeyUntil = Time.time + 0.25f;
        SoundManager.PlayClick();
    }

    // ── Příběhový dialog ───────────────────────────────────────────────────
    // Sestaví repliky podle aktuálního kroku příběhu (a podmínek).
    private void BuildDialogForStep()
    {
        showGiveButton = false;
        int shipLevel  = talkingWith == 0 ? Data.shipLevel : Data.player2ShipLevel;
        int coins      = talkingWith == 0 ? Data.coins     : Data.player2Coins;

        switch (StoryStep)
        {
            case 0:
                if (shipLevel <= 0)
                    activeLines = new[]
                    {
                        "Á, návštěva. Vidím, že jsi vyplul na obyčejném voru.",
                        "Takhle daleko se nedostaneš, chlapče.",
                        "Kup si aspoň malou plachetnici v majáku a vrať se za mnou.",
                        "A pamatuj: myší střílíš z děla, klávesou E vejdeš do majáku " +
                        "nebo přístavu a klávesou R opravíš loď u mola.",
                    };
                else
                    activeLines = new[]
                    {
                        "Á, teď už máš pořádnou loď. Dobře.",
                        "Než tě pošlu za tím, co hledám, musím vědět, že ti můžu věřit.",
                        "Přines mi 1000 mincí a historický poklad.",
                        "Historický poklad občas bývá v pokladech ze starých map — z beden.",
                        "Vrať se, až budeš mít obojí.",
                    };
                break;

            case 1:
                if (coins >= PROVE_COST && Data.hasHistoricalTreasure)
                {
                    activeLines = new[]
                    {
                        "Tak co, máš pro mě 1000 mincí a ten historický poklad?",
                    };
                    showGiveButton = true;
                }
                else
                {
                    activeLines = new[]
                    {
                        "Ještě to nemáš. Chci 1000 mincí a historický poklad.",
                        "Ten historický vyplať ve výkupně (zeleny pult v majáku) — poznáš ho.",
                    };
                }
                break;

            case 2:
                activeLines = new[]
                {
                    $"Ostrov je na souřadnicích [{Data.storyIslandX}, {Data.storyIslandY}].",
                    "Máš to nahoře na obrazovce a na minimapě šipku. Drž se jí.",
                    "Je to daleko. Až tam budeš, poznáš to.",
                };
                break;

            case 3:
                activeLines = new[]
                {
                    "Vidím ti to na očích. Byl jsi tam.",
                    "Někdo tě předběhl. Poklad je pryč.",
                    "Ale ten, kdo kopal, nechal na obelisku vzkaz — stopu, kam dál.",
                    "Nech mě přemýšlet. Řeknu ti víc, až tomu porozumím.",
                };
                break;

            default:
                activeLines = new[]
                {
                    "Ta stopa nás dovede dál. Buď trpělivý, námořníku.",
                };
                break;
        }
    }

    // Volá se, když hráč dočte poslední repliku (nebo dialog ukončí).
    private void OnDialogFinished()
    {
        if (StoryStep == 0)
        {
            int shipLevel = talkingWith == 0 ? Data.shipLevel : Data.player2ShipLevel;
            if (shipLevel >= 1) { Data.storyStep = 1; gridManager.Save(); }
        }
        else if (StoryStep == 3)
        {
            Data.storyStep = 4;
            gridManager.Save();
        }
    }

    // Tlačítko "dát starému námořníkovi 1000 mincí + historický poklad" (krok 1).
    private void GiveToSailor()
    {
        int coins = talkingWith == 0 ? Data.coins : Data.player2Coins;
        if (coins < PROVE_COST || !Data.hasHistoricalTreasure) return;

        if (talkingWith == 0) Data.coins        -= PROVE_COST;
        else                  Data.player2Coins -= PROVE_COST;
        Data.hasHistoricalTreasure = false;

        // Vylosuj daleké místo pro příběhový mega ostrov (deterministicky podle
        // pozice hráče, ať to má každá hra jinde).
        int px = Data.playerGridX, py = Data.playerGridY;
        int hsh = unchecked((px * 92821) ^ (py * 68917) ^ 0x5bd1e995);
        float ang = ((hsh & 0xFFFF) / 65535f) * Mathf.PI * 2f;
        int dist  = 340 + ((hsh >> 16) & 0x7F); // 340..467 políček
        int sx = Mathf.RoundToInt(Mathf.Cos(ang) * dist);
        int sy = Mathf.RoundToInt(Mathf.Sin(ang) * dist);

        gridManager.PlaceMegaIsland(sx, sy);

        Data.storyStep   = 2;
        Data.hasWaypoint = true;   // šipka na minimapě povede k ostrovu
        Data.waypointX   = sx;
        Data.waypointY   = sy;
        gridManager.Save();
        gridManager.NotifyWorldChanged();
        SoundManager.PlayCoin();

        // Pokračuj rovnou navazujícími replikami.
        activeLines = new[]
        {
            "Výborně. Přesně tohle jsem potřeboval.",
            $"To, co hledám, je na ostrově na [{sx}, {sy}]. Daleko na moři.",
            "Máš to nahoře na obrazovce a na minimapě šipku. Vydej se tam.",
        };
        line = 0;
        showGiveButton = false;
        ignoreKeyUntil = Time.time + 0.2f;
    }

    /// <summary>Volá PlayerController, když hráč vstoupí na příběhový mega ostrov.</summary>
    public static void OnReachedStoryIsland()
    {
        var d = GameSession.Instance != null ? GameSession.Instance.Data : null;
        if (d == null || d.storyStep != 2) return;

        d.storyStep   = 3;
        d.hasWaypoint = false; // cíl splněn
        GameSession.Instance.Save();

        var grid = FindFirstObjectByType<GridManager>();
        if (grid != null) grid.NotifyWorldChanged();

        if (CombatDirector.Instance != null)
            CombatDirector.Instance.Toast("Někdo tu už kopal. Na obelisku je vzkaz — vrať se za starým námořníkem.");
    }

    // ───────────────────────────────────────────────────────────────────────
    void Start()
    {
        gridManager = FindFirstObjectByType<GridManager>();
        StartCoroutine(PlaceWhenWorldReady());
    }

    // Startovní ostrov je vygenerovaný hned v GridManager.Awake(), ale pro jistotu
    // (pořadí Awake/Start) zkusíme umístění i pár snímků po sobě. Navíc se okraj
    // ostrova občas dorovná až dodatečně — tak po chvíli zkontrolujeme, že dědovi
    // políčko nezůstalo na vodě, a případně ho přesadíme.
    private IEnumerator PlaceWhenWorldReady()
    {
        // Nech generaci startovního ostrova po startu doběhnout.
        for (int i = 0; i < 12; i++) yield return null;

        for (int tries = 0; tries < 120 && !placed; tries++)
        {
            TryPlace();
            if (placed) break;
            yield return null;
        }

        // Kontrola po usazení světa: první ~3 s po startu se okraj ostrova může
        // ještě dorovnat — kdyby dědovi políčko skončilo na vodě, přesadíme ho.
        for (int check = 0; check < 6; check++)
        {
            yield return new WaitForSeconds(0.5f);
            if (!placed || TileStillGood(tilePos)) continue;

            ClearFigure();
            placed = false;
            yield return null; // nech starou postavičku zmizet
            for (int tries = 0; tries < 60 && !placed; tries++)
            {
                TryPlace();
                if (placed) break;
                yield return null;
            }
        }
    }

    // Je políčko pořád dost "na ostrově" (obklopené pevninou, nebo aspoň 3 sousedi)?
    private bool TileStillGood(Vector2Int t)
        => SurroundedByLand(t) || LandNeighbourCount(t) >= 3;

    // Zahodí díly postavičky (před přesazením).
    private void ClearFigure()
    {
        foreach (Transform child in transform) Destroy(child.gameObject);
    }

    private void TryPlace()
    {
        if (gridManager == null) { gridManager = FindFirstObjectByType<GridManager>(); return; }

        var data = gridManager.gameData;

        // Už dřív usazený děda → použij ULOŽENÉ políčko (pokud je pořád v pořádku),
        // ať se po návratu z majáku nestěhuje.
        if (data.storyNpcPlaced)
        {
            var saved = new Vector2Int(data.storyNpcX, data.storyNpcY);
            if (TileStillGood(saved))
            {
                tilePos = saved;
                placed  = true;
                gridManager.ReserveNpcTile(tilePos.x, tilePos.y);
                BuildFigure();
                return;
            }
            // uložené políčko se rozpadlo (kraj ostrova se přeskládal) → vyber znovu
        }

        List<Vector2Int> harbor = gridManager.GetStartIslandHarborTiles();
        if (harbor.Count == 0) return;

        // Děda musí SEDĚT NA OSTROVĚ — jen políčko, které má kolem sebe (4-směrně)
        // samou pevninu (ne kraj / molo / vodu), aby nekoukal z ničeho. Vynech
        // taky políčka těsně u majáku. Referenční bod pro "nejblíž" je počátek
        // světa [0,0] (tam je startovní ostrov) — NE aktuální pozice hráče,
        // jinak by se děda "stěhoval" podle toho, odkud hráč zrovna přišel.
        Vector2Int origin = Vector2Int.zero;
        Vector2Int best = default; float bestDist = float.MaxValue; bool has = false;

        foreach (var t in harbor)
        {
            if (!SurroundedByLand(t)) continue;
            if (NextToLighthouse(t))  continue;
            float d = (t - origin).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = t; has = true; }
        }

        // Nouzovka: kdyby žádné plně obklopené nebylo (mrňavý ostrov), vezmi
        // aspoň políčko se 3 pevninovými sousedy dál od kraje.
        if (!has)
        {
            foreach (var t in harbor)
            {
                if (LandNeighbourCount(t) < 3 || NextToLighthouse(t)) continue;
                float d = (t - origin).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = t; has = true; }
            }
        }
        if (!has) return; // zkusíme příští snímek

        tilePos = best;
        placed  = true;

        // Ulož políčko napevno.
        data.storyNpcPlaced = true;
        data.storyNpcX      = best.x;
        data.storyNpcY      = best.y;
        gridManager.Save();

        // Na dědově políčku nesmí být žádná dekorace (kámen / palma) — ať trčí
        // ze země panáček, ne trs trávy. GridManager to zařídí i po opětovném
        // vygenerování dlaždice.
        gridManager.ReserveNpcTile(tilePos.x, tilePos.y);

        BuildFigure();
    }

    private bool NextToLighthouse(Vector2Int t) => HasNeighbour(t, TileType.Lighthouse);

    private bool HasNeighbour(Vector2Int t, TileType type)
    {
        return gridManager.GetTileType(t.x + 1, t.y) == type
            || gridManager.GetTileType(t.x - 1, t.y) == type
            || gridManager.GetTileType(t.x, t.y + 1) == type
            || gridManager.GetTileType(t.x, t.y - 1) == type;
    }

    private static bool IsLand(TileType t)
        => t == TileType.Harbor || t == TileType.Lighthouse || t == TileType.Chest;

    private bool SurroundedByLand(Vector2Int t)
        => IsLand(gridManager.GetTileType(t.x + 1, t.y))
        && IsLand(gridManager.GetTileType(t.x - 1, t.y))
        && IsLand(gridManager.GetTileType(t.x, t.y + 1))
        && IsLand(gridManager.GetTileType(t.x, t.y - 1));

    private int LandNeighbourCount(Vector2Int t)
    {
        int n = 0;
        if (IsLand(gridManager.GetTileType(t.x + 1, t.y))) n++;
        if (IsLand(gridManager.GetTileType(t.x - 1, t.y))) n++;
        if (IsLand(gridManager.GetTileType(t.x, t.y + 1))) n++;
        if (IsLand(gridManager.GetTileType(t.x, t.y - 1))) n++;
        return n;
    }

    // ── Panáček (stejné díly jako hráč, jen sedí a je starý) ─────────────────
    private void BuildFigure()
    {
        transform.position = new Vector3(tilePos.x, 0.02f, tilePos.y);

        // Otoč dědu směrem k nejbližšímu molu / k moři, ať kouká na hladinu.
        Vector2Int look = NearestPier() ?? new Vector2Int(tilePos.x, tilePos.y - 1);
        Vector3 dir = new Vector3(look.x - tilePos.x, 0f, look.y - tilePos.y);
        if (dir.sqrMagnitude > 0.001f) transform.rotation = Quaternion.LookRotation(dir);

        MaybeShowStartHint();

        // Nejdřív zkus Kenney model postavy (starý námořník = trochu vybledlá barva).
        // Když v Resources není, postav dědu ze základních tvarů jako dřív.
        if (CharacterModel.TryBuild(transform, "character-male-e",
                CharacterModel.DEFAULT_SCALE, new Color(0.78f, 0.75f, 0.72f)) != null)
            return;

        Material coat  = MakeMat(new Color(0.30f, 0.33f, 0.42f)); // obnošený modrý kabát
        Material skin  = MakeMat(new Color(0.83f, 0.66f, 0.53f));
        Material hair  = MakeMat(new Color(0.88f, 0.88f, 0.85f)); // šedé vlasy / vousy

        AddPart(PrimitiveType.Cube,     "Legs",  new Vector3(0f, 0.12f, 0.30f), new Vector3(0.48f, 0.16f, 0.64f), Vector3.zero,             coat);
        AddPart(PrimitiveType.Capsule,  "Body",  new Vector3(0f, 0.40f, -0.02f), new Vector3(0.44f, 0.40f, 0.44f), new Vector3(-12f, 0f, 0f), coat);
        AddPart(PrimitiveType.Sphere,   "Head",  new Vector3(0f, 0.78f, 0.03f), new Vector3(0.40f, 0.38f, 0.40f), Vector3.zero,             skin);
        AddPart(PrimitiveType.Cylinder, "Hair",  new Vector3(0f, 0.95f, 0.02f), new Vector3(0.46f, 0.07f, 0.46f), Vector3.zero,             hair);
        AddPart(PrimitiveType.Cube,     "Beard", new Vector3(0f, 0.66f, 0.17f), new Vector3(0.24f, 0.26f, 0.12f), Vector3.zero,             hair);
        AddPart(PrimitiveType.Cube,     "Nose",  new Vector3(0f, 0.77f, 0.23f), new Vector3(0.08f, 0.08f, 0.13f), Vector3.zero,             skin);
    }

    // Na úplném začátku (příběh v kroku 0) ukaž jednou krátkou nápovědu ovládání.
    private static bool startHintShown;
    private void MaybeShowStartHint()
    {
        if (startHintShown || StoryStep != 0) return;
        startHintShown = true;
        if (CombatDirector.Instance != null)
            CombatDirector.Instance.Toast(
                "Ovladani:  WASD plout  -  E maják/přístav  -  mys strilet  -  R opravit lod  -  M mapa  -  promluv s namornikem (E)",
                9f);
    }

    private Vector2Int? NearestPier()
    {
        Vector2Int[] dirs = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        foreach (var d in dirs)
            if (gridManager.GetTileType(tilePos.x + d.x, tilePos.y + d.y) == TileType.Pier)
                return tilePos + d;

        // molo nemusí být přímo vedle — projdi širší okolí
        for (int r = 2; r <= 8; r++)
            for (int x = -r; x <= r; x++)
                for (int y = -r; y <= r; y++)
                    if (gridManager.GetTileType(tilePos.x + x, tilePos.y + y) == TileType.Pier)
                        return new Vector2Int(tilePos.x + x, tilePos.y + y);
        return null;
    }

    private void AddPart(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col); // NPC nemá nic blokovat

        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale    = scale;
        go.transform.localEulerAngles = euler;

        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterial = mat;
    }

    private static Material MakeMat(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit")
                 ?? Shader.Find("Standard")
                 ?? Shader.Find("Universal Render Pipeline/Unlit");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
        return m;
    }

    // ── Dialog ─────────────────────────────────────────────────────────────
    void Update()
    {
        if (talkingWith == -1) return;
        if (Time.time < ignoreKeyUntil) return;

        bool advance = talkingWith == 0
            ? (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space))
            : (Input.GetKeyDown(KeyCode.Keypad1) || Input.GetKeyDown(KeyCode.Keypad0));
        bool close = talkingWith == 0
            ? Input.GetKeyDown(KeyCode.Escape)
            : Input.GetKeyDown(KeyCode.KeypadEnter);

        if (close) { OnDialogFinished(); EndTalk(); return; }
        if (!advance) return;

        // Na poslední replice s tlačítkem "dát" se klávesou dál neposouvá —
        // hráč musí kliknout na tlačítko (nebo zavřít Escapem).
        if (showGiveButton && line >= activeLines.Length - 1) return;

        line++;
        ignoreKeyUntil = Time.time + 0.12f;
        if (line >= activeLines.Length) { OnDialogFinished(); EndTalk(); return; }
        SoundManager.PlayClick();
    }

    private void EndTalk()
    {
        talkingWith     = -1;
        line            = 0;
        reopenAllowedAt = Time.time + 0.35f; // ať tentýž stisk E hned neotevře dialog znovu
    }

    void OnGUI()
    {
        if (!placed) return;
        InitStyles();

        if (talkingWith != -1) { DrawDialog(talkingWith); return; }

        // Nápověda "zmáčkni E", když je hráč pěšky blízko.
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
        GUI.Label(r, "[" + key + "]  promluv se starým námořníkem", hintStyle);
    }

    private void DrawDialog(int playerIndex)
    {
        Rect half = HalfRect(playerIndex);

        float w = Mathf.Min(620f, half.width - 40f);
        float h = 130f;
        var box = new Rect(half.x + (half.width - w) / 2f, half.yMax - h - 38f, w, h);

        GUI.color = new Color(0.06f, 0.07f, 0.10f, 0.92f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = new Color(0.9f, 0.75f, 0.35f, 1f);
        GUI.DrawTexture(new Rect(box.x, box.y, box.width, 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(box.x + 18f, box.y + 10f, box.width - 36f, 24f), NpcName, nameStyle);
        string txt = activeLines != null && activeLines.Length > 0
            ? activeLines[Mathf.Clamp(line, 0, activeLines.Length - 1)] : "";
        GUI.Label(new Rect(box.x + 18f, box.y + 40f, box.width - 36f, 60f), txt, textStyle);

        string key = playerIndex == 0 ? "E" : "Numpad 1";
        bool lastLine = activeLines == null || line >= activeLines.Length - 1;

        // Krok 1 se splněnými podmínkami: na poslední replice tlačítko "dát".
        if (showGiveButton && lastLine)
        {
            var br = new Rect(box.x + box.width / 2f - 150f, box.yMax - 30f, 300f, 24f);
            if (SoundManager.Click(GUI.Button(br, "Dát mu 1000 minci + historicky poklad", giveStyle)))
                GiveToSailor();
        }
        else
        {
            string more = lastLine ? "[" + key + "] konec" : "[" + key + "] dál";
            GUI.Label(new Rect(box.x + 18f, box.yMax - 24f, box.width - 36f, 20f), more, hintStyle);
        }
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

        nameStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15, fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.85f, 0.45f) }
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
        giveStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 14, fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = Solid(new Color(0.2f, 0.5f, 0.25f)) },
            hover  = { textColor = Color.white, background = Solid(new Color(0.3f, 0.65f, 0.35f)) }
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
