using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  PirateShip.cs
//  Pirátská loď na moři. Objevuje se v otevřené vodě kolem hráče. Dokud je hráč
//  daleko, jen se plaví náhodně. Když se hráč přiblíží, pirát ho začne
//  pronásledovat, střílet a snažit se do něj narazit — a nad hlavou hráče se
//  objeví boss health bar piráta (kreslí CombatDirector).
//
//  Velikost 0/1/2 (malá / střední / velká) = víc zdraví, víc poškození, víc
//  mincí za potopení. Potopit ho jde střelbou z děla nebo nárazem lodí. Když
//  hráč dost dlouho uteče, pirát to vzdá a zmizí.
//
//  Model = jednoduchý tmavý trup + plachta z primitivů (žádný externí prefab).
// ─────────────────────────────────────────────────────────────────────────────

public class PirateShip : MonoBehaviour
{
    private const float AGGRO_RANGE  = 11f;  // odtud začne pronásledovat + boss bar
    private const float GIVEUP_RANGE = 20f;  // za tímhle to po chvíli vzdá
    private const float GIVEUP_TIME  = 12f;
    private const float WANDER_SPEED = 1.7f;
    private const float CHASE_SPEED  = 3.3f;
    private const float KEEP_DIST    = 3.2f;  // nechce hráči vlézt úplně na kobylku (aby stíhal střílet)
    private const float RAM_RANGE    = 1.4f;
    private const float RELOAD       = 2.9f;
    private const float SHIP_Y       = -0.42f; // stejné "potopení" jako hráčova loď (ShipModelSwitcher)

    public  int    size;               // 0 = malá, 1 = střední, 2 = velká
    private float  hp, maxHp;
    private float  nextShot;
    private float  nextRam;
    private float  outOfRangeTimer;
    private Vector3 wanderDir;
    private float  nextWander;
    private bool   everEngaged;         // jakmile jednou zaútočil, drží se hráče (nezmizí do prázdna)
    private Vector3 lastPlayerPos;

    // Editor občas "tikne" po velkých krocích (throttling mimo fokus) — omezíme
    // krok pohybu, ať loď neproletí půl mapy naráz.
    private static float Dt => Mathf.Min(Time.deltaTime, 0.05f);

    public float HpFraction => maxHp > 0f ? Mathf.Clamp01(hp / maxHp) : 0f;
    public bool  Engaged { get; private set; }

    /// <summary>Vytvoří pirátskou loď dané velikosti na dané pozici.</summary>
    public static PirateShip Spawn(Vector3 pos, int size)
    {
        var root = new GameObject("PirateShip");
        root.transform.position = new Vector3(pos.x, SHIP_Y, pos.z);

        // Nejdřív zkus Kenney model z Resources; když není, postav ho z kvádrů.
        if (!BuildKenneyModel(root.transform, size))
            BuildPrimitiveModel(root.transform, size);

        var ps = root.AddComponent<PirateShip>();
        ps.size  = size;
        ps.maxHp = size == 0 ? 2.5f : size == 1 ? 5f : 9f;
        ps.hp    = ps.maxHp;
        return ps;
    }

    // Kenney loď: Assets/TutorialInfo/Resources/PirateShips/ship-pirate-{small,medium,large}.fbx
    private static bool BuildKenneyModel(Transform root, int size)
    {
        string name = size == 0 ? "ship-pirate-small" : size == 1 ? "ship-pirate-medium" : "ship-pirate-large";
        var prefab = Resources.Load<GameObject>("PirateShips/" + name);
        if (prefab == null) return false;

        var go = Instantiate(prefab, root, false);
        go.name = "Model";
        // Zhruba stejná velikost jako hráčovy lodě. Pirátský fbx je ale širší
        // (galéona), tak dáme o kus menší měřítko, ať výsledná loď sedí velikostně
        // vedle hráčovy (~0.15/0.17/0.20). Model posadíme kousek pod kýl (fbx má
        // pivot na dně trupu), ať část trupu mizí pod hladinou a loď působí, že pluje.
        float sc = size == 0 ? 0.12f : size == 1 ? 0.14f : 0.16f;
        go.transform.localPosition = new Vector3(0f, -0.04f, 0f);
        go.transform.localScale    = new Vector3(sc, sc, sc);

        // Kenney fbx nemá materiál. Rozlišíme trup / plachty / vlajky, ať loď
        // není jen tmavá šmouha (plachty a vlajky by jinak splynuly s trupem).
        Material hull = SharedMat(ref matHull, new Color(0.24f, 0.17f, 0.13f)); // tmavé dřevo
        Material sail = SharedMat(ref matSail, new Color(0.86f, 0.81f, 0.70f)); // plátno
        Material flag = SharedMat(ref matFlag, new Color(0.55f, 0.12f, 0.10f)); // rudá vlajka
        foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            string n = mr.gameObject.name.ToLowerInvariant();
            mr.sharedMaterial    = n.Contains("sail") ? sail : n.Contains("flag") ? flag : hull;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);
        return true;
    }

    private static void BuildPrimitiveModel(Transform root, int size)
    {
        float s = size == 0 ? 0.42f : size == 1 ? 0.52f : 0.66f;
        Material hull = MakeMat(new Color(0.28f, 0.2f, 0.15f));
        Material sail = MakeMat(new Color(0.15f, 0.15f, 0.17f));

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Hull";
        body.transform.SetParent(root, false);
        body.transform.localScale    = new Vector3(0.7f * s, 0.4f * s, 1.5f * s);
        body.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        Strip(body, hull);

        var mast = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mast.name = "Mast";
        mast.transform.SetParent(root, false);
        mast.transform.localScale    = new Vector3(0.08f * s, 1.1f * s, 0.06f * s);
        mast.transform.localPosition = new Vector3(0f, 0.7f * s, 0f);
        Strip(mast, hull);

        var cloth = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cloth.name = "Cloth";
        cloth.transform.SetParent(root, false);
        cloth.transform.localScale    = new Vector3(0.7f * s, 0.75f * s, 0.05f * s);
        cloth.transform.localPosition = new Vector3(0f, 0.8f * s, -0.1f * s);
        Strip(cloth, sail);
    }

    void Update()
    {
        var target = CombatDirector.Instance != null
            ? CombatDirector.Instance.NearestSailingPlayer(transform.position) : null;

        float dist = target != null ? Vector3.Distance(transform.position, target.transform.position) : 999f;
        if (target != null) lastPlayerPos = target.transform.position;

        // Jakmile jednou zaútočil, "zavětří" hráče na větší vzdálenost, ať se
        // souboj nerozpadne kvůli tomu, že hráč o kousek popojel.
        float range = everEngaged ? AGGRO_RANGE + 4f : AGGRO_RANGE;
        Engaged = target != null && dist <= range;

        if (Engaged)
        {
            everEngaged     = true;
            outOfRangeTimer = 0f;
            ChaseAndFight(target, dist);
        }
        else
        {
            // Po souboji ještě chvíli pluje za hráčem, pak teprve začne bloumat.
            if (everEngaged && dist < GIVEUP_RANGE) ChasePosition(lastPlayerPos);
            else                                    Wander();

            if (target == null || dist > GIVEUP_RANGE)
            {
                outOfRangeTimer += Time.deltaTime;
                if (outOfRangeTimer >= GIVEUP_TIME) { Destroy(gameObject); return; }
            }
        }

        // Drž se na hladině.
        transform.position = new Vector3(transform.position.x, SHIP_Y, transform.position.z);
    }

    // Jen pluje k danému bodu (po souboji, když hráč odjel).
    void ChasePosition(Vector3 pos)
    {
        Vector3 to = pos - transform.position; to.y = 0f;
        if (to.magnitude < 0.5f) return;
        Vector3 dir = to.normalized;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 2.5f * Dt);
        transform.position += dir * WANDER_SPEED * Dt;
    }

    void ChaseAndFight(PlayerController target, float dist)
    {
        Vector3 to = target.transform.position - transform.position;
        to.y = 0f;
        Vector3 dir = to.normalized;

        // Natoč se k hráči.
        if (dir.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 3f * Dt);

        // Přibliž se, ale ne úplně na doraz (aby stíhal pálit).
        if (dist > KEEP_DIST)
            transform.position += dir * CHASE_SPEED * Dt;

        // Náraz do hráče (jen jednou za ~1.5 s, ne každý snímek).
        if (dist <= RAM_RANGE && Time.time >= nextRam)
        {
            nextRam = Time.time + 1.5f;
            int ram = size == 0 ? 6 : size == 1 ? 10 : 16;
            target.DamageBoat(ram);
            transform.position -= dir * 1.6f; // odraz zpět
            hp -= 0.4f;                        // náraz bolí i piráta
            if (hp <= 0f) { Sink(); return; }
        }

        // Palba z děla.
        if (Time.time >= nextShot && dist <= AGGRO_RANGE)
        {
            nextShot = Time.time + RELOAD;
            float dmg = size == 0 ? 5f : size == 1 ? 8f : 12f;
            CannonBall.Fire(transform.position + Vector3.up * 0.4f, dir, dmg, CannonBall.Side.Enemy);
            SoundManager.PlaySplash();
        }
    }

    void Wander()
    {
        if (Time.time >= nextWander)
        {
            nextWander = Time.time + Random.Range(2.5f, 5f);
            float a = Random.Range(0f, Mathf.PI * 2f);
            wanderDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }
        if (wanderDir.sqrMagnitude > 0.01f)
        {
            transform.position += wanderDir * WANDER_SPEED * Dt;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(wanderDir, Vector3.up), 2f * Dt);
        }
    }

    /// <summary>Zásah hráčovou dělovou koulí.</summary>
    public void TakeHit(float dmg)
    {
        hp -= dmg;
        if (hp <= 0f) Sink();
    }

    void Sink()
    {
        if (CombatDirector.Instance != null) CombatDirector.Instance.OnPirateSunk(this);
        Destroy(gameObject);
    }

    public bool IsNear(Vector3 pos, float radius)
        => (transform.position - pos).sqrMagnitude <= radius * radius;

    // ── pomůcky ────────────────────────────────────────────────────────────
    private static void Strip(GameObject go, Material mat)
    {
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) { mr.sharedMaterial = mat; mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
    }

    private static Material MakeMat(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
        return m;
    }

    // Materiály trupu / plachty / vlajky se vytvoří jen jednou pro celou hru
    // a sdílí je všechny pirátské lodě (žádný materiál navíc na každou loď).
    private static Material matHull, matSail, matFlag;
    private static Material SharedMat(ref Material slot, Color c)
    {
        if (slot == null) slot = MakeMat(c);
        return slot;
    }
}
