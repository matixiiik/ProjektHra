using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  SeaMonster.cs
//  Mořská obluda (megalodon) na trase od Pirátského ostrova k dalšímu — jednorázová
//  příhoda na moři, viz .claude/story-plan.md §4. Chová se jako "loď pod hladinou":
//    Approach   — pluje k hráči (nad vodou jen hřbetní ploutev).
//    Circle     — kolem hráče krouží (5× rychleji než jeho loď) a hledá příležitost.
//    Telegraph  — na chvíli se "nadechne" (ploutev zčervená) — poslední šance uhnout.
//    Lunge      — rychlý výpad rovným směrem; zásah = velké poškození.
//    Recover    — krátká pauza, pak zase Circle (po pár výpadech Vulnerable).
//    Vulnerable — vyčerpaně se částečně vynoří a pomalu pluje — teď do ní jde
//                 střílet (boss health bar, jako pirát).
//    Sinking    — po vyčerpání HP klesne pod hladinu a zmizí, odměna.
//
//  Útočí na hráče ve VODĚ (v lodi, s rozbitou lodí i pěšky plavajícího). Když je hráč
//  na souši / na molu, obluda nečinně čeká pod hladinou; jakmile znovu vleze do lodi
//  nebo do vody, útok pokračuje. Před souší se obluda sama otáčí (pod ostrov nezajede).
//  Existuje vždy nejvýš jedna (SeaMonster.Instance) — vytváří ji StoryEvents.
// ─────────────────────────────────────────────────────────────────────────────

public class SeaMonster : MonoBehaviour
{
    private enum State { Approach, Circle, Telegraph, Lunge, Recover, Vulnerable, Sinking }

    // Rychlosti se odvozují od RYCHLOSTI LODĚ cílového hráče (viz PlayerController.CurrentBoatSpeed).
    private const float CRUISE_FACTOR      = 5f;    // běžné plavání = 5× rychlejší než loď
    private const float LUNGE_FACTOR       = 8f;    // výpad je ještě rychlejší
    private const float VULNERABLE_FACTOR  = 1.1f;  // vyčerpaná se sotva vleče
    private const float MIN_SPEED          = 12f;   // aspoň tolik j/s, i kdyby hráč stál
    private const float CIRCLE_RADIUS      = 11f;   // v jaké vzdálenosti kolem hráče krouží
    private const float CIRCLE_TIME        = 5f;    // jak dlouho krouží před výpadem
    private const float TELEGRAPH_TIME     = 1.3f;  // "nadechnutí" — hráč má čas uhnout
    private const float LUNGE_MAX_TIME     = 0.9f;
    private const float LUNGE_DAMAGE       = 20f;
    private const float HIT_RADIUS         = 2.2f;  // obluda je velká
    private const int   LUNGES_PER_CYCLE   = 2;     // pár výpadů, pak vyčerpání
    private const float RECOVER_TIME       = 0.8f;
    private const float VULNERABLE_TIME    = 12f;   // jak dlouho je zranitelná
    private const float SINK_TIME          = 1.4f;
    private const float MAX_HP             = 18f;

    // Model má pivot u břicha, ne uprostřed: hřbet/ploutev je nad pivotem, břicho pod ním
    // (změřeno pro MODEL_SCALE 0,4: ploutev ~1,61 j nad pivotem). Hladina ≈ -0,22.
    private const float WATER_Y       = -0.22f;
    private const float MODEL_SCALE   = 0.55f;                         // o něco větší než dřív (0,4)
    private const float FIN_TOP       = 1.61f * MODEL_SCALE / 0.4f;    // výška špičky ploutve nad pivotem
    private const float FIN_VISIBLE   = 0.75f;                         // kolik ploutve kouká z vody
    private const float DEEP_Y        = WATER_Y + FIN_VISIBLE - FIN_TOP; // jen ploutev nad hladinou
    // +1,1 bylo pro skutečný model (vyšší než odhad FIN_TOP) příliš — obluda vyskočila
    // skoro celá z vody (ověřeno v Play, MCP zásah bounds: výška modelu ~3,8 j).
    // +0,4 nechá nad hladinou jen hřbet a ploutev, břicho i ocas zůstanou pod vodou.
    private const float SURFACE_Y     = DEEP_Y + 0.4f;                 // Vulnerable: vynoří se i hřbet

    public static SeaMonster Instance { get; private set; }
    public bool  Engaged { get; private set; } // true, dokud má cíl — kreslí boss bar

    private State  state = State.Approach;
    private float  stateTimer;
    private float  hp = MAX_HP;
    private int    lungesThisCycle;
    private Vector3 lungeDir;
    private bool    hitSomeoneThisLunge;
    private float   circleDir = 1f;   // směr kroužení (+1 / -1)
    private float   wobbleClock;      // čas pro vlnění těla (animace plavání)

    private GridManager gridManager;
    private Transform   modelTf;       // model žraloka (vlní se, ať to vypadá jako plavání)
    private readonly List<Material> glowMats = new List<Material>(); // materiály, co se v Telegraphu zbarví do červena

    /// <summary>Vytvoří obludu na dané pozici (hladina).</summary>
    public static SeaMonster Spawn(Vector3 pos)
    {
        var root = new GameObject("SeaMonster");
        root.transform.position = new Vector3(pos.x, DEEP_Y, pos.z);

        var m = root.AddComponent<SeaMonster>();
        m.gridManager = FindFirstObjectByType<GridManager>();
        m.circleDir = Random.value < 0.5f ? -1f : 1f;
        m.BuildFigure();
        Instance = m;
        m.Engaged = true; // boss bar naskočí hned po vynoření
        if (CombatDirector.Instance != null) CombatDirector.Instance.RegisterMonster(m);
        if (CombatDirector.Instance != null) CombatDirector.Instance.Toast(Loc.T("Něco velkého pluje pod hladinou…", "Something huge is swimming beneath the surface…"));
        return m;
    }

    void OnDestroy() { if (Instance == this) Instance = null; }

    // Model žraloka (Quaternius, CC0 — Assets/Resources/SeaMonster/shark.fbx).
    // Když chybí, postaví se náhradní trup+ploutev z primitivů (fallback).
    private void BuildFigure()
    {
        modelTf = TryBuildSharkModel();
        if (modelTf == null) BuildPrimitiveFallback();
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
        body.transform.localScale       = new Vector3(0.9f, 1.9f, 0.9f) * (MODEL_SCALE / 0.4f);
        body.transform.localEulerAngles = new Vector3(0f, 0f, 90f); // kapsle na bok = protáhlý trup
        body.GetComponent<MeshRenderer>().sharedMaterial = dark;

        var fin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fin.name = "Fin";
        var fc = fin.GetComponent<Collider>();
        if (fc != null) Destroy(fc);
        fin.transform.SetParent(transform, false);
        fin.transform.localPosition    = new Vector3(0f, 0.85f, 0f) * (MODEL_SCALE / 0.4f);
        fin.transform.localScale       = new Vector3(0.05f, 0.5f, 0.22f) * (MODEL_SCALE / 0.4f); // plochý klín = ploutev
        Material finMat = MakeMat(new Color(0.12f, 0.16f, 0.18f));
        fin.GetComponent<MeshRenderer>().sharedMaterial = finMat;
        if (finMat.HasProperty("_EmissionColor")) { finMat.EnableKeyword("_EMISSION"); glowMats.Add(finMat); }
        modelTf = fin.transform.parent; // vlnit se bude celý objekt
    }

    // ── Hlavní smyčka ────────────────────────────────────────────────────────
    void Update()
    {
        PlayerController target = Target();
        if (state != State.Sinking) Engaged = target != null;

        switch (state)
        {
            case State.Approach:   UpdateApproach(target);   break;
            case State.Circle:     UpdateCircle(target);     break;
            case State.Telegraph:  UpdateTelegraph(target);  break;
            case State.Lunge:      UpdateLunge();            break;
            case State.Recover:    UpdateRecover();          break;
            case State.Vulnerable: UpdateVulnerable(target); break;
            case State.Sinking:    UpdateSinking();          break;
        }

        // Hloubka podle stavu (mimo Sinking, ten si ji řídí sám): jen ploutev, ve Vulnerable i hřbet.
        if (state != State.Sinking)
        {
            float targetY = state == State.Vulnerable ? SURFACE_Y : DEEP_Y;
            transform.position = new Vector3(transform.position.x,
                Mathf.Lerp(transform.position.y, targetY, 3f * Time.deltaTime), transform.position.z);
        }

        AnimateBody();
    }

    // Jednoduchá "animace plavání": tělo se vlní (kmitání kolem svislé osy) a jemně se houpe.
    // Model je statický, takže vlnění dělá kód — čím rychleji plave, tím rychleji vlní.
    private void AnimateBody()
    {
        if (modelTf == null) return;
        float speedFactor = (state == State.Lunge) ? 2.4f : (state == State.Vulnerable ? 0.7f : 1.4f);
        wobbleClock += Time.deltaTime * speedFactor;
        modelTf.localRotation = Quaternion.Euler(0f, Mathf.Sin(wobbleClock * 5f) * 9f, Mathf.Sin(wobbleClock * 2.3f) * 4f);
    }

    // Cíl = nejbližší hráč, který je VE VODĚ. Hráč na souši / na molu je v bezpečí (null).
    private PlayerController Target()
    {
        PlayerController best = null;
        float bestSq = float.MaxValue;
        foreach (var pc in PlayerController.All)
        {
            if (!pc.IsInWater) continue;
            float sq = (pc.transform.position - transform.position).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = pc; }
        }
        return best;
    }

    // Cestovní rychlost = násobek rychlosti lodě cíle (aspoň MIN_SPEED).
    private float Speed(PlayerController target, float factor)
        => Mathf.Max(MIN_SPEED, (target != null ? target.CurrentBoatSpeed : 5f) * factor);

    // ── Stavy ────────────────────────────────────────────────────────────────
    private void UpdateApproach(PlayerController target)
    {
        if (target == null) return; // hráč je na souši → čekáme

        Vector3 to = target.transform.position - transform.position; to.y = 0f;
        if (to.magnitude <= CIRCLE_RADIUS * 1.5f) { EnterCircle(); return; }

        MoveToward(to.normalized, Speed(target, CRUISE_FACTOR), 2.5f);
    }

    private void EnterCircle()
    {
        state = State.Circle;
        stateTimer = 0f;
    }

    // Krouží kolem hráče v poloměru CIRCLE_RADIUS (tečna + korekce dovnitř/ven).
    private void UpdateCircle(PlayerController target)
    {
        if (target == null) return;
        stateTimer += Time.deltaTime;

        MoveToward(CircleDirection(target), Speed(target, CRUISE_FACTOR), 4f);

        if (stateTimer >= CIRCLE_TIME) EnterTelegraph();
    }

    // Směr pohybu při kroužení: po tečně kružnice kolem cíle + přitahování zpět na poloměr.
    private Vector3 CircleDirection(PlayerController target)
    {
        Vector3 fromTarget = transform.position - target.transform.position; fromTarget.y = 0f;
        float dist = fromTarget.magnitude;
        Vector3 radial = dist > 0.01f ? fromTarget / dist : Vector3.forward;
        Vector3 tangent = new Vector3(-radial.z, 0f, radial.x) * circleDir;
        float pull = Mathf.Clamp((dist - CIRCLE_RADIUS) / CIRCLE_RADIUS, -1f, 1f); // >0 = moc daleko → dovnitř
        return (tangent - radial * pull).normalized;
    }

    private void EnterTelegraph()
    {
        state = State.Telegraph;
        stateTimer = 0f;
        SoundManager.PlaySplash();
    }

    private void UpdateTelegraph(PlayerController target)
    {
        stateTimer += Time.deltaTime;

        // Tělo postupně "rozzáří" do červena — vizuální "tohle přijde" varování.
        float t = Mathf.Clamp01(stateTimer / TELEGRAPH_TIME);
        SetGlow(Color.Lerp(Color.black, new Color(0.9f, 0.15f, 0.1f), t) * 2f);

        // Míří na hráče a zpomalí (poslední moment na uhnutí).
        if (target != null)
        {
            Vector3 to = target.transform.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), 3f * Time.deltaTime);
        }

        if (stateTimer >= TELEGRAPH_TIME) EnterLunge(target);
    }

    private void EnterLunge(PlayerController target)
    {
        // Směr se "zamkne" přesně teď — během výpadu se už nemění (uhýbatelné).
        Vector3 to = target != null ? target.transform.position - transform.position : transform.forward;
        to.y = 0f;
        lungeDir = to.sqrMagnitude > 0.01f ? to.normalized : transform.forward;
        lungeSpeed = Speed(target, LUNGE_FACTOR);

        state = State.Lunge;
        stateTimer = 0f;
        hitSomeoneThisLunge = false;
        SetGlow(Color.black);
    }

    private float lungeSpeed;

    private void UpdateLunge()
    {
        stateTimer += Time.deltaTime;

        // Výpad se zastaví u pevniny (pod ostrov obluda nezajede).
        Vector3 next = transform.position + lungeDir * lungeSpeed * Time.deltaTime;
        if (IsLandAt(next)) { EnterRecoverOrVulnerable(); return; }
        transform.position = next;
        transform.rotation = Quaternion.LookRotation(lungeDir, Vector3.up);

        if (!hitSomeoneThisLunge)
        {
            foreach (var pc in PlayerController.All)
            {
                if (!pc.IsInWater) continue;
                // Jen vodorovná vzdálenost (výška obluda × hráč se neřeší — obluda je pod hladinou).
                Vector3 d = pc.transform.position - transform.position; d.y = 0f;
                if (d.sqrMagnitude > HIT_RADIUS * HIT_RADIUS) continue;

                // Loď (nebo rozbitá loď — DamageBoat sám pošle zásah do plavce); pěšky plavající jde přímo do panáčka.
                if (pc.IsFootSwimming) pc.DamagePlayer(Mathf.RoundToInt(LUNGE_DAMAGE));
                else                   pc.DamageBoat(Mathf.RoundToInt(LUNGE_DAMAGE));
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
        if (stateTimer >= RECOVER_TIME) EnterCircle();
    }

    // Vyčerpaná, částečně vynořená a pomalá — teď do ní jde střílet (viz CannonBall.CheckEnemyHits).
    private void UpdateVulnerable(PlayerController target)
    {
        stateTimer += Time.deltaTime;

        if (target != null)
            MoveToward(CircleDirection(target), Speed(target, VULNERABLE_FACTOR), 2f);

        if (stateTimer >= VULNERABLE_TIME) { SetGlow(Color.black); EnterCircle(); }
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

    // ── Pohyb a pevnina ──────────────────────────────────────────────────────
    private static readonly float[] STEER_ANGLES = { 0f, 45f, -45f, 90f, -90f }; // pořadí, ve kterém se zkouší stočit před břehem

    // Popluje ve směru `dir` rychlostí `speed`, plynule se natáčí (`turn`). Před pevninou
    // směr stočí (zkouší ±45° a ±90°); když nejde nikam, zůstane stát.
    private void MoveToward(Vector3 dir, float speed, float turn)
    {
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        float step = speed * Time.deltaTime;
        Vector3 chosen = Vector3.zero;
        foreach (float a in STEER_ANGLES)
        {
            Vector3 cand = Quaternion.Euler(0f, a, 0f) * dir;
            // Kouká se o kus dopředu (ne jen na další krok), ať se otočí včas před břehem.
            if (!IsLandAt(transform.position + cand * (step + 2.5f))) { chosen = cand; break; }
        }
        if (chosen == Vector3.zero) return; // zabalená mezi břehy — stůj

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(chosen, Vector3.up), turn * Time.deltaTime);
        transform.position += chosen * step;
    }

    // Je na dané pozici pevnina (nebo stavba)? Voda, ryby, vraky i nevygenerované políčko = plavat lze.
    private bool IsLandAt(Vector3 pos)
    {
        if (gridManager == null) return false;
        TileType t = gridManager.GetTileType(Mathf.RoundToInt(pos.x), Mathf.RoundToInt(pos.z));
        return !(t == TileType.Empty || t == TileType.Water || t == TileType.Water_Fish || t == TileType.Treasure);
    }

    // Nastaví emisní "záři" všech těles obludy (varovná červená v Telegraphu, jinak černá = vypnuto).
    private void SetGlow(Color c)
    {
        foreach (var mat in glowMats)
            if (mat != null && mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", c);
    }

    // Jen vodorovná vzdálenost — viz HostileIslandCannon.IsNear (souboj je záměrně "2D nad mořem").
    public bool IsNear(Vector3 pos, float radius)
    {
        float dx = transform.position.x - pos.x, dz = transform.position.z - pos.z;
        return dx * dx + dz * dz <= radius * radius;
    }

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
