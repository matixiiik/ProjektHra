using System.Collections.Generic;
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

    // ── Obrana ostrova 1 (Krok 2) — obsazeno jen když megaIndex == 0 ────────
    private readonly List<HostileIslandCannon> myCannons = new List<HostileIslandCannon>();
    private readonly List<LandGuard>           myGuards  = new List<LandGuard>();
    private PirateShip myGuardShip;
    private bool        defenseSpawned; // ať Update() nezačne počítat dřív, než se obrana vůbec postaví

    // ── Trezor + puzzle (Krok 3) — vzniká, až padne obrana (megaTask >= 1) ──
    private VaultMechanism vault;
    private bool           vaultBuilt;

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

    // Sečti zničenou obranu → megaTask 0→1. Jen dokud je co počítat (defenseSpawned)
    // a dokud je pořád megaTask == 0 (jinak by po zničení poslední věci nic nedělalo).
    void Update()
    {
        if (!defenseSpawned || gridManager == null) return;
        var d = gridManager.gameData;
        if (d.megaTask != 0) return;

        myCannons.RemoveAll(c => c == null);
        myGuards.RemoveAll(g => g == null);
        if (myCannons.Count > 0 || myGuards.Count > 0 || myGuardShip != null) return;

        d.megaTask = 1;
        gridManager.Save();
        gridManager.NotifyWorldChanged();
        if (CombatDirector.Instance != null) CombatDirector.Instance.Toast("Obrana ostrova padla. Prohledej ho dál.");
        BuildVaultIfNeeded();
    }

    // Postaví trezor, jakmile obrana padla (megaTask >= 1) — buď hned po
    // zničení poslední hlídky (výše v Update()), nebo při načtení savu, kde
    // už obrana dřív padla (viz BuildFortress, větev pro megaTask > 0).
    private void BuildVaultIfNeeded()
    {
        if (vaultBuilt || gridManager == null || gridManager.gameData.megaTask < 1) return;
        vaultBuilt = true;

        Vector2Int spot = tilePos; // nouzovka, kdyby se nenašlo nic lepšího
        var candidates = FindGuardTiles(1);
        if (candidates.Count > 0) spot = candidates[0];

        bool alreadySolved = gridManager.gameData.megaTask >= 2;
        vault = VaultMechanism.Spawn(spot, alreadySolved);
    }

    // ── Obsah ostrova podle megaIndex ─────────────────────────────────────
    // Ostrov 2 a 3 mají zatím jen placeholder ceduli — skutečný obsah staví
    // další kroky plánu (§5 a dál v .claude/story-plan.md).
    private void BuildFortress()
    {
        BuildSign("Mega ostrov 1 — Pevnost staré posádky. Kolem obelisku hlídkuje ozbrojená posádka — trezor je někde uvnitř.",
                   new Color(0.5f, 0.24f, 0.18f));

        if (gridManager == null) return;

        // Obrana se staví, jen když ještě nebyla vyřízená (staré savy po
        // reloadu ať znovu nespawnou už poražené hlídky) — místo toho rovnou
        // postav trezor, ten na megaTask 0 nezávisí.
        if (gridManager.gameData.megaTask > 0) { BuildVaultIfNeeded(); return; }

        // 2–3 děla + 2–3 strážci, deterministicky podle pozice ostrova.
        int seed = Mathf.Abs(unchecked(tilePos.x * 73856093 ^ tilePos.y * 19349663));
        int cannonCount = 2 + seed % 2;
        int guardCount  = 2 + (seed / 2) % 2;

        foreach (var spot in gridManager.GetHostileCannonSpots(tilePos, cannonCount, TileType.MegaIsland))
        {
            var cannon = HostileIslandCannon.Spawn(spot, "mega");
            myCannons.Add(cannon);
            if (CombatDirector.Instance != null) CombatDirector.Instance.RegisterCannon(cannon);
        }

        var guardWaterSpots = gridManager.GetGuardWaterSpots(tilePos, 1);
        if (guardWaterSpots.Count > 0)
        {
            var w = guardWaterSpots[0];
            myGuardShip = PirateShip.Spawn(new Vector3(w.x, 0f, w.y), 1); // střední loď
            myGuardShip.SetGuard(new Vector3(w.x, 0f, w.y));
            myGuardShip.guardIslandKey = "mega";
            if (CombatDirector.Instance != null) CombatDirector.Instance.RegisterGuardShip(myGuardShip);
        }

        foreach (var spot in FindGuardTiles(guardCount))
            myGuards.Add(LandGuard.Spawn(spot, "mega"));

        defenseSpawned = true;
    }

    // Pár políček pevniny blíž ke středu ostrova (u budoucího trezoru), kam
    // postavit stacionární strážce. Podobný postup jako u děl, jen blíž středu.
    private List<Vector2Int> FindGuardTiles(int maxCount)
    {
        var cand = new List<Vector2Int>();
        for (int x = tilePos.x - 6; x <= tilePos.x + 6; x++)
            for (int y = tilePos.y - 6; y <= tilePos.y + 6; y++)
            {
                if (x == tilePos.x && y == tilePos.y) continue; // ne přímo na obelisku
                if (gridManager.GetTileType(x, y) != TileType.MegaIsland) continue;
                cand.Add(new Vector2Int(x, y));
            }
        cand.Sort((a, b) => DistSqToCenter(a) - DistSqToCenter(b));

        var pick = new List<Vector2Int>();
        foreach (var c in cand)
        {
            bool tooClose = false;
            foreach (var p in pick)
                if (Mathf.Abs(p.x - c.x) < 3 && Mathf.Abs(p.y - c.y) < 3) { tooClose = true; break; }
            if (tooClose) continue;
            pick.Add(c);
            if (pick.Count >= maxCount) break;
        }
        return pick;
    }

    private int DistSqToCenter(Vector2Int p)
        => (p.x - tilePos.x) * (p.x - tilePos.x) + (p.y - tilePos.y) * (p.y - tilePos.y);

    private void BuildWreckGraveyard()
        => BuildSign("Mega ostrov 2 — Hřbitov lodí. Zatím je tu ticho.",
                      new Color(0.32f, 0.36f, 0.42f));

    private void BuildConfrontation()
        => BuildSign("Mega ostrov 3 — Kde to začalo. Zatím je tu ticho.",
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
    // Vrací true, když hráč stojí vedle obelisku (nebo živého strážce) a stisk
    // E patří tomuhle ostrovu. V dalších krocích přibudou další interaktivní
    // body (tabulky, trezor, vzkaz) — stejnou metodou, viz plán §2.
    public bool TryInteract(int x, int y, int playerIndex)
    {
        // Nejdřív živí strážci — E vedle nich = úder (viz LandGuard.PLAYER_HIT).
        foreach (var guard in myGuards)
            if (guard != null && guard.IsAt(x, y))
            {
                guard.TakeHit(LandGuard.PLAYER_HIT);
                SoundManager.PlayHit();
                return true;
            }

        // Trezor — dokud není vyřešený, E otevře puzzle; po vyřešení E přečte
        // vzkaz uvnitř (jednou — pak posune příběh na další ostrov).
        if (vault != null && vault.IsAt(x, y))
            return vault.Solved ? TryReadMessage() : vault.Open(playerIndex);

        if (x != tilePos.x || y != tilePos.y) return false;
        if (CombatDirector.Instance != null) CombatDirector.Instance.Toast(signText);
        return true;
    }

    /// <summary>Co udělá E na daném políčku — bez vedlejších účinků, jen na
    /// vypsání nápovědy (viz PlayerController.GetContextHint). Vrací null, když
    /// tam není nic k udělání.</summary>
    public string GetHint(int x, int y)
    {
        foreach (var guard in myGuards)
            if (guard != null && guard.IsAt(x, y)) return "zaútočit na stráž";

        if (vault != null && vault.IsAt(x, y))
        {
            if (!vault.Solved) return "otevřít trezor";
            return gridManager != null && gridManager.gameData.megaTask < 3 ? "přečíst vzkaz" : null;
        }

        if (x == tilePos.x && y == tilePos.y) return "prozkoumat obelisk";
        return null;
    }

    /// <summary>Zavolá LandGuard při své smrti — jen okamžitá hláška. Skutečný postup
    /// (megaTask) řeší Update() nahoře, který sečte, co všechno ještě žije.</summary>
    public void OnGuardDestroyed(LandGuard g)
    {
        if (CombatDirector.Instance != null) CombatDirector.Instance.Toast("Stráž poražena.");
    }

    // Přečtení vzkazu v trezoru (Krok 4) — jen jednou, pak posune příběh na
    // další mega ostrov. Text i logika platí zatím jen pro ostrov 1 (bratrův
    // první vzkaz) — ostrov 2 dostane vlastní text, až přijde na řadu.
    private bool TryReadMessage()
    {
        if (gridManager == null) return true;
        var d = gridManager.gameData;

        if (d.megaTask >= 3)
        {
            if (CombatDirector.Instance != null) CombatDirector.Instance.Toast("Vzkaz už jsi přečetl.");
            return true;
        }
        if (d.megaTask != 2) return true; // pojistka — nemělo by nastat (vault.Solved už megaTask=2 zajišťuje)

        d.megaTask = 3;
        gridManager.Save();

        if (CombatDirector.Instance != null)
            CombatDirector.Instance.Toast(
                "Vzkaz: \"Přišel jsi pozdě. Mám to já — měl jsem to celou dobu. "
              + "Jestli fakt chcete, co je rodiny, přijeď si pro to sám.\"", 7f);

        gridManager.GiveNextMegaIsland(); // umístí ostrov 2, nastaví waypoint, zničí tenhle marker
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
