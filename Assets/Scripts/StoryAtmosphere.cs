using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  StoryAtmosphere.cs
//  Nálada příběhových míst (fáze B) — barva slunce, mlha, okolní světlo, obloha
//  a hudba se plynule mění podle toho, kde hráč je:
//    • Pirátský ostrov      → teplý západ slunce
//    • Hřbitov lodí         → studená zelenošedá mlha (stejně i když hráče honí Bludný Holanďan)
//    • Poslední ostrov      → bouře: tma, déšť (částice + šum) a blesky s hromem
//    • jinde                → normální den (výchozí hodnoty ze scény)
//  K tomu hudba: bojová při souboji s příběhovým bossem, jinak skladba ostrova,
//  na volném moři ticho (jen hukot vln).
//
//  Jak to funguje: při startu si zapamatuje výchozí "vzhled" scény (Look). Každý
//  snímek spočítá, jaká nálada má platit a jak silně (0–1 podle vzdálenosti od
//  ostrova), a výsledný vzhled = výchozí smíchaný s cílovým. Mění se jen globální
//  RenderSettings a hlavní světlo — nic se neukládá, po návratu z majáku (nová
//  scéna) se stav spočítá znovu. Vytváří ho GridManager.Awake (Ensure).
// ─────────────────────────────────────────────────────────────────────────────

public class StoryAtmosphere : MonoBehaviour
{
    // Nálady (index do pole LOOKS níž).
    private const int MOOD_NORMAL = 0, MOOD_PIRATE = 1, MOOD_GRAVEYARD = 2, MOOD_STORM = 3;

    private const float FULL_RANGE  = 40f;  // do téhle vzdálenosti od středu ostrova platí nálada naplno
    private const float FADE_RANGE  = 85f;  // od téhle vzdálenosti už vůbec
    private const float BLEND_SPEED = 0.35f; // jak rychle se nálada prolíná (podíl za sekundu)

    // Jeden "vzhled" scény — všechno, co nálada mění.
    private struct Look
    {
        public Color sun;        public float sunIntensity;
        public Color fog;        public float fogStart, fogEnd;
        public Color ambSky, ambEquator, ambGround;
        public Color skyTint;    public float skyExposure;
    }

    private Look baseLook;          // výchozí vzhled scény (zapamatovaný při startu)
    private Look[] looks;           // cílové vzhledy nálad (MOOD_…); [0] = baseLook
    private Light sunLight;
    private Material skyMat;
    private int   mood = MOOD_NORMAL;   // nálada, která se právě prolíná
    private float blend;                // 0..1, jak silně právě platí

    // Bouře: déšť a blesky.
    private ParticleSystem[] rain = new ParticleSystem[2]; // jeden déšť nad každým hráčem
    private float nextLightning = 8f;
    private float flashTimer;           // > 0 = právě svítí blesk
    private float thunderAt = -1f;      // kdy zahřmí (zvuk jde se zpožděním za zábleskem)

    /// <summary>Vytvoří objekt nálady, pokud ve scéně ještě není (volá GridManager.Awake).</summary>
    public static void Ensure()
    {
        if (FindFirstObjectByType<StoryAtmosphere>() != null) return;
        new GameObject("StoryAtmosphere").AddComponent<StoryAtmosphere>();
    }

    void Start()
    {
        // Hlavní (směrové) světlo — RenderSettings.sun bývá prázdné, tak ho najdi.
        sunLight = RenderSettings.sun;
        if (sunLight == null)
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sunLight = l; break; }

        skyMat = RenderSettings.skybox; // kopie od SkyClouds (Awake GridManageru už proběhl)
        baseLook = CaptureCurrent();
        looks = new[] { baseLook, MakePirateLook(), MakeGraveyardLook(), MakeStormLook() };
    }

    // ── Cílové vzhledy (odvozené z výchozího, ať sedí na jakoukoli scénu) ─────
    private Look CaptureCurrent()
    {
        var k = new Look
        {
            sun          = sunLight != null ? sunLight.color : Color.white,
            sunIntensity = sunLight != null ? sunLight.intensity : 1f,
            fog          = RenderSettings.fogColor,
            fogStart     = RenderSettings.fogStartDistance,
            fogEnd       = RenderSettings.fogEndDistance,
            ambSky       = RenderSettings.ambientSkyColor,
            ambEquator   = RenderSettings.ambientEquatorColor,
            ambGround    = RenderSettings.ambientGroundColor,
            skyTint      = new Color(0.5f, 0.5f, 0.5f),
            skyExposure  = 1.3f,
        };
        if (skyMat != null && skyMat.HasProperty("_SkyTint"))  k.skyTint     = skyMat.GetColor("_SkyTint");
        if (skyMat != null && skyMat.HasProperty("_Exposure")) k.skyExposure = skyMat.GetFloat("_Exposure");
        return k;
    }

    // Pirátský ostrov: teplý západ slunce — oranžové slunce, broskvová mlha.
    private Look MakePirateLook()
    {
        // Barvy se jen PŘIMÍCHÁVAJÍ k výchozím (Lerp), ať moře zůstane modré a jen se oteplí.
        Look k = baseLook;
        k.sun = new Color(1f, 0.74f, 0.52f);          k.sunIntensity = baseLook.sunIntensity * 0.95f;
        k.fog = Color.Lerp(baseLook.fog, new Color(0.85f, 0.55f, 0.42f), 0.55f);
        k.ambSky     = Color.Lerp(baseLook.ambSky,     new Color(0.95f, 0.70f, 0.55f), 0.45f);
        k.ambEquator = Color.Lerp(baseLook.ambEquator, new Color(0.85f, 0.60f, 0.48f), 0.45f);
        k.skyTint = Color.Lerp(baseLook.skyTint, new Color(0.85f, 0.50f, 0.35f), 0.6f);
        k.skyExposure = baseLook.skyExposure * 0.95f;
        return k;
    }

    // Hřbitov lodí: studená zelenošedá mlha, bledé slunce, mlha blíž.
    private Look MakeGraveyardLook()
    {
        Look k = baseLook;
        k.sun = new Color(0.65f, 0.80f, 0.72f);       k.sunIntensity = baseLook.sunIntensity * 0.55f;
        k.fog = new Color(0.40f, 0.52f, 0.46f);
        k.fogStart = 6f;                              k.fogEnd = Mathf.Min(baseLook.fogEnd, 45f);
        k.ambSky = new Color(0.42f, 0.55f, 0.50f);    k.ambEquator = new Color(0.36f, 0.46f, 0.42f);
        k.ambGround = baseLook.ambGround * 0.6f;
        k.skyTint = new Color(0.30f, 0.42f, 0.36f);   k.skyExposure = baseLook.skyExposure * 0.6f;
        return k;
    }

    // Poslední ostrov: bouře — tmavé nebe, šedomodrá mlha, slabé slunce.
    private Look MakeStormLook()
    {
        Look k = baseLook;
        k.sun = new Color(0.62f, 0.66f, 0.76f);       k.sunIntensity = baseLook.sunIntensity * 0.4f;
        k.fog = new Color(0.24f, 0.27f, 0.32f);
        k.fogStart = 10f;                             k.fogEnd = Mathf.Min(baseLook.fogEnd, 55f);
        k.ambSky = new Color(0.30f, 0.33f, 0.40f);    k.ambEquator = new Color(0.26f, 0.28f, 0.33f);
        k.ambGround = baseLook.ambGround * 0.5f;
        k.skyTint = new Color(0.22f, 0.24f, 0.28f);   k.skyExposure = baseLook.skyExposure * 0.45f;
        return k;
    }

    // ── Hlavní smyčka ──────────────────────────────────────────────────────────
    void Update()
    {
        if (looks == null) return;
        GameData d = GameSession.Instance != null ? GameSession.Instance.Data : null;
        if (d == null) return;

        // 1) Jaká nálada má platit a jak silně.
        float strength;
        int target = TargetMood(d, out strength);

        // 2) Prolínání: když se mění nálada, nejdřív stávající doběhne k nule, pak nastoupí nová.
        float step = BLEND_SPEED * Time.deltaTime;
        if (target != mood)
        {
            blend = Mathf.MoveTowards(blend, 0f, step * 2f);
            if (blend <= 0f) mood = target;
        }
        else blend = Mathf.MoveTowards(blend, strength, step);

        Apply(Mix(baseLook, looks[mood], blend));
        UpdateStorm(mood == MOOD_STORM ? blend : 0f);
        UpdateMusic(d);
    }

    // Nálada podle polohy nejbližšího hráče vůči aktuálnímu mega ostrovu.
    private int TargetMood(GameData d, out float strength)
    {
        strength = 1f;
        if (StoryEvents.GhostHunting) return MOOD_GRAVEYARD; // Holanďan si mlhu nese s sebou

        // Po dohrání příběhu bouře u Posledního ostrova odezní — "klid po bouři".
        if (!d.storyIslandActive || d.storyDone) { strength = 0f; return MOOD_NORMAL; }

        float dist = NearestPlayerDistance(new Vector2(d.storyIslandX, d.storyIslandY));
        strength = 1f - Mathf.InverseLerp(FULL_RANGE, FADE_RANGE, dist);
        if (strength <= 0f) return MOOD_NORMAL;

        switch (d.megaIndex)
        {
            case 0:  return MOOD_PIRATE;
            case 1:  return MOOD_GRAVEYARD;
            default: return MOOD_STORM;
        }
    }

    private static float NearestPlayerDistance(Vector2 to)
    {
        float best = float.MaxValue;
        foreach (var pc in PlayerController.All)
        {
            float dist = Vector2.Distance(new Vector2(pc.transform.position.x, pc.transform.position.z), to);
            if (dist < best) best = dist;
        }
        return best;
    }

    // Lineární smíchání dvou vzhledů (t = 0 → a, t = 1 → b).
    private static Look Mix(Look a, Look b, float t)
    {
        return new Look
        {
            sun = Color.Lerp(a.sun, b.sun, t),           sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t),
            fog = Color.Lerp(a.fog, b.fog, t),
            fogStart = Mathf.Lerp(a.fogStart, b.fogStart, t), fogEnd = Mathf.Lerp(a.fogEnd, b.fogEnd, t),
            ambSky = Color.Lerp(a.ambSky, b.ambSky, t),
            ambEquator = Color.Lerp(a.ambEquator, b.ambEquator, t),
            ambGround = Color.Lerp(a.ambGround, b.ambGround, t),
            skyTint = Color.Lerp(a.skyTint, b.skyTint, t), skyExposure = Mathf.Lerp(a.skyExposure, b.skyExposure, t),
        };
    }

    private void Apply(Look k)
    {
        // Blesk: na zlomek vteřiny všechno prozáří studeným bílým světlem.
        float flash = flashTimer > 0f ? 1f : 0f;

        if (sunLight != null)
        {
            sunLight.color     = Color.Lerp(k.sun, new Color(0.85f, 0.9f, 1f), flash);
            sunLight.intensity = k.sunIntensity + flash * 2.2f;
        }
        RenderSettings.fogColor          = Color.Lerp(k.fog, new Color(0.7f, 0.75f, 0.85f), flash * 0.6f);
        RenderSettings.fogStartDistance  = k.fogStart;
        RenderSettings.fogEndDistance    = k.fogEnd;
        RenderSettings.ambientSkyColor     = k.ambSky + Color.white * (flash * 0.5f);
        RenderSettings.ambientEquatorColor = k.ambEquator;
        RenderSettings.ambientGroundColor  = k.ambGround;
        if (skyMat != null)
        {
            if (skyMat.HasProperty("_SkyTint"))  skyMat.SetColor("_SkyTint", k.skyTint);
            if (skyMat.HasProperty("_Exposure")) skyMat.SetFloat("_Exposure", k.skyExposure + flash * 0.8f);
        }
    }

    // ── Bouře: déšť + blesky ───────────────────────────────────────────────────
    private void UpdateStorm(float storm)
    {
        SoundManager.SetRain(storm);

        // Déšť nad každým hráčem (ve split-screenu dva). Vznikne, až je poprvé potřeba.
        int i = 0;
        foreach (var pc in PlayerController.All)
        {
            if (i >= rain.Length) break;
            if (rain[i] == null && storm > 0.05f) rain[i] = BuildRain();
            if (rain[i] != null)
            {
                rain[i].transform.position = pc.transform.position + Vector3.up * 24f; // vysoko nad kamerou
                var em = rain[i].emission;
                em.rateOverTime = 700f * storm;
            }
            i++;
        }

        // Blesky jen v plné bouři, náhodně každých 6–14 s.
        if (flashTimer > 0f) flashTimer -= Time.deltaTime;
        if (thunderAt > 0f && Time.time >= thunderAt) { SoundManager.PlayThunder(0.9f); thunderAt = -1f; }
        if (storm < 0.7f) return;

        nextLightning -= Time.deltaTime;
        if (nextLightning <= 0f)
        {
            nextLightning = Random.Range(6f, 14f);
            flashTimer    = 0.12f;
            thunderAt     = Time.time + Random.Range(0.3f, 1.4f); // hrom chvíli po záblesku
        }
    }

    // Částicový déšť: tenké protáhlé kapky padající z krabice nad hráčem.
    private ParticleSystem BuildRain()
    {
        var go = new GameObject("Rain");
        go.transform.SetParent(transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // nastavení se mění jen u zastaveného systému

        var main = ps.main;
        main.loop            = true;
        main.startLifetime   = 1.7f;
        main.startSpeed      = 0f;
        main.startSize       = 0.025f;
        main.startColor      = new Color(0.75f, 0.82f, 0.95f, 0.4f);
        main.maxParticles    = 3000;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // kapky nejedou s hráčem, padají "ve světě"
        main.gravityModifier = 0f;

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-2f);   // vítr šikmo
        vel.y = new ParticleSystem.MinMaxCurve(-16f);  // rychlý pád
        vel.z = new ParticleSystem.MinMaxCurve(0f);

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale     = new Vector3(48f, 0.5f, 48f);

        var em = ps.emission;
        em.rateOverTime = 0f;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.renderMode   = ParticleSystemRenderMode.Stretch; // kapka protažená ve směru pádu = čárka
        r.velocityScale = 0.035f;
        r.lengthScale   = 1f;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.sharedMaterial = RainMaterial();

        ps.Play();
        return ps;
    }

    // Průhledný neosvětlený materiál kapky (URP/Unlit je v Always Included Shaders — funguje i v buildu).
    private static Material RainMaterial()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");
        var mat = new Material(sh);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.78f, 0.85f, 0.95f, 0.32f));
        if (mat.HasProperty("_Surface"))  mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite"))   mat.SetFloat("_ZWrite", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3100;
        return mat;
    }

    // ── Hudba ──────────────────────────────────────────────────────────────────
    private void UpdateMusic(GameData d)
    {
        int track = SoundManager.MUSIC_NONE;
        if (EndScreen.IsOpen)                                                   track = SoundManager.MUSIC_ENDING;
        else if (CombatDirector.Instance != null && CombatDirector.Instance.StoryBossEngaged) track = SoundManager.MUSIC_BOSS;
        else if (mood == MOOD_PIRATE    && blend > 0.3f)                        track = SoundManager.MUSIC_PIRATE;
        else if (mood == MOOD_GRAVEYARD && blend > 0.3f)                        track = SoundManager.MUSIC_GRAVEYARD;
        else if (mood == MOOD_STORM     && blend > 0.3f)                        track = SoundManager.MUSIC_FINAL;
        SoundManager.SetMusic(track);
    }

    // Úklid: vrať scéně výchozí vzhled a ztiš hudbu i déšť (zničení objektu = konec scény).
    void OnDestroy()
    {
        if (looks == null) return;
        flashTimer = 0f;
        Apply(baseLook);
        if (SoundManager.Instance != null) { SoundManager.SetMusic(SoundManager.MUSIC_NONE); SoundManager.SetRain(0f); }
    }
}
