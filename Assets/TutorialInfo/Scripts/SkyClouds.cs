using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  SkyClouds.cs
//  Obloha nad mořem: zapne proceduální skybox (modré nebe + sluníčko podle
//  směru hlavního světla), zjemní mlhu, aby bylo nebe vidět, a vytvoří pár
//  velkých pomalu plujících mraků nad hráčem.
//
//  Mraky jsou velké průhledné placky s měkkou texturou vygenerovanou z
//  Perlinova šumu (žádný obrázek se needituje). Plují ve větru a když se moc
//  vzdálí od hráče, přeskočí zpět — takže je nad hráčem pořád obloha.
//
//  Objekt vytváří GridManager (viz CreateSeaWorld). Čistě vizuální, na hru
//  nemá vliv.
// ─────────────────────────────────────────────────────────────────────────────

public class SkyClouds : MonoBehaviour
{
    // Mraky jsou rozmístěné do kruhu kolem hráče u obzoru — herní kamera se
    // dívá šikmo dolů, takže oblohu vidíš hlavně jako pruh nad obzorem.
    // Herní kamera ukazuje jen úzký pruh oblohy nad obzorem, takže mraky sedí
    // nízko a daleko — jako roztrhaný pás mraků na obzoru moře.
    private const int   CLOUD_COUNT = 16;
    private const float CLOUD_DIST_MIN = 40f, CLOUD_DIST_MAX = 62f; // vzdálenost od hráče
    private const float CLOUD_Y_MIN = 7f,    CLOUD_Y_MAX = 15f;     // výška nad hladinou
    private const float DRIFT_SPEED  = 1.1f;                        // stupňů za sekundu (obíhání kolem hráče)

    private Transform follow;
    private Transform[] clouds;
    private float[]     cloudAngle;  // aktuální úhel mraku kolem hráče (°)
    private float[]     cloudDist;   // poloměr mraku

    /// <summary>Zavolá GridManager hned po vytvoření objektu.</summary>
    public void Init(Transform player1)
    {
        follow = player1;

        SetupSkyAndFog();

        Material cloudMat = BuildCloudMaterial();
        clouds     = new Transform[CLOUD_COUNT];
        cloudAngle = new float[CLOUD_COUNT];
        cloudDist  = new float[CLOUD_COUNT];
        for (int i = 0; i < CLOUD_COUNT; i++)
        {
            cloudAngle[i] = Random.Range(0f, 360f);
            cloudDist[i]  = Random.Range(CLOUD_DIST_MIN, CLOUD_DIST_MAX);
            clouds[i]     = BuildCloud(i, cloudMat);                     // vytvoří placku
            PlaceCloud(i, Random.Range(CLOUD_Y_MIN, CLOUD_Y_MAX));      // teď už clouds[i] existuje
        }
    }

    // ── Skybox + mlha ───────────────────────────────────────────────────────
    void SetupSkyAndFog()
    {
        // Kamera(y) ať kreslí skybox místo jednolité barvy.
        foreach (var cam in Camera.allCameras)
            if (cam.clearFlags == CameraClearFlags.SolidColor || cam.clearFlags == CameraClearFlags.Color)
                cam.clearFlags = CameraClearFlags.Skybox;
        if (Camera.main != null) Camera.main.clearFlags = CameraClearFlags.Skybox;

        // Proceduální skybox (kopie, ať nesaháme na sdílený asset).
        if (RenderSettings.skybox != null && RenderSettings.skybox.shader != null
            && RenderSettings.skybox.shader.name.Contains("Skybox/Procedural"))
        {
            var sky = new Material(RenderSettings.skybox);
            if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 0.85f);
            if (sky.HasProperty("_Exposure"))            sky.SetFloat("_Exposure", 1.3f);
            if (sky.HasProperty("_SunSize"))             sky.SetFloat("_SunSize", 0.05f);
            if (sky.HasProperty("_SkyTint"))             sky.SetColor("_SkyTint", new Color(0.5f, 0.65f, 0.85f));
            if (sky.HasProperty("_GroundColor"))         sky.SetColor("_GroundColor", new Color(0.42f, 0.55f, 0.62f));
            RenderSettings.skybox = sky;
        }

        // Herní kamera se dívá dost shora — dovol ji naklonit níž k obzoru,
        // ať jde obloha (mraky, slunce) pořádně vidět. (Výchozí náklon zůstává.)
        var orbit = Camera.main != null ? Camera.main.GetComponent<CameraOrbit>() : null;
        if (orbit != null && orbit.minPitch > 8f) orbit.minPitch = 8f;

        // Mlhu nech, ale posuň dál — ať je nad obzorem vidět nebe a mraky.
        // (Konec mlhy je sladěný s GridManager.ACTIVE_GRID_SIZE = 28, aby
        //  "naskakování" vzdálených ostrovů zůstalo schované v oparu jako dřív.)
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 18f;
        RenderSettings.fogEndDistance   = 70f;

        DynamicGI.UpdateEnvironment();
    }

    // ── Mrak: velká placka s měkkou texturou z Perlinova šumu ────────────────
    Material BuildCloudMaterial()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");
        var mat = new Material(sh);

        mat.SetTexture("_BaseMap", MakeCloudTexture());
        mat.SetTexture("_MainTex", mat.GetTexture("_BaseMap"));
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.9f));

        // Přepni materiál do průhledného režimu a vypni ořez rubu.
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
        if (mat.HasProperty("_Blend"))   mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite"))  mat.SetFloat("_ZWrite", 0f);
        if (mat.HasProperty("_Cull"))    mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        return mat;
    }

    // Vygeneruje čtvercovou texturu měkkého mraku (bílá, průhledné okraje).
    Texture2D MakeCloudTexture()
    {
        const int S = 128;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float seed = Random.value * 100f;

        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                // dvě oktávy šumu → chuchvalcovitý tvar
                float n = Mathf.PerlinNoise(seed + x * 0.03f, seed + y * 0.03f) * 0.65f
                        + Mathf.PerlinNoise(seed + x * 0.08f, seed + y * 0.08f) * 0.35f;

                // kruhový úbytek k okrajům, ať placka nemá viditelné hrany
                float dx = (x - S / 2f) / (S / 2f);
                float dy = (y - S / 2f) / (S / 2f);
                float edge = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));

                float a = Mathf.SmoothStep(0f, 1f, (n * edge - 0.28f) * 2.2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
            }

        tex.Apply();
        return tex;
    }

    // Vytvoří jeden mrak (quad + materiál). Umístění řeší PlaceCloud.
    Transform BuildCloud(int i, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Cloud" + i;
        Destroy(go.GetComponent<Collider>());
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.transform.SetParent(transform, false);

        float w = Random.Range(22f, 42f);
        go.transform.localScale = new Vector3(w, Random.Range(6f, 11f), 1f); // hodně na šířku = plochý mrak
        return go.transform;
    }

    // Umístí mrak podle jeho úhlu/vzdálenosti a natočí ho čelem k hráči.
    void PlaceCloud(int i, float y)
    {
        float rad = cloudAngle[i] * Mathf.Deg2Rad;
        Vector3 p = new Vector3(Mathf.Cos(rad) * cloudDist[i], y, Mathf.Sin(rad) * cloudDist[i]);
        clouds[i].localPosition = p;
        clouds[i].localRotation = Quaternion.LookRotation(new Vector3(p.x, 0f, p.z)); // placka čelem od středu
    }

    void LateUpdate()
    {
        // Drž se nad hráčem (jen vodorovně).
        if (follow != null)
            transform.position = new Vector3(follow.position.x, 0f, follow.position.z);

        if (clouds == null) return;

        // Mraky pomalu obíhají kolem hráče (efekt větru), pořád u obzoru.
        for (int i = 0; i < clouds.Length; i++)
        {
            if (clouds[i] == null) continue;
            cloudAngle[i] += DRIFT_SPEED * Time.deltaTime;
            PlaceCloud(i, clouds[i].localPosition.y);
        }
    }
}
