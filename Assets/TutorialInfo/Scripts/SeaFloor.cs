using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  SeaFloor.cs
//  Mořské dno pod hladinou. Přes poloprůhlednou vodu (viz OceanSurface) je vidět
//  jako členité dno — díky tomu má moře "hloubku" a nekouká se do prázdna.
//
//  Je to jedna velká plocha, která jede za hráčem. Výška vrcholů = Perlinův šum
//  ve SVĚTOVÝCH souřadnicích (hloubka ~3 až ~10 pod hladinou), takže dno je pořád
//  stejné na stejném místě a jen se dogeneruje kolem hráče.
//
//  Pod políčky s vrakem (Treasure) se dno PLYNULE zvedne do mělčiny (~2.8 pod
//  hladinu) — vypadá to jako přírodní mělčina/útes, na kterém vrak uvázl, ne
//  jako umělá kupka uprostřed ničeho. Souřadnice vraků dodává GridManager.
//
//  Objekt vytváří GridManager (viz CreateSeaWorld). Nemá kolizi ani vliv na hru.
// ─────────────────────────────────────────────────────────────────────────────

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SeaFloor : MonoBehaviour
{
    private const float SIZE      = 164f;  // strana plochy
    private const float STEP      = 4f;    // rozteč vrcholů (jemnější, ať se dá vykreslit mělčina pod vrakem)
    private const float FLOOR_MIN = -10f;  // nejhlubší místo dna
    private const float FLOOR_MAX = -3f;   // nejmělčí "normální" místo dna
    private const float NOISE     = 0.045f;// měřítko Perlinova šumu

    private const float SHOAL_Y      = -2.7f; // jak vysoko se dno zvedne pod vrakem
    private const float SHOAL_RADIUS = 9f;    // do jaké vzdálenosti od vraku se dno zvedá
    private const int   SCAN_RADIUS  = 44;    // v kolika políčkách kolem hledat vraky

    private Transform   p1;
    private Transform   p2;
    private float       nextP2Scan;
    private float       nextWreckRescan; // občas přepočítat dno, i když hráč stojí (mohl se dogenerovat nový vrak)
    private GridManager grid;

    private Mesh       mesh;
    private Vector3[]  verts;
    private Vector2Int lastSnap = new Vector2Int(int.MaxValue, int.MaxValue);

    private readonly List<Vector2Int> wrecks = new List<Vector2Int>();

    /// <summary>Zavolá GridManager hned po vytvoření objektu.</summary>
    public void Init(Material sandMaterial, Transform player1, GridManager gridManager)
    {
        p1   = player1;
        grid = gridManager;

        var mr = GetComponent<MeshRenderer>();
        if (sandMaterial != null)
        {
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
        if (verts.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
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

        // Hráč může stát na místě ve chvíli, kdy se opodál dogeneruje políčko s
        // vrakem — Snap by pak Reshape nezavolal. Proto ho jednou za čas vynutíme.
        if (Time.time >= nextWreckRescan)
        {
            nextWreckRescan = Time.time + 1f;
            Reshape();
        }
    }

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

    // Výška vrcholu = Perlinův šum + plynulé zvednutí pod vraky.
    void Reshape()
    {
        float ox = transform.position.x;
        float oz = transform.position.z;

        if (grid != null)
            grid.CollectTreasureTilesNear(Mathf.RoundToInt(ox), Mathf.RoundToInt(oz), SCAN_RADIUS, wrecks);
        else
            wrecks.Clear();

        for (int k = 0; k < verts.Length; k++)
        {
            float wx = verts[k].x + ox;
            float wz = verts[k].z + oz;

            float noise  = Mathf.PerlinNoise(wx * NOISE + 500f, wz * NOISE + 500f);
            float floorY = Mathf.Lerp(FLOOR_MIN, FLOOR_MAX, noise * noise);

            // Nejbližší vrak → plynulý nájezd dna vzhůru (přírodní mělčina).
            float shoal = 0f;
            for (int t = 0; t < wrecks.Count; t++)
            {
                float dx = wx - wrecks[t].x;
                float dz = wz - wrecks[t].y;
                float s  = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dz * dz) / SHOAL_RADIUS);
                s = s * s * (3f - 2f * s); // smoothstep
                if (s > shoal) shoal = s;
            }
            if (shoal > 0f) floorY = Mathf.Max(floorY, Mathf.Lerp(floorY, SHOAL_Y, shoal));

            verts[k].y = floorY;
        }

        mesh.vertices = verts;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}
