using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  HostileIslandCannon.cs
//  Dělo na nepřátelském ostrově (~40 % ostrovů). Když se hráčova loď dostane
//  na dostřel, dělo se po ní otočí a začne střílet dělové koule. Hráč ho může
//  sestřelit (pár zásahů) — pak ostrov "zkrotne" a dá odměnu.
//
//  Objekt vytváří CombatDirector na pevninové dlaždici u kraje ostrova. Model =
//  Kenney "Pirate Kit" dělo (Assets/Resources/PirateKit/cannon.fbx) obarvené
//  atlas-texturou. Když model chybí, postaví se z primitivů (fallback).
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
    private Transform   aimPart;          // co se otáčí k hráči (hlaveň / celý model)
    private bool        kenney;           // true = otáčíme celý model jen kolem osy Y

    /// <summary>Vytvoří dělo na daném políčku ostrova.</summary>
    public static HostileIslandCannon Spawn(Vector2Int tile, string islandKey)
    {
        var root = new GameObject("HostileCannon");
        root.transform.position = new Vector3(tile.x, 0.05f, tile.y);

        var c = root.AddComponent<HostileIslandCannon>();
        c.tile      = tile;
        c.islandKey = islandKey;

        // Nejdřív zkus Kenney model, jinak primitivní dělo.
        Transform model = TryBuildKenneyCannon(root.transform);
        if (model != null)
        {
            c.aimPart = model;
            c.kenney  = true;
        }
        else
        {
            c.aimPart = BuildPrimitiveCannon(root.transform);
            c.kenney  = false;
        }
        return c;
    }

    // ── Kenney dělo (Pirate Kit) ───────────────────────────────────────────
    private static Texture2D colormap;
    private static bool      colormapTried;

    private static Transform TryBuildKenneyCannon(Transform root)
    {
        var prefab = Resources.Load<GameObject>("PirateKit/cannon");
        if (prefab == null) return null;

        var go = Instantiate(prefab, root, false);
        go.name = "Model";
        go.transform.localPosition    = Vector3.zero;
        go.transform.localEulerAngles = Vector3.zero;
        go.transform.localScale       = Vector3.one * 0.9f;

        if (!colormapTried) { colormapTried = true; colormap = Resources.Load<Texture2D>("PirateKit/colormap"); }

        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(sh) { name = "CannonColormap (runtime)" };
        if (colormap != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", colormap);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", colormap);
        }
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.15f);

        foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            mr.sharedMaterial    = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);
        return go.transform;
    }

    // ── Fallback: dělo z primitivů ─────────────────────────────────────────
    private static Transform BuildPrimitiveCannon(Transform root)
    {
        Material dark = MakeMat(new Color(0.18f, 0.18f, 0.2f));

        var baseGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
        baseGo.name = "Base";
        baseGo.transform.SetParent(root, false);
        baseGo.transform.localPosition = new Vector3(0f, 0.22f, 0f);
        baseGo.transform.localScale    = new Vector3(0.7f, 0.24f, 0.7f);
        StripCollider(baseGo, dark);

        var barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        barrel.name = "Barrel";
        barrel.transform.SetParent(root, false);
        barrel.transform.localPosition    = new Vector3(0f, 0.45f, 0.25f);
        barrel.transform.localScale       = new Vector3(0.22f, 0.42f, 0.22f);
        barrel.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
        StripCollider(barrel, dark);
        return barrel.transform;
    }

    void Update()
    {
        var target = CombatDirector.Instance != null ? CombatDirector.Instance.NearestSailingPlayer(transform.position) : null;
        if (target == null) return;

        Vector3 to = target.transform.position - transform.position;
        to.y = 0f;
        if (to.magnitude > RANGE) return;

        // Otoč dělo k hráči.
        if (aimPart != null && to.sqrMagnitude > 0.01f)
        {
            if (kenney)
            {
                // Kenney model stojí na zemi — otáčej jen kolem svislé osy.
                // Model míří +Z, tak k němu žádný extra offset netřeba.
                Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up);
                aimPart.rotation = Quaternion.Slerp(aimPart.rotation, look, 4f * Time.deltaTime);
            }
            else
            {
                Quaternion look = Quaternion.LookRotation(to.normalized, Vector3.up);
                aimPart.rotation = Quaternion.Slerp(aimPart.rotation, look * Quaternion.Euler(90f, 0f, 0f), 4f * Time.deltaTime);
            }
        }

        if (Time.time >= nextShot)
        {
            nextShot = Time.time + RELOAD;
            Vector3 from = transform.position + Vector3.up * 0.4f + to.normalized * 0.4f;
            CannonBall.Fire(from, to, DAMAGE, CannonBall.Side.Enemy);
            SoundManager.PlayCannon();
        }
    }

    /// <summary>Zásah hráčovou dělovou koulí.</summary>
    public void TakeHit(float dmg)
    {
        hp -= dmg;
        if (hp <= 0f)
        {
            SoundManager.PlaySink();
            if (CombatDirector.Instance != null) CombatDirector.Instance.OnIslandCannonDestroyed(islandKey, this);
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
