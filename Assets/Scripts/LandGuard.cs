using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  LandGuard.cs
//  Stacionární strážce na pevnině — zatím jen na mega ostrovech příběhu (viz
//  .claude/story-plan.md, ostrov 1 "Pevnost staré posádky"). Nehýbe se z místa;
//  když je hráč PĚŠKY v dostřelu, střílí po něm stejně jako ostrovní dělo
//  (CannonBall, Side.Enemy — CannonBall to pak pošle do PlayerController.
//  DamagePlayer, ne DamageBoat, protože hráč je pěšky).
//
//  Hráč ho sejme tak, že k němu dojde a zmáčkne E — MegaIslandMarker.TryInteract
//  zavolá TakeHit(PLAYER_HIT). Žádná nová zbraň pro hráče zatím není (plán
//  počítá s tím, že se doladí později — "v1: koule jako dělo"). Zásah
//  MegaIslandMarker.OnGuardDestroyed rozdá i drobnou odměnu (EconomyConfig.
//  LandGuardReward).
//
//  Vzhled: Kenney model (character-male-c, stejný jako prodavači/bratr) v
//  olivové barvě + muška — fallback na primitivního panáčka, když model chybí.
// ─────────────────────────────────────────────────────────────────────────────

public class LandGuard : MonoBehaviour
{
    private const float RANGE      = 7f;   // dostřel (v políčkách)
    private const float RELOAD     = 2.2f; // pauza mezi výstřely
    private const float DAMAGE     = 6f;   // kolik ubere hráči jeden zásah
    private const float MAX_HP     = 8f;   // kolik "úderů" hráče vydrží (viz PLAYER_HIT)
    public  const float PLAYER_HIT = 2f;   // kolik ubere strážci jeden zásah od hráče (E)

    public  string     islandKey;   // klíč mega ostrova ("mega"), pro budoucí rozšíření
    private Vector2Int tile;        // políčko, na kterém strážce stojí (nehýbe se)
    private float       hp = MAX_HP;
    private float       nextShot;

    /// <summary>Vytvoří strážce na daném políčku ostrova.</summary>
    public static LandGuard Spawn(Vector2Int tile, string islandKey)
    {
        var root = new GameObject("LandGuard");
        root.transform.position = new Vector3(tile.x, 0f, tile.y);

        var g = root.AddComponent<LandGuard>();
        g.tile      = tile;
        g.islandKey = islandKey;
        g.BuildFigure();
        return g;
    }

    // Kenney model (stejná postava jako prodavači/bratr — character-male-c),
    // obarvený do vojenské olivové, s dlouhou muškou v ruce. Když model v
    // Resources chybí, postaví se náhradní panáček z primitivů.
    private void BuildFigure()
    {
        var model = CharacterModel.TryBuild(transform, "character-male-c",
            CharacterModel.DEFAULT_SCALE, new Color(0.30f, 0.34f, 0.22f)); // vojenská olivová
        if (model == null) { BuildPrimitiveFigure(); return; }

        AddMusket();
    }

    // Náhrada, když Kenney model chybí — tmavý panáček s muškou (dřívější vzhled).
    private void BuildPrimitiveFigure()
    {
        Material coat = MakeMat(new Color(0.22f, 0.20f, 0.22f));
        Material skin = MakeMat(new Color(0.72f, 0.58f, 0.47f));

        AddPart(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.55f, 0f), new Vector3(0.42f, 0.42f, 0.42f), coat);
        AddPart(PrimitiveType.Sphere,  "Head", new Vector3(0f, 1.00f, 0f), new Vector3(0.36f, 0.34f, 0.36f), skin);
        AddMusket();
    }

    // Dlouhá muška v ruce — stejná pro Kenney model i primitivní náhradu.
    private void AddMusket()
    {
        Material dark = MakeMat(new Color(0.12f, 0.12f, 0.13f));
        var musket = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        musket.name = "Musket";
        var col = musket.GetComponent<Collider>();
        if (col != null) Destroy(col);
        musket.transform.SetParent(transform, false);
        musket.transform.localPosition    = new Vector3(0.16f, 0.85f, 0.20f);
        musket.transform.localScale       = new Vector3(0.05f, 0.55f, 0.05f);
        musket.transform.localEulerAngles = new Vector3(80f, 0f, 0f);
        musket.GetComponent<MeshRenderer>().sharedMaterial = dark;
    }

    void Update()
    {
        // Nejbližší hráč PĚŠKY v dostřelu (strážce je na pevnině, na lodě/plavce nestřílí).
        PlayerController target = null;
        float bestSq = RANGE * RANGE;
        foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (!pc.IsOnFoot) continue;
            float sq = (pc.transform.position - transform.position).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; target = pc; }
        }
        if (target == null) return;

        Vector3 to = target.transform.position - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), 4f * Time.deltaTime);

        if (Time.time >= nextShot)
        {
            nextShot = Time.time + RELOAD;
            Vector3 from = transform.position + Vector3.up * 0.75f + to.normalized * 0.4f;
            CannonBall.Fire(from, to, DAMAGE, CannonBall.Side.Enemy);
            SoundManager.PlayCannon();
        }
    }

    /// <summary>Zásah od hráče (E vedle strážce — viz MegaIslandMarker.TryInteract).</summary>
    public void TakeHit(float dmg)
    {
        hp -= dmg;
        if (hp <= 0f)
        {
            SoundManager.PlaySink();
            if (MegaIslandMarker.Instance != null) MegaIslandMarker.Instance.OnGuardDestroyed(this);
            Destroy(gameObject);
        }
    }

    /// <summary>Stojí strážce na tomhle políčku? (pro hák z PlayerController přes marker)</summary>
    public bool IsAt(int x, int y) => tile.x == x && tile.y == y;

    private void AddPart(PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col); // strážce nemá nic blokovat (hit test je na vzdálenost, ne kolize)

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
}
