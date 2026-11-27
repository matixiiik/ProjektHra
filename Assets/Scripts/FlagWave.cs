using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  FlagWave.cs
//  Vlající vlajka (fáze B). Připojí se na objekt vlajky (kvádr z GridManager.
//  AddHostileFlag) a vymění jeho síť za jemnou mřížku 10×4 políček. Každý snímek
//  posune vrcholy do strany podle sinusovky — u žerdi se vlajka nehýbe, ke konci
//  vlaje čím dál víc (jako látka ve větru).
//
//  Počítá se jen, když vlajku nějaká kamera vidí (Renderer.isVisible), takže
//  vzdálené ostrovy nic nestojí. Materiál se přepne na oboustranný (Cull Off),
//  ať je vlajka vidět z obou stran.
// ─────────────────────────────────────────────────────────────────────────────

[RequireComponent(typeof(MeshFilter))]
public class FlagWave : MonoBehaviour
{
    private const int   COLS      = 10;    // dílků po délce vlajky
    private const int   ROWS      = 4;     // dílků na výšku
    private const float AMPLITUDE = 0.10f; // jak moc konec vlajky vlaje (v jednotkách světa)
    private const float SPEED     = 4.5f;  // rychlost vlnění
    private const float WAVES     = 5.5f;  // kolik vln je na vlajce

    private Mesh      mesh;
    private Vector3[] basePos;   // rovná vlajka (bez vlnění)
    private Vector3[] moved;     // pracovní pole — ať se každý snímek nealokuje nové
    private Renderer  rend;
    private float     phase;     // posun fáze, ať všechny vlajky nevlají stejně

    void Start()
    {
        rend = GetComponent<Renderer>();
        phase = (transform.position.x * 0.37f + transform.position.z * 0.21f) % 6.28f;

        // Kvádr měl tloušťku přes měřítko Z (0,04) — plochá síť tloušťku nepotřebuje,
        // a s Z = 1 je výchylka vlnění přímo v jednotkách světa.
        Vector3 sc = transform.localScale;
        transform.localScale = new Vector3(sc.x, sc.y, 1f);

        BuildMesh();
        GetComponent<MeshFilter>().sharedMesh = mesh;

        // Oboustranný materiál (URP Lit má vlastnost _Cull: 0 = nevynechávat žádnou stranu).
        if (rend != null && rend.material.HasProperty("_Cull")) rend.material.SetFloat("_Cull", 0f);
    }

    // Plochá mřížka v rovině XY, x od -0,5 (u žerdi) do +0,5 (volný konec).
    private void BuildMesh()
    {
        int vx = COLS + 1, vy = ROWS + 1;
        basePos = new Vector3[vx * vy];
        var uv  = new Vector2[vx * vy];
        for (int y = 0; y < vy; y++)
            for (int x = 0; x < vx; x++)
            {
                int i = y * vx + x;
                basePos[i] = new Vector3(x / (float)COLS - 0.5f, y / (float)ROWS - 0.5f, 0f);
                uv[i]      = new Vector2(x / (float)COLS, y / (float)ROWS);
            }

        var tris = new int[COLS * ROWS * 6];
        int t = 0;
        for (int y = 0; y < ROWS; y++)
            for (int x = 0; x < COLS; x++)
            {
                int a = y * vx + x, b = a + 1, c = a + vx, d = c + 1;
                tris[t++] = a; tris[t++] = c; tris[t++] = b;
                tris[t++] = b; tris[t++] = c; tris[t++] = d;
            }

        moved = (Vector3[])basePos.Clone();
        mesh = new Mesh { name = "FlagWaveMesh" };
        mesh.vertices  = moved;
        mesh.uv        = uv;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.MarkDynamic();
    }

    void Update()
    {
        if (mesh == null || rend == null || !rend.isVisible) return;

        float time = Time.time * SPEED + phase;
        for (int i = 0; i < basePos.Length; i++)
        {
            Vector3 p = basePos[i];
            float along = p.x + 0.5f;                      // 0 u žerdi … 1 na konci
            float wave  = Mathf.Sin(time - along * WAVES) * AMPLITUDE * along;
            moved[i] = new Vector3(p.x, p.y - along * along * 0.04f, wave); // konec trochu povisne
        }
        mesh.vertices = moved;
        mesh.RecalculateNormals();
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
    }
}
