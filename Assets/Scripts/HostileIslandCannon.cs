using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  HostileIslandCannon.cs
//  Dělo na nepřátelském ostrově (~20 % ostrovů). Když se hráčova loď dostane
//  na dostřel, dělo se po ní otočí a začne střílet dělové koule. Hráč ho může
//  sestřelit (pár zásahů) — pak ostrov "zkrotne" a dá odměnu.
//
//  Objekt vytváří CombatDirector na pevninové dlaždici u kraje ostrova. Model =
//  jednoduchá tmavá hlaveň + podstavec z primitivů (žádný externí prefab).
// ─────────────────────────────────────────────────────────────────────────────

public class HostileIslandCannon : MonoBehaviour
{
    private const float RANGE   = 9.5f;  // dostřel (v políčkách)
    private const float RELOAD  = 2.6f;  // pauza mezi výstřely
    private const float DAMAGE  = 7f;    // kolik ubere hráčově lodi
    private const float MAX_HP  = 3f;    // kolik poškození dělo vydrží

    public  string     islandKey;        // klíč nepřátelského ostrova (do GameData)
    private Vector2Int  tile;             // políčko, na kterém dělo stojí
    private float       hp = MAX_HP;
    private float       nextShot;
    private Transform   barrel;

    /// <summary>Vytvoří dělo na daném políčku ostrova.</summary>
    public static HostileIslandCannon Spawn(Vector2Int tile, string islandKey)
    {
        var root = new GameObject("HostileCannon");
        root.transform.position = new Vector3(tile.x, 0.15f, tile.y);

        Material dark = MakeMat(new Color(0.18f, 0.18f, 0.2f));

        var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseGo.name = "Base";
        baseGo.transform.SetParent(root.transform, false);
        baseGo.transform.localPosition = new Vector3(0f, 0.12f, 0f);
        baseGo.transform.localScale    = new Vector3(0.7f, 0.24f, 0.7f);
        StripCollider(baseGo, dark);

        var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barrel.name = "Barrel";
        barrel.transform.SetParent(root.transform, false);
        barrel.transform.localPosition   = new Vector3(0f, 0.35f, 0.25f);
        barrel.transform.localScale      = new Vector3(0.22f, 0.42f, 0.22f);
        barrel.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
        StripCollider(barrel, dark);

        var c = root.AddComponent<HostileIslandCannon>();
        c.tile      = tile;
        c.islandKey = islandKey;
        c.barrel    = barrel.transform;
        return c;
    }

    void Update()
    {
        var target = CombatDirector.Instance != null ? CombatDirector.Instance.NearestSailingPlayer(transform.position) : null;
        if (target == null) return;

        Vector3 to = target.transform.position - transform.position;
        to.y = 0f;
        if (to.magnitude > RANGE) return;

        // Otoč hlaveň k hráči.
        if (barrel != null && to.sqrMagnitude > 0.01f)
        {
            Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up);
            barrel.rotation = Quaternion.Slerp(barrel.rotation, look * Quaternion.Euler(90f, 0f, 0f), 4f * Time.deltaTime);
        }

        if (Time.time >= nextShot)
        {
            nextShot = Time.time + RELOAD;
            Vector3 from = transform.position + Vector3.up * 0.35f;
            CannonBall.Fire(from, to, DAMAGE, CannonBall.Side.Enemy);
            SoundManager.PlaySplash();
        }
    }

    /// <summary>Zásah hráčovou dělovou koulí.</summary>
    public void TakeHit(float dmg)
    {
        hp -= dmg;
        if (hp <= 0f)
        {
            if (CombatDirector.Instance != null) CombatDirector.Instance.OnIslandCannonDestroyed(islandKey);
            Destroy(gameObject);
        }
    }

    public bool IsNear(Vector3 pos, float radius)
        => (transform.position - pos).sqrMagnitude <= radius * radius;

    // ── pomůcky ────────────────────────────────────────────────────────────
    private static void StripCollider(GameObject go, Material mat)
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
