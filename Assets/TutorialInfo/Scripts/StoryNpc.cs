using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  StoryNpc.cs
//  Příběhové NPC — starý námořník ("děda"), který sedí na základním (startovním)
//  ostrově a kouká na moře. Když k němu hráč přijde pěšky a zmáčkne E (P1) /
//  Numpad1 (P2), spustí se krátký dialog.
//
//  Zatím je to NÁČRT příběhu: děda říká, že kdysi schoval svůj poklad na jednom
//  z ostrovů a je jeho poslední přání, aby ho hráč našel. (Navázání na skutečný
//  úkol/odměnu doděláme později.)
//
//  Objekt "StoryNpc" je v SampleScene. Panáčka i jeho umístění si vytvoří sám
//  v Start() — nic se nezapojuje v inspektoru. V majáku / jiných scénách není.
// ─────────────────────────────────────────────────────────────────────────────

public class StoryNpc : MonoBehaviour
{
    // Repliky dědy (náčrt — text se bude ladit).
    private static readonly string[] Lines =
    {
        "Á, návštěva. Pojď blíž, mladý námořníku.",
        "Jsem starý a moře už mě dávno nechce.",
        "Ale mám jedno poslední přání, než tu skončím.",
        "Kdysi jsem na jednom z ostrovů schoval truhlu — celý svůj poklad.",
        "Najdi ji pro mě. Ať po mně něco zůstane.",
        "Víc ti teď neřeknu. Až budeš připravený, vrať se za mnou.",
    };

    private const string NpcName   = "Starý námořník";
    private const float  HintRange = 2.4f; // na kolik políček se ukáže nápověda "zmáčkni E"

    private GridManager gridManager;
    private Vector2Int  tilePos;      // políčko, na kterém děda sedí
    private bool        placed;
    private int         talkingWith = -1; // 0 = P1, 1 = P2, -1 = nikdo
    private int         line;
    private float       ignoreKeyUntil;  // aby E, kterým se dialog otevřel, hned nepřeskočilo první repliku
    private float       reopenAllowedAt; // krátká pauza po konci dialogu, ať tentýž stisk E dialog hned neotevře znovu

    private GUIStyle nameStyle, textStyle, hintStyle;
    private bool     stylesReady;

    // ── Dotazy pro PlayerController ─────────────────────────────────────────
    /// <summary>Sedí děda na tomhle políčku?</summary>
    public bool IsAt(int x, int y) => placed && x == tilePos.x && y == tilePos.y;

    /// <summary>Mluví zrovna tenhle hráč s dědou? (blokuje mu pohyb)</summary>
    public bool IsTalkingWith(int playerIndex) => talkingWith == playerIndex;

    /// <summary>Spustí dialog pro daného hráče (volá PlayerController po zmáčknutí E vedle dědy).</summary>
    public void StartTalk(int playerIndex)
    {
        if (talkingWith != -1) return;
        if (Time.time < reopenAllowedAt) return; // právě jsme dialog zavřeli — nech E "vyprchat"
        talkingWith    = playerIndex;
        line           = 0;
        ignoreKeyUntil = Time.time + 0.25f;
        SoundManager.PlayClick();
    }

    // ───────────────────────────────────────────────────────────────────────
    void Start()
    {
        gridManager = FindFirstObjectByType<GridManager>();
        StartCoroutine(PlaceWhenWorldReady());
    }

    // Startovní ostrov je vygenerovaný hned v GridManager.Awake(), ale pro jistotu
    // (pořadí Awake/Start) zkusíme umístění i pár snímků po sobě.
    private IEnumerator PlaceWhenWorldReady()
    {
        for (int tries = 0; tries < 120 && !placed; tries++)
        {
            TryPlace();
            if (placed) yield break;
            yield return null;
        }
    }

    private void TryPlace()
    {
        if (gridManager == null) { gridManager = FindFirstObjectByType<GridManager>(); return; }

        List<Vector2Int> harbor = gridManager.GetStartIslandHarborTiles();
        if (harbor.Count == 0) return;

        // Děda musí SEDĚT NA OSTROVĚ — jen políčko, které má kolem sebe (4-směrně)
        // samou pevninu (ne kraj / molo / vodu), aby nekoukal z ničeho. Vynech
        // taky políčka těsně u majáku. Z vyhovujících vezmi to nejblíž startu hráče.
        Vector2Int spawn = new Vector2Int(gridManager.gameData.playerGridX, gridManager.gameData.playerGridY);
        Vector2Int best = default; float bestDist = float.MaxValue; bool has = false;

        foreach (var t in harbor)
        {
            if (!SurroundedByLand(t)) continue;
            if (NextToLighthouse(t))  continue;
            float d = (t - spawn).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = t; has = true; }
        }

        // Nouzovka: kdyby žádné plně obklopené nebylo (mrňavý ostrov), vezmi
        // aspoň políčko se 3 pevninovými sousedy dál od kraje.
        if (!has)
        {
            foreach (var t in harbor)
            {
                if (LandNeighbourCount(t) < 3 || NextToLighthouse(t)) continue;
                float d = (t - spawn).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = t; has = true; }
            }
        }
        if (!has) return; // zkusíme příští snímek

        tilePos = best;
        placed  = true;

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

        if (close) { EndTalk(); return; }
        if (!advance) return;

        line++;
        ignoreKeyUntil = Time.time + 0.12f;
        if (line >= Lines.Length) { EndTalk(); return; }
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
        GUI.Label(new Rect(box.x + 18f, box.y + 40f, box.width - 36f, 60f), Lines[Mathf.Clamp(line, 0, Lines.Length - 1)], textStyle);

        string key = playerIndex == 0 ? "E" : "Numpad 1";
        string more = line >= Lines.Length - 1 ? "[" + key + "] konec" : "[" + key + "] dál";
        GUI.Label(new Rect(box.x + 18f, box.yMax - 24f, box.width - 36f, 20f), more, hintStyle);
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
        stylesReady = true;
    }
}
