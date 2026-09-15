using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  SeaMonster.cs
//  Mořská obluda (megalodon styl) mezi ostrovem 1 a 2 — jednorázová příhoda
//  na moři, viz .claude/story-plan.md §4. Cyklus:
//    Approach   — pluje pod hladinou k hráči (vidět jen ploutev + brázda).
//    Telegraph  — na dostřel se na chvíli "nadechne" (ploutev zčervená),
//                 pak se ROZJEDE rovným směrem (dá se uhnout kormidlem).
//    Lunge      — rychlý výpad rovným směrem; zásah = velké poškození trupu.
//    Vulnerable — po pár výpadech se vyčerpaně vynoří a chvíli stojí — teď
//                 do ní jde střílet (boss health bar, stejně jako pirát).
//    Sinking    — po vyčerpání HP klesne pod hladinu a zmizí, odměna.
//  Existuje vždy nejvýš jedna (SeaMonster.Instance) — vytváří ji StoryEvents.
// ─────────────────────────────────────────────────────────────────────────────

public class SeaMonster : MonoBehaviour
{
    private enum State { Approach, Telegraph, Lunge, Recover, Vulnerable, Sinking }

    // Rychlosti odvozené od hráče pěšky (PlayerController.moveSpeed = 5 j/s
    // = "člověk"). Výpad = 10× člověk, plavání kolem = rychlé, ale ne tak
    // zběsilé, ať je znát rozdíl mezi "plave k tobě" a "teď zaútočí".
    private const float HUMAN_SPEED        = 5f;
    private const float APPROACH_SPEED     = HUMAN_SPEED * 3f;  // 15 j/s
    private const float LUNGE_TRIGGER_RANGE = 9f;   // odtud se spustí telegraph
    private const float TELEGRAPH_TIME     = 1.5f;  // "nadechnutí" — hráč má čas uhnout
    private const float LUNGE_SPEED        = HUMAN_SPEED * 10f; // 50 j/s — samotný výpad
    private const float LUNGE_MAX_TIME     = 0.5f;  // při 50 j/s i tak pokryje ~25 políček
    private const float LUNGE_DAMAGE       = 20f;
    private const float HIT_RADIUS         = 1.3f;
    private const int   LUNGES_PER_CYCLE   = 2;      // pár výpadů, pak vyčerpání
    private const float RECOVER_TIME       = 1.0f;  // pauza mezi výpady v jednom cyklu
    private const float VULNERABLE_TIME    = 9f;    // jak dlouho stojí a dá se do ní střílet
    private const float SINK_TIME          = 1.4f;
    private const float MAX_HP             = 18f;

    // Model má pivot u břicha, ne uprostřed (změřeno v Play módu: hřbet/ploutev
    // je ~1.61 j nad pivotem, břicho ~0.91 j pod ním). Hladina ≈ -0.22.
    private const float DEEP_Y     = -1.76f; // jen špička hřbetní ploutve nad hladinou
    private const float SURFACE_Y  = 0.65f;  // skoro celá nad hladinou, jen břicho ji ještě čechrá (Vulnerable)
    private const float MODEL_SCALE = 0.4f;  // Quaternius Shark.fbx (CC0, Resources/SeaMonster/shark) — surový model je obří

    public static SeaMonster Instance { get; private set; }
    public bool  Engaged { get; private set; } // true od prvního telegraphu do potopení — kreslí boss bar

    private State  state = State.Approach;
    private float  stateTimer;
    private float  hp = MAX_HP;
    private int    lungesThisCycle;
    private Vector3 lungeDir;
    private bool    hitSomeoneThisLunge;

    private readonly List<Material> glowMats = new List<Material>(); // materiály, co se v Telegraphu zbarví do červena

    /// <summary>Vytvoří obludu na dané pozici (hladina).</summary>
    public static SeaMonster Spawn(Vector3 pos)
    {
        var root = new GameObject("SeaMonster");
        root.transform.position = new Vector3(pos.x, DEEP_Y, pos.z);

        var m = root.AddComponent<SeaMonster>();
        m.BuildFigure();
        Instance = m;
        m.Engaged = true; // boss bar naskočí hned po vynoření, ne až při prvním výpadu
        if (CombatDirector.Instance != null) CombatDirector.Instance.RegisterMonster(m);
        if (CombatDirector.Instance != null) CombatDirector.Instance.Toast("Něco velkého pluje pod hladinou...");
        return m;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // Model žraloka (Quaternius, CC0 — Assets/Resources/SeaMonster/shark.fbx).
    // Když chybí, postaví se náhradní trup+ploutev z primitivů (fallback).
    private void BuildFigure()
    {
        if (TryBuildSharkModel() == null) BuildPrimitiveFallback();
    }

    private Transform TryBuildSharkModel()
    {
        var prefab = Resources.Load<GameObject>("SeaMonster/shark");
        if (prefab == null) return null;

        var go = Instantiate(prefab, transform, false);
        go.name = "Model";
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale    = Vector3.one * MODEL_SCALE;

        foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            var mat = mr.material; // instance kopie — ať se emisí nehrabe do sdíleného assetu
            if (mat.HasProperty("_EmissionColor")) { mat.EnableKeyword("_EMISSION"); glowMats.Add(mat); }
        }
        foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);
        return go.transform;
    }

    // Náhrada, když model v Resources chybí — tmavý protáhlý trup + ploutev.
    private void BuildPrimitiveFallback()
    {
        Material dark = MakeMat(new Color(0.12f, 0.16f, 0.18f));

        var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        var bc = body.GetComponent<Collider>();
        if (bc != null) Destroy(bc);
        body.transform.SetParent(transform, false);
        body.transform.localPosition    = Vector3.zero;
        body.transform.localScale       = new Vector3(0.9f, 1.9f, 0.9f);
        body.transform.localEulerAngles = new Vector3(0f, 0f, 90f); // kapsle na bok = protáhlý trup
        body.GetComponent<MeshRenderer>().sharedMaterial = dark;

        var fin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fin.name = "Fin";
        var fc = fin.GetComponent<Collider>();
        if (fc != null) Destroy(fc);
        fin.transform.SetParent(transform, false);
        fin.transform.localPosition    = new Vector3(0f, 0.85f, 0f);
        fin.transform.localScale       = new Vector3(0.05f, 0.5f, 0.22f); // plochý klín = ploutev
        Material finMat = MakeMat(new Color(0.12f, 0.16f, 0.18f));
        fin.GetComponent<MeshRenderer>().sharedMaterial = finMat;
        if (finMat.HasProperty("_EmissionColor")) { finMat.EnableKeyword("_EMISSION"); glowMats.Add(finMat); }
    }

    void Update()
    {
        switch (state)
        {
            case State.Approach:   UpdateApproach();   break;
            case State.Telegraph:  UpdateTelegraph();  break;
            case State.Lunge:      UpdateLunge();       break;
            case State.Recover:    UpdateRecover();     break;
            case State.Vulnerable: UpdateVulnerable();  break;
            case State.Sinking:    UpdateSinking();     break;
        }

        // Hloubka podle stavu (mimo Sinking, ten si ji řídí sám).
        if (state != State.Sinking)
        {
            float targetY = state == State.Vulnerable ? SURFACE_Y : DEEP_Y;
            transform.position = new Vector3(transform.position.x,
                Mathf.Lerp(transform.position.y, targetY, 3f * Time.deltaTime), transform.position.z);
        }
    }

    private PlayerController Target() =>
        CombatDirector.Instance != null ? CombatDirector.Instance.NearestSailingPlayer(transform.position) : null;

    private void UpdateApproach()
    {
        var target = Target();
        if (target == null) return;

        Vector3 to = target.transform.position - transform.position; to.y = 0f;
        float dist = to.magnitude;

        if (dist <= LUNGE_TRIGGER_RANGE) { EnterTelegraph(); return; }

        if (to.sqrMagnitude > 0.01f)
        {
            Vector3 dir = to.normalized;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), 2f * Time.deltaTime);
            transform.position += dir * APPROACH_SPEED * Time.deltaTime;
        }
    }

    private void EnterTelegraph()
    {
        state = State.Telegraph;
        stateTimer = 0f;
        Engaged = true;
        SoundManager.PlaySplash();
    }

    private void UpdateTelegraph()
    {
        stateTimer += Time.deltaTime;

        // Tělo postupně "rozzáří" do červena — vizuální "tohle přijde" varování.
        float t = Mathf.Clamp01(stateTimer / TELEGRAPH_TIME);
        SetGlow(Color.Lerp(Color.black, new Color(0.9f, 0.15f, 0.1f), t) * 2f);

        // Mírně se natáčí k hráči, dokud se úplně nerozjede (poslední moment na uhnutí).
        var target = Target();
        if (target != null)
        {
            Vector3 to = target.transform.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), 2f * Time.deltaTime);
        }

        if (stateTimer >= TELEGRAPH_TIME) EnterLunge();
    }

    private void EnterLunge()
    {
        var target = Target();
        // Směr se "zamkne" přesně teď — během výpadu se už nemění (uhýbatelné).
        Vector3 to = target != null ? target.transform.position - transform.position : transform.forward;
        to.y = 0f;
        lungeDir = to.sqrMagnitude > 0.01f ? to.normalized : transform.forward;

        state = State.Lunge;
        stateTimer = 0f;
        hitSomeoneThisLunge = false;
        SetGlow(Color.black);
    }

    private void UpdateLunge()
    {
        stateTimer += Time.deltaTime;
        transform.position += lungeDir * LUNGE_SPEED * Time.deltaTime;
        transform.rotation  = Quaternion.LookRotation(lungeDir, Vector3.up);

        if (!hitSomeoneThisLunge)
        {
            foreach (var pc in PlayerController.All)
            {
                if (!pc.IsSailing) continue;
                if ((pc.transform.position - transform.position).sqrMagnitude > HIT_RADIUS * HIT_RADIUS) continue;
                pc.DamageBoat(Mathf.RoundToInt(LUNGE_DAMAGE));
                SoundManager.PlayHit();
                hitSomeoneThisLunge = true;
                break;
            }
        }

        if (stateTimer >= LUNGE_MAX_TIME) EnterRecoverOrVulnerable();
    }

    private void EnterRecoverOrVulnerable()
    {
        lungesThisCycle++;
        if (lungesThisCycle >= LUNGES_PER_CYCLE)
        {
            state = State.Vulnerable;
            stateTimer = 0f;
            lungesThisCycle = 0;
            SoundManager.PlaySplash();
        }
        else
        {
            state = State.Recover;
            stateTimer = 0f;
        }
    }

    private void UpdateRecover()
    {
        stateTimer += Time.deltaTime;
        if (stateTimer >= RECOVER_TIME) { state = State.Approach; }
    }

    // Vyčerpaná, skoro na hladině — teď do ní jde střílet (viz CannonBall.CheckEnemyHits).
    private void UpdateVulnerable()
    {
        stateTimer += Time.deltaTime;
        if (stateTimer >= VULNERABLE_TIME) { state = State.Approach; SetGlow(Color.black); }
    }

    /// <summary>Zásah hráčovou dělovou koulí — účinný jen ve stavu Vulnerable
    /// (jinak je obluda pod hladinou, zásah "minul").</summary>
    public void TakeHit(float dmg)
    {
        if (state != State.Vulnerable) return; // pod hladinou zásah "mine"

        hp -= dmg;
        if (hp <= 0f) EnterSinking();
    }

    private void EnterSinking()
    {
        state = State.Sinking;
        stateTimer = 0f;
        Engaged = false;
        SoundManager.PlaySink();

        var grid = FindFirstObjectByType<GridManager>();
        if (grid != null)
        {
            grid.gameData.ambush1Done = true;
            grid.Save();
        }
        if (CombatDirector.Instance != null) CombatDirector.Instance.OnMonsterSunk();
    }

    private void UpdateSinking()
    {
        stateTimer += Time.deltaTime;
        transform.position += Vector3.down * 1.5f * Time.deltaTime;
        if (stateTimer >= SINK_TIME) Destroy(gameObject);
    }

    // Nastaví emisní "záři" všech těles obludy (varovná červená v Telegraphu, jinak černá = vypnuto).
    private void SetGlow(Color c)
    {
        foreach (var mat in glowMats)
            if (mat != null && mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", c);
    }

    public bool IsNear(Vector3 pos, float radius)
        => (transform.position - pos).sqrMagnitude <= radius * radius;

    /// <summary>0–1 podíl zbývajícího zdraví, pro boss health bar.</summary>
    public float HpFraction => Mathf.Clamp01(hp / MAX_HP);

    private static Material MakeMat(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
        return m;
    }
}
