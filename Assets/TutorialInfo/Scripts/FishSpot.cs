using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  FishSpot.cs
//  Oživí rybí dlaždici (Water_Fish) — místo pouhého barevného dílku tu pod
//  hladinou plave hejno rybek a po hladině se šíří kruhy (jako když ryba čeří
//  vodu). Čistě vizuální, žádný vliv na hratelnost.
//
//  Sedí na prefabu WaterFishPrefab. Vše si postaví v Start() z primitiv +
//  materiálů generovaných v kódu (žádné externí soubory, jako SoundManager).
// ─────────────────────────────────────────────────────────────────────────────

public class FishSpot : MonoBehaviour
{
    private const int   FISH_COUNT   = 3;
    private const float SURFACE_Y    = -0.24f; // kousek pod hladinou oceánu (−0.22)
    private const float SWIM_RADIUS  = 0.28f;
    private const float SWIM_SPEED   = 40f;    // stupňů za sekundu
    private const int   RIPPLE_COUNT = 2;
    private const float RIPPLE_TIME  = 2.6f;   // jak dlouho trvá jeden kruh

    private Transform     content;   // vyrovnává zploštění prefabu (localScale y = 0.1)
    private Transform[]    fish;
    private float[]        fishPhase;
    private LineRenderer[] ripples;
    private float[]        rippleT;

    private static Material fishMat;

    void Start()
    {
        // Placatý čtverec přeměň na kulatou, sotva znatelnou "tmavší vodu" =
        // poznáš, kde se rybaří, i když jsou ryby zrovna schované, ale netrčí
        // z toho ostrý čtverec.
        var slab = GetComponent<MeshRenderer>();
        var mf   = GetComponent<MeshFilter>();
        if (slab != null && mf != null)
        {
            var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mf.sharedMesh = disc.GetComponent<MeshFilter>().sharedMesh; // kruhový podstavec
            Destroy(disc);

            transform.localScale = new Vector3(0.9f, 0.02f, 0.9f);

            var m = new Material(slab.sharedMaterial);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.12f, 0.36f, 0.42f, 0.55f));
            slab.sharedMaterial    = m;
            slab.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false; // ať kuličky/hráč o dílek nezakopnou

        // Prefab je zploštělý (localScale y ≈ 0.1) — děti by byly taky. Vlož mezi
        // ně objekt, který to zploštění vyruší, ať rybky nejsou placaté.
        content = new GameObject("FishLife").transform;
        content.SetParent(transform, false);
        Vector3 ps = transform.localScale;
        content.localScale = new Vector3(1f / Mathf.Max(ps.x, 1e-3f),
                                         1f / Mathf.Max(ps.y, 1e-3f),
                                         1f / Mathf.Max(ps.z, 1e-3f));

        BuildFish();
        BuildRipples();
    }

    // ── Rybky ────────────────────────────────────────────────────────────
    void BuildFish()
    {
        if (fishMat == null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            fishMat = new Material(sh);
            Color c = new Color(0.62f, 0.72f, 0.82f); // stříbřitě modrá
            if (fishMat.HasProperty("_BaseColor")) fishMat.SetColor("_BaseColor", c);
            if (fishMat.HasProperty("_Color"))     fishMat.SetColor("_Color", c);
        }

        fish      = new Transform[FISH_COUNT];
        fishPhase = new float[FISH_COUNT];

        for (int i = 0; i < FISH_COUNT; i++)
        {
            var f = new GameObject("Fish" + i).transform;
            f.SetParent(content, false);
            fishPhase[i] = i * (360f / FISH_COUNT);

            // Tělo (protáhlá koule).
            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Strip(body);
            body.transform.SetParent(f, false);
            body.transform.localScale = new Vector3(0.16f, 0.09f, 0.10f);

            // Ocas (placatý trojúhelníček ze zploštělé krychle, natočený).
            var tail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Strip(tail);
            tail.transform.SetParent(f, false);
            tail.transform.localPosition    = new Vector3(-0.10f, 0f, 0f);
            tail.transform.localScale       = new Vector3(0.09f, 0.01f, 0.09f);
            tail.transform.localEulerAngles = new Vector3(0f, 45f, 0f);

            fish[i] = f;
        }
    }

    // ── Kruhy na hladině ─────────────────────────────────────────────────
    void BuildRipples()
    {
        ripples = new LineRenderer[RIPPLE_COUNT];
        rippleT = new float[RIPPLE_COUNT];

        // Sprites/Default respektuje průhlednost přes startColor/endColor.
        Shader sh = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");

        for (int i = 0; i < RIPPLE_COUNT; i++)
        {
            var go = new GameObject("Ripple" + i);
            go.transform.SetParent(content, false);
            go.transform.localPosition = new Vector3(0f, SURFACE_Y - transform.position.y + 0.01f, 0f);

            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace   = false;
            lr.loop            = true;
            lr.positionCount   = 28;
            lr.widthMultiplier = 0.03f;
            lr.material        = new Material(sh);
            lr.textureMode     = LineTextureMode.Stretch;
            lr.numCapVertices  = 0;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            ripples[i] = lr;
            rippleT[i] = i * (RIPPLE_TIME / RIPPLE_COUNT);
        }
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // Rybky krouží pod hladinou a lehce se pohupují.
        if (fish != null)
        {
            for (int i = 0; i < fish.Length; i++)
            {
                fishPhase[i] += SWIM_SPEED * dt;
                float a = fishPhase[i] * Mathf.Deg2Rad;
                float y = SURFACE_Y - transform.position.y - 0.08f + Mathf.Sin(Time.time * 2f + i) * 0.02f;
                fish[i].localPosition    = new Vector3(Mathf.Cos(a) * SWIM_RADIUS, y, Mathf.Sin(a) * SWIM_RADIUS);
                // natoč rybku po směru kroužení (tečna)
                fish[i].localRotation = Quaternion.Euler(0f, -fishPhase[i] + 90f, 0f);
            }
        }

        // Kruhy na hladině se rozšiřují a mizí.
        if (ripples != null)
        {
            for (int i = 0; i < ripples.Length; i++)
            {
                rippleT[i] += dt;
                if (rippleT[i] > RIPPLE_TIME) rippleT[i] -= RIPPLE_TIME;

                float k = rippleT[i] / RIPPLE_TIME;      // 0..1
                float radius = Mathf.Lerp(0.05f, 0.42f, k);
                float alpha  = Mathf.Clamp01(1f - k) * 0.55f;

                var lr = ripples[i];
                var rc = new Color(0.85f, 0.95f, 1f, alpha);
                lr.startColor = rc;
                lr.endColor   = rc;

                int n = lr.positionCount;
                for (int p = 0; p < n; p++)
                {
                    float ang = p / (float)n * Mathf.PI * 2f;
                    lr.SetPosition(p, new Vector3(Mathf.Cos(ang) * radius, 0f, Mathf.Sin(ang) * radius));
                }
            }
        }
    }

    private static void Strip(GameObject go)
    {
        var c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sharedMaterial    = fishMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
