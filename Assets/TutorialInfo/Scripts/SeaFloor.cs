using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  SeaFloor.cs
//  Mořské dno hluboko pod hladinou. Přes poloprůhlednou vodu (viz OceanSurface)
//  je vidět jako tmavé členité dno — díky tomu má moře "hloubku" a nekouká se
//  do prázdna.
//
//  Je to jedna velká plocha, která jede za hráčem. Výška vrcholů = Perlinův šum
//  ve SVĚTOVÝCH souřadnicích (hloubka ~3 až ~10 pod hladinou), takže dno je
//  pořád stejné na stejném místě a jen se dogeneruje kolem hráče. Pod vraky
//  pokladů je navíc písčitá kupa (viz GridManager), ať vrak nestojí ve vzduchu.
//
//  Objekt vytváří GridManager (viz CreateSeaWorld). Nemá kolizi ani vliv na hru —
//  těžba pokladu funguje dál stejně (mezerník na políčku pokladu).
// ─────────────────────────────────────────────────────────────────────────────

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SeaFloor : MonoBehaviour
{
    private const float SIZE      = 164f;  // strana plochy (o kus větší než hladina, ať pod ní není mezera)
    private const float STEP      = 8f;    // rozteč vrcholů (dno je daleko, stačí hrubší síť)
    private const float FLOOR_MIN = -10f;  // nejhlubší místo dna (hloubka ~10)
    private const float FLOOR_MAX = -3f;   // nejmělčí místo dna (hloubka ~3) — přes vodu je vidět
    private const float NOISE     = 0.045f;// měřítko Perlinova šumu (menší = větší kopce)

    private Transform p1;
    private Transform p2;
    private float     nextP2Scan;

    private Mesh      mesh;
    private Vector3[] verts;
    private Vector2Int lastSnap = new Vector2Int(int.MaxValue, int.MaxValue);

    /// <summary>Zavolá GridManager hned po vytvoření objektu.</summary>
    public void Init(Material sandMaterial, Transform player1)
    {
        p1 = player1;

        var mr = GetComponent<MeshRenderer>();
        if (sandMaterial != null)
        {
            // Tmavší kopie písčitého materiálu ostrova — ať dno není tak výrazné.
            var mat = new Material(sandMaterial);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.33f, 0.38f, 0.36f, 1f));
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     new Color(0.33f, 0.38f, 0.36f, 1f));
            mr.sharedMaterial = mat;
        }
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows    = false;

        BuildMesh();
        GetComponent<MeshFilter>().sharedMesh = mesh;

        Snap(force: true);
    }

    // Postaví síť vrcholů (výšku doplní Reshape podle Perlinova šumu).
    void BuildMesh()
    {
        int n = Mathf.RoundToInt(SIZE / STEP) + 1;
        verts = new Vector3[n * n];
        var tris = new int[(n - 1) * (n - 1) * 6];

        float half = SIZE * 0.5f;
        int v = 0;
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
                verts[v++] = new Vector3(-half + i * STEP, FLOOR_MIN, -half + j * STEP);

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

        mesh = new Mesh { name = "SeaFloor" };
        mesh.vertices  = verts;
        mesh.triangles = tris;
    }

    void LateUpdate()
    {
        if (p2 == null && MultiplayerManager.IsMultiplayer && Time.time >= nextP2Scan)
        {
            nextP2Scan = Time.time + 0.5f;
            foreach (var pc in FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (pc.playerIndex == 1) { p2 = pc.transform; break; }
        }
        Snap(force: false);
    }

    // Posune plochu k hráči (zaokrouhleně na STEP) a když se posunula,
    // přepočítá výšky vrcholů podle šumu ve světě.
    void Snap(bool force)
    {
        Vector3 c;
        if (p1 != null && p2 != null) c = (p1.position + p2.position) * 0.5f;
        else if (p1 != null)          c = p1.position;
        else                          c = transform.position;

        int sx = Mathf.RoundToInt(c.x / STEP);
        int sz = Mathf.RoundToInt(c.z / STEP);
        transform.position = new Vector3(sx * STEP, 0f, sz * STEP);

        var snap = new Vector2Int(sx, sz);
        if (!force && snap == lastSnap) return;
        lastSnap = snap;
        Reshape();
    }

    // Výška každého vrcholu = Perlinův šum podle jeho SVĚTOVÉ pozice.
    void Reshape()
    {
        float ox = transform.position.x;
        float oz = transform.position.z;

        for (int k = 0; k < verts.Length; k++)
        {
            float wx = verts[k].x + ox;
            float wz = verts[k].z + oz;
            float noise = Mathf.PerlinNoise(wx * NOISE + 500f, wz * NOISE + 500f); // 0..1
            // noise² → dno je většinou hluboké (~10), mělčiny (~3) jsou jen občas.
            verts[k].y = Mathf.Lerp(FLOOR_MIN, FLOOR_MAX, noise * noise);
        }

        mesh.vertices = verts;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
