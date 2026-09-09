using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  LighthouseInteriorDecor.cs
//  Zabydlí kulatou místnost v majáku pár "útulnými" detaily: teplejší světlo,
//  krb s mihotavým ohněm, závěsnou lampu, nástěnné lucerny, kobereček a dvě
//  květiny v květináči.
//
//  Skript sedí na objektu "InteriorManagers" ve scéně LighthouseInterior a
//  všechno si staví sám v Start() jako svoje děti (primitiva + barevné
//  materiály) — nic se nezapojuje v inspektoru. Je čistě vizuální, nemá vliv
//  na hratelnost.
//
//  Pozn.: v coopu se celý interiér posouvá o velký offset od oceánu (řeší
//  LighthouseInterior.Awake). Protože stavíme až v Start() jako děti tohoto
//  objektu, posun se propíše sám.
// ─────────────────────────────────────────────────────────────────────────────

public class LighthouseInteriorDecor : MonoBehaviour
{
    // Kulatá místnost má poloměr ~4, mezera (dollhouse pohled) je vepředu (−Z).
    private const float WALL_R = 3.8f;

    // Poloměr, ve kterém se smí hráč pohybovat = zhruba kobereček (ať nevleze
    // do krbu, do pultu ani do zdi). Nastaví se všem InteriorPlayer ve scéně.
    private const float WALK_RADIUS = 2.3f;

    // Barvy trička prodavačů = barvy pultů.
    private static readonly Color UpgradeShirt = new Color(0.28f, 0.45f, 0.68f); // modrá — vylepšení
    private static readonly Color QuestShirt   = new Color(0.86f, 0.52f, 0.16f); // oranžová — questy
    private static readonly Color SellShirt    = new Color(0.30f, 0.62f, 0.30f); // zelená — výkupna

    // Kam se pulty přesunou — tři vedle sebe podél zadní stěny, víc do místnosti
    // než původní kostky (z=2.5). Krb se kvůli tomu přesune k levé stěně.
    private static readonly Vector3 UpgradePos = new Vector3(-2.4f, 0.5f, 2.3f);
    private static readonly Vector3 QuestPos   = new Vector3( 0.1f, 0.5f, 2.9f);
    private static readonly Vector3 SellPos    = new Vector3( 2.6f, 0.5f, 2.3f);

    private Material wood, woodDark, stone, cloth, leaf, ember, metal, skin;

    void Start()
    {
        BuildMaterials();
        WarmUpSceneLights();
        ClampWalkArea();

        BuildHearth(new Vector3(-3.2f, 0f, 0.2f), 90f);   // krb u levé stěny (zadní stěna je pro pulty)
        BuildHangingLamp(new Vector3(0f, 0f, -0.2f));     // lampa nad středem
        BuildRug(new Vector3(0f, 0.02f, -0.6f));          // kobereček u vchodu
        BuildPlant(new Vector3(2.9f, 0f, -1.6f));         // květina vpravo vepředu
        BuildPlant(new Vector3(-2.7f, 0f, -1.9f));        // květina vlevo u vchodu
        BuildWallSconce(125f);                            // lucerna vpravo u vchodu
        BuildWallSconce(-125f);                           // lucerna vlevo u vchodu

        // Pulty: přesuň je víc do místnosti, z kostek udělej dřevěné pulty
        // s prodavačem v tričku barvy obchodu. Přidej nový zelený pult výkupny.
        // Pozor: v coopu je interiér posunutý o velký offset → hýbeme relativně
        // (delta od původní polohy kostky ve scéně), ať offset nezrušíme.
        var up = FindInScene("Counter_Upgrade");
        var qu = FindInScene("Counter_Quest");
        if (up != null) { up.transform.position += UpgradePos - SceneUpgradePos; DressCounter(up, UpgradeShirt); }
        if (qu != null) { qu.transform.position += QuestPos   - SceneQuestPos;   DressCounter(qu, QuestShirt); }

        if (qu != null) BuildSellCounter(qu);
    }

    // Původní polohy kostek pultu ve scéně LighthouseInterior.
    private static readonly Vector3 SceneUpgradePos = new Vector3(-2.6f, 0.5f, 2.5f);
    private static readonly Vector3 SceneQuestPos   = new Vector3( 2.6f, 0.5f, 2.5f);

    // Nový zelený pult VÝKUPNY vedle oranžového (questy). Vlastní InteriorInteractable
    // s akcí QuestShopSell → QuestShopManager.Open(idx, sellMode: true). Polohu bere
    // relativně k oranžovému pultu (kvůli coop offsetu).
    private void BuildSellCounter(GameObject questCounter)
    {
        var go = new GameObject("Counter_Sell");
        // V coopu je aktivní scéna SampleScene → přesuň objekt do scény majáku,
        // jinak by ho InteriorPlayer (hledá jen ve své scéně) neviděl.
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        go.transform.position = questCounter.transform.position + (SellPos - QuestPos);

        var it = go.AddComponent<InteriorInteractable>();
        it.action = InteriorAction.QuestShopSell;
        it.range  = 2.2f;
        it.prompt = "E — vykupna (prodej korist)";

        DressCounter(go, SellShirt);
    }

    // Omez pochozí plochu na kobereček (a případným bodům zájmu přidej dosah,
    // ať na ně hráč z kraje koberce dosáhne).
    private void ClampWalkArea()
    {
        foreach (var ip in FindObjectsByType<InteriorPlayer>(FindObjectsSortMode.None))
            if (ip.gameObject.scene == gameObject.scene) ip.areaRadius = WALK_RADIUS;

        foreach (var it in FindObjectsByType<InteriorInteractable>(FindObjectsSortMode.None))
            if (it.gameObject.scene == gameObject.scene && it.range < 2.2f) it.range = 2.2f;
    }

    // Z objektu pultu (barevná kostka, nebo nový prázdný) udělá dřevěný pult
    // otočený čelem do místnosti + za ním stojícího prodavače v tričku dané barvy.
    private void DressCounter(GameObject counter, Color shirt)
    {
        // Původní kostku (pokud je) schovej — objekt necháme kvůli InteriorInteractable.
        foreach (var mr in counter.GetComponentsInChildren<MeshRenderer>(true))
            mr.enabled = false;

        var root = new GameObject("CounterDressing");
        root.transform.SetParent(counter.transform, false);
        // Kostka pultu má počátek ~0.5 nad podlahou → srovnej na zem.
        root.transform.position = new Vector3(counter.transform.position.x, 0f, counter.transform.position.z);
        // Otoč pult čelem do místnosti: +Z míří od středu ke zdi, −Z (strana pro
        // zákazníka) ke středu. Prodavač je pak za pultem (u zdi).
        Vector3 outward = new Vector3(counter.transform.position.x, 0f, counter.transform.position.z);
        if (outward.sqrMagnitude > 0.01f)
            root.transform.rotation = Quaternion.LookRotation(outward.normalized, Vector3.up);

        // Dřevěný pult (deska + čelo + nožky). Deska trochu přečnívá ke středu.
        Box(root.transform, "Top",   new Vector3(0f, 0.66f, -0.12f), new Vector3(2.0f, 0.14f, 0.95f), wood);
        Box(root.transform, "Front", new Vector3(0f, 0.33f, -0.55f), new Vector3(2.0f, 0.66f, 0.16f), woodDark);
        Box(root.transform, "Side1", new Vector3(-0.92f, 0.33f, -0.1f), new Vector3(0.14f, 0.66f, 0.8f), woodDark);
        Box(root.transform, "Side2", new Vector3(0.92f, 0.33f, -0.1f),  new Vector3(0.14f, 0.66f, 0.8f), woodDark);

        // Prodavač stojí ZA pultem (u zdi → +Z), čelem ke středu (−Z).
        BuildShopkeeper(root.transform, new Vector3(0f, 0f, 0.55f), shirt);
    }

    // Panáček prodavače — stejné díly jako hráč (tělo + hlava + klobouk + nos),
    // tričko v barvě obchodu. Trochu větší, ať kouká přes pult.
    private void BuildShopkeeper(Transform parent, Vector3 pos, Color shirt)
    {
        var g = new GameObject("Shopkeeper");
        g.transform.SetParent(parent, false);
        g.transform.localPosition    = pos;
        g.transform.localEulerAngles = new Vector3(0f, 180f, 0f); // čelem ke středu (−Z rodiče)

        Material shirtMat = Mat(shirt);
        Box(g.transform,    "Legs", new Vector3(0f, 0.30f, 0f),   new Vector3(0.42f, 0.60f, 0.36f), woodDark);
        Cyl(g.transform,    "Body", new Vector3(0f, 0.86f, 0f),   new Vector3(0.56f, 0.42f, 0.56f), Vector3.zero, shirtMat);
        Sphere(g.transform, "Head", new Vector3(0f, 1.34f, 0f),   new Vector3(0.40f, 0.40f, 0.40f), skin);
        Box(g.transform,    "Hat",  new Vector3(0f, 1.56f, 0f),   new Vector3(0.62f, 0.14f, 0.62f), woodDark);
        Box(g.transform,    "Nose", new Vector3(0f, 1.32f, 0.22f), new Vector3(0.09f, 0.09f, 0.14f), skin);
    }

    private GameObject FindInScene(string name)
    {
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
            if (t.name == name && t.gameObject.scene == gameObject.scene) return t.gameObject;
        return null;
    }

    // ── Materiály ─────────────────────────────────────────────────────────
    private void BuildMaterials()
    {
        wood     = Mat(new Color(0.52f, 0.36f, 0.22f));
        woodDark = Mat(new Color(0.32f, 0.22f, 0.14f));
        stone    = Mat(new Color(0.46f, 0.45f, 0.44f));
        cloth    = Mat(new Color(0.60f, 0.22f, 0.20f)); // teplá červená (kobereček)
        leaf     = Mat(new Color(0.30f, 0.52f, 0.26f));
        metal    = Mat(new Color(0.24f, 0.23f, 0.22f));
        skin     = Mat(new Color(0.85f, 0.68f, 0.55f)); // kůže prodavače

        // Žhavý oheň / plamínek — svítí i bez světla (emise).
        ember = Mat(new Color(1f, 0.55f, 0.18f));
        if (ember.HasProperty("_EmissionColor"))
        {
            ember.EnableKeyword("_EMISSION");
            ember.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.12f) * 2.4f);
        }
    }

    private static Material Mat(Color c)
    {
        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var m = new Material(sh);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", c);
        return m;
    }

    // ── Zteplení scénického světla ────────────────────────────────────────
    // Ve scéně je studené modré výplňové světlo — kousek ho ubereme a přidáme
    // teplý přísvit, ať místnost působí víc "u ohně" a míň "v ledničce".
    private void WarmUpSceneLights()
    {
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.gameObject.scene != gameObject.scene) continue;
            if (l.type == LightType.Directional && l.color.b > l.color.r) // ta modrá výplň
            {
                l.color     = Color.Lerp(l.color, new Color(0.85f, 0.85f, 0.9f), 0.6f);
                l.intensity *= 0.6f;
            }
        }

        var warm = NewChild("WarmFill", Vector3.zero);
        var wl = warm.AddComponent<Light>();
        wl.type      = LightType.Directional;
        wl.color     = new Color(1f, 0.80f, 0.58f);
        wl.intensity = 0.55f;
        warm.transform.rotation = Quaternion.Euler(50f, -25f, 0f);
    }

    // ── Krb ───────────────────────────────────────────────────────────────
    private void BuildHearth(Vector3 pos, float yaw)
    {
        var root = NewChild("Hearth", pos);
        root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

        Box(root.transform, "Base",   new Vector3(0f, 0.25f, 0f),    new Vector3(1.8f, 0.5f, 0.55f), stone);
        Box(root.transform, "Left",   new Vector3(-0.8f, 0.85f, 0f), new Vector3(0.35f, 1.2f, 0.55f), stone);
        Box(root.transform, "Right",  new Vector3(0.8f, 0.85f, 0f),  new Vector3(0.35f, 1.2f, 0.55f), stone);
        Box(root.transform, "Hood",   new Vector3(0f, 1.55f, 0f),    new Vector3(1.5f, 0.6f, 0.5f),  stone);
        Box(root.transform, "Mantle", new Vector3(0f, 1.15f, 0.1f),  new Vector3(2.0f, 0.18f, 0.7f), woodDark);
        Box(root.transform, "Fire",   new Vector3(0f, 0.45f, 0.02f), new Vector3(1.0f, 0.45f, 0.3f), ember);

        // Polínka opřená vedle krbu.
        for (int i = 0; i < 3; i++)
            Cyl(root.transform, "Log" + i, new Vector3(1.2f, 0.13f + i * 0.14f, 0.1f),
                new Vector3(0.15f, 0.55f, 0.15f), new Vector3(90f, 0f, 10f), woodDark);

        // Teplé mihotavé světlo z ohně.
        var lightGo = NewChild("FireLight", pos + new Vector3(0.4f, 0.7f, 0f));
        var fl = lightGo.AddComponent<Light>();
        fl.type      = LightType.Point;
        fl.color     = new Color(1f, 0.58f, 0.25f);
        fl.range     = 9f;
        fl.intensity = 3.6f;
        lightGo.AddComponent<Flicker>().Bind(fl, 3.6f);
    }

    // ── Závěsná lampa nad středem ─────────────────────────────────────────
    private void BuildHangingLamp(Vector3 pos)
    {
        var root = NewChild("HangingLamp", pos);
        Box(root.transform, "Chain", new Vector3(0f, 3.4f, 0f), new Vector3(0.04f, 1.4f, 0.04f), metal);
        Box(root.transform, "Cage",  new Vector3(0f, 2.5f, 0f), new Vector3(0.28f, 0.4f, 0.28f), metal);
        var glow = Sphere(root.transform, "Glow", new Vector3(0f, 2.5f, 0f), Vector3.one * 0.22f, ember);
        glow.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var lightGo = NewChild("LampGlow", pos + new Vector3(0f, 2.5f, 0f));
        var pl = lightGo.AddComponent<Light>();
        pl.type      = LightType.Point;
        pl.color     = new Color(1f, 0.86f, 0.62f);
        pl.range     = 8f;
        pl.intensity = 2.4f;
    }

    // ── Kobereček (menší, u vchodu) ──────────────────────────────────────
    private void BuildRug(Vector3 pos)
    {
        var rug = GameObject.CreatePrimitive(PrimitiveType.Quad);
        rug.name = "AccentRug";
        Destroy(rug.GetComponent<Collider>());
        rug.transform.SetParent(transform, false);
        rug.transform.localPosition    = pos;
        rug.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
        rug.transform.localScale       = new Vector3(2.6f, 1.7f, 1f);
        rug.GetComponent<MeshRenderer>().sharedMaterial = cloth;
        rug.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    // ── Květina v květináči ───────────────────────────────────────────────
    private void BuildPlant(Vector3 pos)
    {
        var root = NewChild("Plant", pos);
        Cyl(root.transform, "Pot",  new Vector3(0f, 0.2f, 0f),        new Vector3(0.42f, 0.22f, 0.42f), Vector3.zero, woodDark);
        Sphere(root.transform, "Leaf0", new Vector3(0f, 0.58f, 0f),      new Vector3(0.6f, 0.55f, 0.6f), leaf);
        Sphere(root.transform, "Leaf1", new Vector3(0.14f, 0.8f, 0.08f), new Vector3(0.4f, 0.4f, 0.4f), leaf);
        Sphere(root.transform, "Leaf2", new Vector3(-0.12f, 0.76f, -0.07f), new Vector3(0.36f, 0.36f, 0.36f), leaf);
    }

    // ── Nástěnná lucerna ─────────────────────────────────────────────────
    private void BuildWallSconce(float angleDeg)
    {
        float a = angleDeg * Mathf.Deg2Rad;
        Vector3 onWall = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * (WALL_R - 0.2f);
        onWall.y = 2.3f;

        var root = NewChild("Sconce", onWall);
        Box(root.transform, "Bracket", Vector3.zero, new Vector3(0.12f, 0.12f, 0.3f), metal);
        var flame = Sphere(root.transform, "Flame", new Vector3(0f, 0.16f, 0f), Vector3.one * 0.15f, ember);
        flame.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var lightGo = NewChild("SconceLight", onWall + new Vector3(0f, 0.15f, 0f));
        var pl = lightGo.AddComponent<Light>();
        pl.type      = LightType.Point;
        pl.color     = new Color(1f, 0.72f, 0.42f);
        pl.range     = 5f;
        pl.intensity = 2f;
        lightGo.AddComponent<Flicker>().Bind(pl, 2f);
    }

    // ── Pomůcky pro stavbu z primitivů ───────────────────────────────────
    private GameObject NewChild(string name, Vector3 localPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        return go;
    }

    private GameObject Box(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        => Prim(PrimitiveType.Cube, parent, name, pos, scale, Vector3.zero, mat);

    private GameObject Sphere(Transform parent, string name, Vector3 pos, Vector3 scale, Material mat)
        => Prim(PrimitiveType.Sphere, parent, name, pos, scale, Vector3.zero, mat);

    private GameObject Cyl(Transform parent, string name, Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
        => Prim(PrimitiveType.Cylinder, parent, name, pos, scale, euler, mat);

    private GameObject Prim(PrimitiveType type, Transform parent, string name,
                            Vector3 pos, Vector3 scale, Vector3 euler, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col); // dekorace nemá nic blokovat

        go.transform.SetParent(parent, false);
        go.transform.localPosition    = pos;
        go.transform.localScale       = scale;
        go.transform.localEulerAngles = euler;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    // ── Mihotání světla (oheň / lucerna) ─────────────────────────────────
    private class Flicker : MonoBehaviour
    {
        private Light target;
        private float baseIntensity;
        private float seed;

        public void Bind(Light l, float intensity)
        {
            target        = l;
            baseIntensity = intensity;
            seed          = Random.value * 100f;
        }

        void Update()
        {
            if (target == null) return;
            float n = Mathf.PerlinNoise(seed, Time.time * 6f);      // 0..1
            target.intensity = baseIntensity * (0.82f + n * 0.30f); // ~0.82–1.12×
        }
    }
}
