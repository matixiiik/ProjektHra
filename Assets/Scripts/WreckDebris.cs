using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  WreckDebris.cs
//  Trosky po rozbité lodi. Nese kus nákladu (ryby / poklady / náboje), který
//  se při zkáze lodě vysypal do vody. Chvíli plave na hladině — když k němu
//  hráč (co plave) doplave, náklad se mu vrátí. Když ho nechá, po ~18 s klesne
//  a je pryč.
//
//  Vytváří se přes WreckDebris.Spawn(...) z PlayerController.WreckBoat().
//  Model = pár beden a prken z primitiv.
// ─────────────────────────────────────────────────────────────────────────────

public class WreckDebris : MonoBehaviour
{
    private const float FLOAT_TIME   = 18f;   // jak dlouho plave, než začne klesat
    private const float SINK_TIME    = 3f;    // jak dlouho pak klesá (pak se zničí)
    private const float PICKUP_RANGE = 1.6f;  // na kolik doplavat, ať se náklad vrátí
    private const float WATER_Y      = -0.12f;

    private int ownerIndex;
    private int fish, treasure, ammo;
    private float age;

    public static void Spawn(Vector3 pos, int ownerIndex, int fish, int treasure, int ammo)
    {
        var go = new GameObject("WreckDebris");
        go.transform.position = new Vector3(pos.x, WATER_Y, pos.z);

        var d = go.AddComponent<WreckDebris>();
        d.ownerIndex = ownerIndex;
        d.fish = fish; d.treasure = treasure; d.ammo = ammo;

        d.BuildModel();
    }

    void BuildModel()
    {
        Material wood = MakeMat(new Color(0.32f, 0.22f, 0.14f));

        // Dvě bedny + prkno, mírně natočené.
        AddBox(new Vector3(-0.18f, 0.08f, 0f), new Vector3(0.34f, 0.28f, 0.34f), 18f, wood);
        AddBox(new Vector3( 0.2f,  0.05f, 0.1f), new Vector3(0.28f, 0.24f, 0.28f), -25f, wood);
        AddBox(new Vector3( 0.05f, 0.02f, -0.25f), new Vector3(0.7f, 0.06f, 0.16f), 8f, wood);
    }

    void AddBox(Vector3 pos, Vector3 scale, float yaw, Material mat)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(b.GetComponent<Collider>());
        b.transform.SetParent(transform, false);
        b.transform.localPosition    = pos;
        b.transform.localScale       = scale;
        b.transform.localEulerAngles = new Vector3(0f, yaw, 0f);
        var r = b.GetComponent<Renderer>();
        if (r != null) { r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; }
    }

    void Update()
    {
        age += Time.deltaTime;

        // Jemné houpání + po FLOAT_TIME klesání.
        float bob  = Mathf.Sin(age * 2f) * 0.04f;
        float sink = age > FLOAT_TIME ? (age - FLOAT_TIME) / SINK_TIME * 1.5f : 0f;
        transform.position = new Vector3(transform.position.x, WATER_Y + bob - sink, transform.position.z);
        transform.Rotate(0f, 8f * Time.deltaTime, 0f);

        if (age >= FLOAT_TIME + SINK_TIME) { Destroy(gameObject); return; }

        // Doplaval k němu majitel (plave = rozbitá loď)? → vrať náklad.
        foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
        {
            if (pc.playerIndex != ownerIndex || !pc.IsSwimming) continue;
            if ((pc.transform.position - transform.position).sqrMagnitude > PICKUP_RANGE * PICKUP_RANGE) continue;

            pc.RecoverCargo(fish, treasure, ammo);
            if (CombatDirector.Instance != null)
                CombatDirector.Instance.Toast("Zachranil jsi cast nakladu z vraku.");
            Destroy(gameObject);
            return;
        }
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
