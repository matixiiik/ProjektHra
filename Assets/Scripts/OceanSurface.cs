using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  OceanSurface.cs
//  Jedna velká vodní plocha místo stovek malých vodních dlaždic. Plocha pořád
//  "jede" za hráčem, takže vypadá jako nekonečné moře bez viditelné mřížky.
//
//  Vlny = součet dvou pomalých dlouhých sinusovek počítaný ve SVĚTOVÝCH
//  souřadnicích. Díky tomu hladina plynule navazuje i ve chvíli, kdy se
//  plocha posune za hráčem — nevypadá to jako blikající nebo ujíždějící textura.
//
//  Voda je poloprůhledná (viz waterAlpha) — přes ni je vidět mořské dno
//  (viz SeaFloor) a potopené vraky pokladů.
//
//  Objekt i s tímto skriptem vytváří GridManager při startu (viz CreateSeaWorld).
//  Ve scéně není potřeba nic přidávat ani zapojovat.
// ─────────────────────────────────────────────────────────────────────────────

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class OceanSurface : MonoBehaviour
{
    [Header("Hladina")]
    public float seaLevel  = -0.22f; // klidová výška vody (níž = ostrovy víc koukají z vody)
    public float waterAlpha = 0.62f; // průhlednost vody (1 = neprůhledná, míň = víc vidět dno)

    [Header("Vlny (jemné dlouhé vlnobití)")]
    public float amp1 = 0.045f, len1 = 14f, spd1 = 0.30f; // hlavní dlouhá vlna
    public float amp2 = 0.028f, len2 = 7f,  spd2 = 0.55f; // menší křížová vlna

    // ── Rozměry plochy (konstanty — velikost se za běhu nemění) ──────────────
    // Menší než dohled — za okrajem (schovaným v mlze) je vidět obloha (skybox).
    private const float SIZE = 150f; // délka strany plochy (v políčkách)
    private const float STEP = 5f;   // rozteč vrcholů mřížky (větší = míň vrcholů = svižnější)

    private Transform p1;         // hráč 1 (vždy)
    private Transform p2;         // hráč 2 (jen split-screen; vzniká později přes MultiplayerManager)
    private float     nextP2Scan; // časovač dohledávání hráče 2

    private Mesh      mesh;
    private Vector3[] flat;       // vrcholy v rovině (bez vln), lokální souřadnice
    private Vector3[] work;       // pracovní pole s aktuální výškou vln

    /// <summary>Zavolá GridManager hned po vytvoření objektu.</summary>
    public void Init(Material waterMaterial, Transform player1)
    {
        p1 = player1;

        var mr = GetComponent<MeshRenderer>();
        Color waterBase = Color.cyan;
        if (waterMaterial != null)
        {
            // Vlastní kopie materiálu — ať nesaháme na sdílený asset ve složce.
            var mat = new Material(waterMaterial);
            Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : Color.cyan;
            waterBase = c;
            c.a = waterAlpha;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color", c);
            mr.sharedMaterial = mat;
        }
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;

        // Stejná vrstva jako dřív vodní dlaždice ("Water" = builtin vrstva 4).
        int water = LayerMask.NameToLayer("Water");
        if (water >= 0) gameObject.layer = water;

        BuildFlatMesh();
        GetComponent<MeshFilter>().sharedMesh = mesh;

        BuildSkin(mr.sharedMaterial, waterBase);

        FollowPlayers();
    }

    // Tenká tmavší "kožka" těsně nad hlavní hladinou. Sdílí animovanou síť, takže
    // se vlní s ní. Je hodně průhledná → u lodě je hladina znát tmavší, ale do
    // hloubky (dno, vraky) je pořád vidět.
    void BuildSkin(Material baseWaterMat, Color waterBase)
    {
        if (baseWaterMat == null) return;

        var skinGo = new GameObject("OceanSkin");
        skinGo.transform.SetParent(transform, false);
        skinGo.transform.localPosition = new Vector3(0f, 0.02f, 0f);
        skinGo.layer = gameObject.layer;

        skinGo.AddComponent<MeshFilter>().sharedMesh = mesh; // stejná (animovaná) síť

        var smr = skinGo.AddComponent<MeshRenderer>();
        smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        smr.receiveShadows    = false;

        var skinMat = new Material(baseWaterMat);
        Color sc = new Color(waterBase.r * 0.5f, waterBase.g * 0.6f, waterBase.b * 0.65f, 0.22f);
        if (skinMat.HasProperty("_BaseColor")) skinMat.SetColor("_BaseColor", sc);
        if (skinMat.HasProperty("_Color"))     skinMat.SetColor("_Color", sc);
        skinMat.renderQueue = baseWaterMat.renderQueue + 1; // kreslit až po hlavní hladině
        smr.sharedMaterial = skinMat;
    }

    // Postaví plochou čtvercovou síť vrcholů STEP od sebe, vycentrovanou na (0,0).
    void BuildFlatMesh()
    {
        int n = Mathf.RoundToInt(SIZE / STEP) + 1; // počet vrcholů na jednu stranu
        flat = new Vector3[n * n];
        work = new Vector3[n * n];
        var tris = new int[(n - 1) * (n - 1) * 6];

        float half = SIZE * 0.5f;
        int v = 0;
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                flat[v] = new Vector3(-half + i * STEP, 0f, -half + j * STEP);
                v++;
            }

        int t = 0;
        for (int j = 0; j < n - 1; j++)
            for (int i = 0; i < n - 1; i++)
            {
                int row = j * n + i;
                tris[t++] = row;
                tris[t++] = row + n;
                tris[t++] = row + 1;
                tris[t++] = row + 1;
                tris[t++] = row + n;
                tris[t++] = row + n + 1;
            }

        mesh = new Mesh { name = "OceanSurface" };
        if (flat.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices  = flat;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
    }

    void LateUpdate()
    {
        // Ve split-screenu vzniká hráč 2 až po startu — občas ho zkus dohledat.
        if (p2 == null && MultiplayerManager.IsMultiplayer && Time.time >= nextP2Scan)
        {
            nextP2Scan = Time.time + 0.5f;
            foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (pc.playerIndex == 1) { p2 = pc.transform; break; }
        }

        FollowPlayers();
        Animate();
    }

    // Umístí střed plochy k hráči (ve split-screenu doprostřed mezi oba hráče),
    // zaokrouhleno na rozteč vrcholů, ať se síť viditelně nechvěje.
    void FollowPlayers()
    {
        Vector3 c;
        if (p1 != null && p2 != null) c = (p1.position + p2.position) * 0.5f;
        else if (p1 != null)          c = p1.position;
        else                          c = transform.position;

        float sx = Mathf.Round(c.x / STEP) * STEP;
        float sz = Mathf.Round(c.z / STEP) * STEP;
        transform.position = new Vector3(sx, seaLevel, sz);
    }

    // Posune každý vrchol na výšku dvou sečtených sinusovek podle jeho SVĚTOVÉ
    // pozice — proto hladina navazuje i po posunu plochy za hráčem.
    void Animate()
    {
        float time = Time.time;
        float ox = transform.position.x;
        float oz = transform.position.z;

        for (int k = 0; k < flat.Length; k++)
        {
            float wx = flat[k].x + ox;
            float wz = flat[k].z + oz;

            float h = Mathf.Sin((wx * 0.9f + wz * 0.5f) / len1 + time * spd1) * amp1
                    + Mathf.Sin((wx * -0.4f + wz * 1.0f) / len2 + time * spd2) * amp2;

            work[k] = new Vector3(flat[k].x, h, flat[k].z);
        }

        mesh.vertices = work;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
