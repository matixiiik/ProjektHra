using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  MegaIslandMarker.cs
//  "Mozek" příběhového mega ostrova. Staví kamenný obelisk (vždy) a podle
//  gameData.megaIndex (0/1/2 = který ostrov v pořadí) jeho konkrétní obsah —
//  zatím jen placeholder cedule, skutečnou obranu/trezor/konfrontaci přidají
//  další kroky podle .claude/story-plan.md. Vytváří ho GridManager.PlaceMegaIsland
//  (a znovu po načtení save, pokud je storyIslandActive).
//
//  Ve hře je aktivní vždy nejvýš jeden mega ostrov najednou, proto stačí
//  jednoduchý statický Instance — přes něj se s ostrovem dá interagovat
//  (PlayerController) i ho testovat (GameConsole).
// ─────────────────────────────────────────────────────────────────────────────

public class MegaIslandMarker : MonoBehaviour
{
    public static MegaIslandMarker Instance { get; private set; }

    private GridManager gridManager;
    private Vector2Int  tilePos;  // políčko obelisku (= střed ostrova)
    private string      signText; // co cedule říká — vrátí se jako toast při interakci

    void Awake()     { Instance = this; }
    void OnDestroy() { if (Instance == this) Instance = null; }

    void Start()
    {
        gridManager = FindFirstObjectByType<GridManager>();
        tilePos = new Vector2Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.z));

        BuildObelisk();

        int megaIndex = gridManager != null ? gridManager.gameData.megaIndex : 0;
        switch (megaIndex)
        {
            case 0: BuildFortress();       break;
            case 1: BuildWreckGraveyard(); break;
            default: BuildConfrontation(); break; // 2 (a jistota pro budoucí)
        }
    }

    // ── Obsah ostrova podle megaIndex — zatím jen placeholder cedule ─────────
    // Skutečnou obranu (děla + LandGuard), trezor s puzzlem atd. staví další
    // kroky plánu (§3 a dál v .claude/story-plan.md).
    private void BuildFortress()
        => BuildSign("Mega ostrov 1 — Pevnost staré posádky (TODO: obrana + trezor)",
                      new Color(0.5f, 0.24f, 0.18f));

    private void BuildWreckGraveyard()
        => BuildSign("Mega ostrov 2 — Hřbitov lodí (TODO: hlídač + puzzle z vraků)",
                      new Color(0.32f, 0.36f, 0.42f));

    private void BuildConfrontation()
        => BuildSign("Mega ostrov 3 — Kde to začalo (TODO: konfrontace s bratrem)",
                      new Color(0.52f, 0.44f, 0.16f));

    // Dřevěná cedule kousek od obelisku — jen orientační, dokud nevznikne
    // skutečný obsah ostrova. Text se ukáže jako toast při interakci (E).
    private void BuildSign(string text, Color boardColor)
    {
        signText = text;

        Material wood  = MakeMat(new Color(0.35f, 0.24f, 0.14f));
        Material board = MakeMat(boardColor);

        Box("SignPost",  new Vector3(1.8f, 0.6f,  1.8f), new Vector3(0.12f, 1.2f, 0.12f), wood);
        Box("SignBoard", new Vector3(1.8f, 1.25f, 1.8f), new Vector3(1.1f,  0.6f, 0.08f),  board);
    }

    // ── Interakce (hák z PlayerController.TryInteractAdjacentBuilding) ──────
    // Vrací true, když hráč stojí vedle obelisku a stisk E patří tomuhle
    // ostrovu — v dalších krocích přibudou další interaktivní body (tabulky,
    // trezor, vzkaz), zatím je tu jen samotný obelisk s cedulí.
    public bool TryInteract(int x, int y, int playerIndex)
    {
        if (x != tilePos.x || y != tilePos.y) return false;
        if (CombatDirector.Instance != null) CombatDirector.Instance.Toast(signText);
        return true;
    }

    // ── Vizuál obelisku (beze změny z předchozí verze) ───────────────────────
    private void BuildObelisk()
    {
        Material stone = MakeMat(new Color(0.42f, 0.40f, 0.38f));
        Material glow  = MakeMat(new Color(0.55f, 0.85f, 0.95f));
        if (glow.HasProperty("_EmissionColor"))
        {
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(0.35f, 0.7f, 0.95f) * 2f);
        }

        // Stupňovitý podstavec + obelisk.
        Box("Base",   new Vector3(0f, 0.15f, 0f), new Vector3(3.0f, 0.3f, 3.0f), stone);
        Box("Base2",  new Vector3(0f, 0.45f, 0f), new Vector3(2.2f, 0.3f, 2.2f), stone);
        Box("Shaft",  new Vector3(0f, 2.4f, 0f),  new Vector3(0.8f, 3.6f, 0.8f), stone);
        Box("Cap",    new Vector3(0f, 4.4f, 0f),  new Vector3(0.5f, 0.5f, 0.5f), glow);

        var lightGo = new GameObject("MarkerLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 4.4f, 0f);
        var l = lightGo.AddComponent<Light>();
        l.type      = LightType.Point;
        l.color     = new Color(0.55f, 0.85f, 0.95f);
        l.range     = 14f;
        l.intensity = 2.5f;
    }

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
