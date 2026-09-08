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
    private const float SHIP_Y       = -0.35f;

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

        float s = size == 0 ? 0.8f : size == 1 ? 1.05f : 1.35f;
        Material hull = MakeMat(new Color(0.28f, 0.2f, 0.15f));
        Material sail = MakeMat(new Color(0.15f, 0.15f, 0.17f));

        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Hull";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale    = new Vector3(0.7f * s, 0.4f * s, 1.5f * s);
        body.transform.localPosition = new Vector3(0f, 0.1f, 0f);
        Strip(body, hull);

        var mast = GameObject.CreatePrimitive(PrimitiveType.Cube);
        mast.name = "Sail";
        mast.transform.SetParent(root.transform, false);
        mast.transform.localScale    = new Vector3(0.08f * s, 1.1f * s, 0.06f * s);
        mast.transform.localPosition = new Vector3(0f, 0.7f * s, 0f);
        Strip(mast, hull);

        var cloth = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cloth.name = "Cloth";
        cloth.transform.SetParent(root.transform, false);
        cloth.transform.localScale    = new Vector3(0.7f * s, 0.75f * s, 0.05f * s);
        cloth.transform.localPosition = new Vector3(0f, 0.8f * s, -0.1f * s);
        Strip(cloth, sail);

        var ps = root.AddComponent<PirateShip>();
        ps.size  = size;
        ps.maxHp = size == 0 ? 2.5f : size == 1 ? 5f : 9f;
        ps.hp    = ps.maxHp;
        return ps;
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
}
