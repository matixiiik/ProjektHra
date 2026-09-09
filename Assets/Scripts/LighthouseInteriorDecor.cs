using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  LighthouseInteriorDecor.cs
//  Zařídí kulatou místnost v majáku — SYMETRICKY (vše zrcadleno podle osy Z),
//  podle náčrtu od uživatele:
//   • zadní stěna: JEDEN dlouhý dřevěný stůl ROVNOBĚŽNÝ SE DVEŘMI, za ním tři
//     prodavači (vylepšení / questy / výkupna) v tričku barvy obchodu
//   • na zdi nad stolem velká mapa
//   • uprostřed: kruhový kobereček + závěsná lampa
//   • po stranách: ohřívadlo + lucerna + květina + bedny (dekorace — nemusí být)
//  Původní barevné kostky pultů se schovají, náhodné bedny/sudy ze scény taky.
//
//  Skript sedí na "InteriorManagers" ve scéně LighthouseInterior a všechno si
//  staví sám v Start() z primitiv + materiálů v kódu. Čistě vizuální (kromě
//  omezení pochozí plochy na kobereček), nemá vliv na hratelnost.
//
//  Pozn.: v coopu se interiér posouvá o velký offset od oceánu (řeší
//  LighthouseInterior.Awake). Stavíme až v Start() jako děti tohoto objektu →
//  posun se propíše sám. Pulty ze scény hýbeme RELATIVNĚ.
// ─────────────────────────────────────────────────────────────────────────────

public class LighthouseInteriorDecor : MonoBehaviour
{
    private const float WALL_R      = 3.8f;  // poloměr kulaté stěny
    private const float WALK_RADIUS = 2.5f;  // kam smí hráč (zhruba kobereček)

    // Barvy trička prodavačů = barvy obchodů.
    private static readonly Color UpgradeShirt = new Color(0.28f, 0.45f, 0.68f); // modrá — vylepšení (vlevo)
    private static readonly Color QuestShirt   = new Color(0.86f, 0.52f, 0.16f); // oranžová — questy (uprostřed)
    private static readonly Color SellShirt    = new Color(0.30f, 0.62f, 0.30f); // zelená — výkupna (vpravo)

    // Jeden dlouhý stůl u zadní stěny (rovnoběžný se dveřmi = podél osy X).
    private const float TABLE_Z    = 2.25f; // jak daleko dozadu je stůl (víc do místnosti)
    private const float SLOT_X     = 1.3f;  // rozestup 3 obchodů podél stolu
    private const float COUNTER_Z  = 1.65f; // kde je bod interakce (před stolem)
    private const float KEEPER_Z   = 2.70f; // kde stojí prodavač (za stolem)
    private static readonly Vector3 SceneQuestPos = new Vector3(2.6f, 0.5f, 2.5f); // původní poloha kostky (coop)

    private Material wood, woodDark, stone, cloth, leaf, ember, metal, skin;

    void Start()
    {
        BuildMaterials();
        WarmUpSceneLights();
        HideSceneClutter();
        ClampWalkArea();

        // Střed — nízký sloup + závěsná lampa, kolem kruhový kobereček.
        BuildCentre();

        // Zrcadlená dekorace po stranách.
        for (int s = -1; s <= 1; s += 2)
        {
            BuildBrazier(new Vector3(s * (WALL_R - 0.5f), 0f, -0.4f));
            BuildWallSconce(s * 118f);
            BuildPlant(new Vector3(s * 3.0f, 0f, -1.8f));
            BuildCrates(new Vector3(s * 3.1f, 0f, -0.7f), s);
        }

        // Jeden dlouhý stůl u zadní stěny + velká mapa nad ním.
        BuildMainTable();
        BuildWallMap();

        // Tři obchody podél stolu: 3 body interakce + 3 prodavači.
        var up = FindInScene("Counter_Upgrade");
        var qu = FindInScene("Counter_Quest");

        // V coopu je celý interiér posunutý — kostky pultu ze scény přesouváme
        // absolutně, takže si offset spočítáme z Counter_Quest.
        Vector3 coopOffset = qu != null ? qu.transform.position - SceneQuestPos : Vector3.zero;

        PlaceCounter(up, -SLOT_X, coopOffset, UpgradeShirt);
        PlaceCounter(qu,  0f,     coopOffset, QuestShirt);
        BuildSellCounter(coopOffset);
    }

    // Přesune bod interakce daného obchodu před stůl (X = slotX + offset) a
    // postaví za stůl prodavače v tričku barvy obchodu.
    private void PlaceCounter(GameObject counter, float slotX, Vector3 coopOffset, Color shirt)
    {
        if (counter == null) return;

        foreach (var mr in counter.GetComponentsInChildren<MeshRenderer>(true))
            mr.enabled = false;

        counter.transform.position = coopOffset + new Vector3(slotX, 0.5f, COUNTER_Z);

        var it = counter.GetComponent<InteriorInteractable>();
        if (it != null && it.range < 2.4f) it.range = 2.4f;

        BuildKeeper(slotX, shirt);
    }

    private void BuildSellCounter(Vector3 coopOffset)
    {
        var go = new GameObject("Counter_Sell");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        go.transform.position = coopOffset + new Vector3(SLOT_X, 0.5f, COUNTER_Z);

        var it = go.AddComponent<InteriorInteractable>();
        it.action = InteriorAction.QuestShopSell;
        it.range  = 2.4f;
        it.prompt = "E — vykupna (prodej korist)";

        BuildKeeper(SLOT_X, SellShirt);
    }

    // ── Jeden dlouhý stůl (rovnoběžný se dveřmi) ─────────────────────────
    private void BuildMainTable()
    {
        var root = NewChild("MainTable", new Vector3(0f, 0f, TABLE_Z));
        // žádná rotace → deska běží podél X = rovnoběžně se dveřmi (které jsou na −Z)

        Box(root.transform, "Top",    new Vector3(0f, 0.66f, -0.05f), new Vector3(4.8f, 0.14f, 0.95f), wood);
        Box(root.transform, "Front",  new Vector3(0f, 0.33f, -0.45f), new Vector3(4.8f, 0.66f, 0.16f), woodDark);
        Box(root.transform, "Back",   new Vector3(0f, 0.33f, 0.42f),  new Vector3(4.8f, 0.66f, 0.14f), woodDark);
        Box(root.transform, "EndL",   new Vector3(-2.35f, 0.33f, 0f), new Vector3(0.16f, 0.66f, 0.9f), woodDark);
        Box(root.transform, "EndR",   new Vector3(2.35f, 0.33f, 0f),  new Vector3(0.16f, 0.66f, 0.9f), woodDark);

        // Pár věcí na stole (mezi obchody), ať není holý.
        Cyl(root.transform, "Jar",   new Vector3(-0.7f, 0.82f, 0f), new Vector3(0.16f, 0.14f, 0.16f), Vector3.zero, stone);
        Box(root.transform, "Book",  new Vector3(0.75f, 0.77f, 0.05f), new Vector3(0.34f, 0.09f, 0.26f), Mat(new Color(0.4f, 0.3f, 0.5f)));
        Box(root.transform, "Ledger", new Vector3(2.0f, 0.77f, 0f),   new Vector3(0.3f, 0.08f, 0.4f), woodDark);
    }

    // Velká mapa na zdi nad stolem.
    private void BuildWallMap()
    {
        const float MZ = 3.35f; // před zadní stěnou

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "WallMap";
        Destroy(quad.GetComponent<Collider>());
        quad.transform.SetParent(transform, false);
        quad.transform.localPosition    = new Vector3(0f, 2.55f, MZ);
        quad.transform.localEulerAngles = new Vector3(0f, 180f, 0f); // čelem do místnosti (−Z)
        quad.transform.localScale       = new Vector3(2.8f, 1.9f, 1f);
        var mr = quad.GetComponent<MeshRenderer>();
        mr.sharedMaterial    = Mat(new Color(0.80f, 0.70f, 0.48f)); // pergamen
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // dřevěný rám kolem mapy
        Box(transform, "MapFrameL", new Vector3(-1.48f, 2.55f, MZ + 0.02f), new Vector3(0.12f, 2.1f, 0.06f), woodDark);
        Box(transform, "MapFrameR", new Vector3( 1.48f, 2.55f, MZ + 0.02f), new Vector3(0.12f, 2.1f, 0.06f), woodDark);
        Box(transform, "MapFrameT", new Vector3(0f, 3.55f, MZ + 0.02f),     new Vector3(3.0f, 0.12f, 0.06f), woodDark);
        Box(transform, "MapFrameB", new Vector3(0f, 1.55f, MZ + 0.02f),     new Vector3(3.0f, 0.12f, 0.06f), woodDark);
    }

    // Prodavač za stolem na dané X-pozici, čelem ke dveřím (−Z).
    private void BuildKeeper(float x, Color shirt)
    {
        var g = NewChild("Shopkeeper", new Vector3(x, 0f, KEEPER_Z));

        // Kenney model postavy (idle), obarvený podle barvy trička; čelem ke dveřím.
        var model = CharacterModel.TryBuild(g.transform, "character-male-c",
            CharacterModel.DEFAULT_SCALE, shirt, "PlayerAnim");
        if (model != null)
        {
            g.transform.rotation = Quaternion.LookRotation(Vector3.back); // Kenney model kouká +Z → otoč
            return;
        }

        // Fallback — prodavač ze základních tvarů.
        Material shirtMat = Mat(shirt);
        Box(g.transform,    "Legs", new Vector3(0f, 0.30f, 0f),     new Vector3(0.42f, 0.60f, 0.36f), woodDark);
        Cyl(g.transform,    "Body", new Vector3(0f, 0.86f, 0f),     new Vector3(0.56f, 0.42f, 0.56f), Vector3.zero, shirtMat);
        Sphere(g.transform, "Head", new Vector3(0f, 1.34f, 0f),     new Vector3(0.40f, 0.40f, 0.40f), skin);
        Box(g.transform,    "Nose", new Vector3(0f, 1.32f, -0.22f), new Vector3(0.09f, 0.09f, 0.14f), skin);
    }

    // ── Střed: kruhový kobereček + závěsná lampa ────────────────────────
    private void BuildCentre()
    {
        var rug = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        rug.name = "CentreRug";
        Destroy(rug.GetComponent<Collider>());
        rug.transform.SetParent(transform, false);
        rug.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        rug.transform.localScale    = new Vector3(4.4f, 0.02f, 4.4f);
        var rmr = rug.GetComponent<MeshRenderer>();
        rmr.sharedMaterial    = cloth;
        rmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        BuildHangingLamp(new Vector3(0f, 0f, 0f));
    }

    // ── Ohřívadlo (koš s ohněm) — u boční stěny, zrcadlené ────────────────
    private void BuildBrazier(Vector3 pos)
    {
        var root = NewChild("Brazier", pos);

        Cyl(root.transform, "Foot", new Vector3(0f, 0.10f, 0f), new Vector3(0.30f, 0.10f, 0.30f), Vector3.zero, metal);
        Cyl(root.transform, "Post", new Vector3(0f, 0.45f, 0f), new Vector3(0.12f, 0.45f, 0.12f), Vector3.zero, metal);
        Cyl(root.transform, "Bowl", new Vector3(0f, 0.92f, 0f), new Vector3(0.55f, 0.16f, 0.55f), Vector3.zero, metal);
        var fire = Sphere(root.transform, "Fire", new Vector3(0f, 1.06f, 0f), new Vector3(0.42f, 0.34f, 0.42f), ember);
        fire.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var lightGo = NewChild("BrazierLight", pos + new Vector3(0f, 1.1f, 0f));
        var fl = lightGo.AddComponent<Light>();
        fl.type      = LightType.Point;
        fl.color     = new Color(1f, 0.6f, 0.28f);
        fl.range     = 7.5f;
        fl.intensity = 2.8f;
        lightGo.AddComponent<Flicker>().Bind(fl, 2.8f);
    }

    // ── Závěsná lampa (přesně uprostřed) ─────────────────────────────────
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

    // ── Květina v květináči ─────────────────────────────────────────────
    private void BuildPlant(Vector3 pos)
    {
        var root = NewChild("Plant", pos);
        Cyl(root.transform, "Pot",  new Vector3(0f, 0.2f, 0f),           new Vector3(0.42f, 0.22f, 0.42f), Vector3.zero, woodDark);
        Sphere(root.transform, "Leaf0", new Vector3(0f, 0.58f, 0f),         new Vector3(0.6f, 0.55f, 0.6f), leaf);
        Sphere(root.transform, "Leaf1", new Vector3(0.14f, 0.8f, 0.08f),    new Vector3(0.4f, 0.4f, 0.4f), leaf);
        Sphere(root.transform, "Leaf2", new Vector3(-0.12f, 0.76f, -0.07f), new Vector3(0.36f, 0.36f, 0.36f), leaf);
    }

    // ── Bedny (dvě na sobě) — zrcadlené ────────────────────────────────
    private void BuildCrates(Vector3 pos, int side)
    {
        var root = NewChild("Crates", pos);
        root.transform.localRotation = Quaternion.Euler(0f, side * 18f, 0f);
        Box(root.transform, "Crate0", new Vector3(0f, 0.28f, 0f),   new Vector3(0.56f, 0.56f, 0.56f), wood);
        Box(root.transform, "Crate1", new Vector3(0.1f, 0.78f, 0.06f), new Vector3(0.46f, 0.46f, 0.46f), woodDark);
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
        pl.range     = 4.5f;
        pl.intensity = 1.8f;
        lightGo.AddComponent<Flicker>().Bind(pl, 1.8f);
    }

    // ── Úklid: schovej náhodné bedny/sudy ze scény (kazí symetrii) ──────
    private void HideSceneClutter()
    {
        foreach (var t in FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.gameObject.scene != gameObject.scene) continue;
            string n = t.name.ToLowerInvariant();
            if (n == "chest" || n == "barrel" || n == "crate" || n == "cannon" || n == "rug"
             || n.StartsWith("bottle") || n.StartsWith("flag"))
                foreach (var mr in t.GetComponentsInChildren<MeshRenderer>(true))
                    mr.enabled = false;
        }
    }

    private void ClampWalkArea()
    {
        foreach (var ip in FindObjectsByType<InteriorPlayer>(FindObjectsSortMode.None))
            if (ip.gameObject.scene == gameObject.scene)
            {
                ip.areaRadius  = WALK_RADIUS;
                ip.innerRadius = 0f; // uprostřed nic není
            }

        foreach (var it in FindObjectsByType<InteriorInteractable>(FindObjectsSortMode.None))
            if (it.gameObject.scene == gameObject.scene && it.range < 2.2f) it.range = 2.2f;
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
        cloth    = Mat(new Color(0.55f, 0.20f, 0.18f)); // teplá červená (kobereček)
        leaf     = Mat(new Color(0.30f, 0.52f, 0.26f));
        metal    = Mat(new Color(0.24f, 0.23f, 0.22f));
        skin     = Mat(new Color(0.85f, 0.68f, 0.55f));

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
    private void WarmUpSceneLights()
    {
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (l.gameObject.scene != gameObject.scene) continue;
            if (l.type == LightType.Directional && l.color.b > l.color.r)
            {
                l.color     = Color.Lerp(l.color, new Color(0.85f, 0.85f, 0.9f), 0.6f);
                l.intensity *= 0.6f;
            }
        }

        var warm = NewChild("WarmFill", Vector3.zero);
        var wl = warm.AddComponent<Light>();
        wl.type      = LightType.Directional;
        wl.color     = new Color(1f, 0.80f, 0.58f);
        wl.intensity = 0.5f;
        warm.transform.rotation = Quaternion.Euler(55f, 0f, 0f); // symetricky = shora zepředu
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
        if (col != null) Destroy(col);

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
            float n = Mathf.PerlinNoise(seed, Time.time * 6f);
            target.intensity = baseIntensity * (0.82f + n * 0.30f);
        }
    }
}
