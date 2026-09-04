using UnityEngine;
using System.Collections.Generic;

// ─────────────────────────────────────────────────────────────────────────────
//  IslandTerrain.cs
//  Vygeneruje JEDEN hladký 3D mesh pro celý ostrov místo mřížky dlaždic.
//  Okraje se plynule svažují pod hladinu (efekt pláže), povrch má lehký šum,
//  aby vypadal jako přírodní terén, ne jako kostičky.
//
//  Vstup = množina souřadnic pevninových políček ("x,y" jako Vector2Int).
//  Políčko [x,y] pokrývá čtverec [x, x+1) × [y, y+1) ve světě.
// ─────────────────────────────────────────────────────────────────────────────

public static class IslandTerrain
{
    public  const float LAND_Y = 0.02f;   // výška pevniny (nad hladinou)
    private const float DEEP_Y = -0.75f;  // kam se svažuje okraj (pod hladinu)
    private const float RES    = 0.5f;    // rozteč vrcholů mřížky (jemnější = hladší)
    private const float BEACH  = 1.25f;   // šířka svahu pláže (kolik za pevninu mesh sahá)

    /// <summary>Postaví mesh terénu pro daný ostrov.</summary>
    public static Mesh Build(HashSet<Vector2Int> land)
    {
        // ── ohraničení + okraj na pláž ──────────────────────────────────────
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        foreach (var c in land)
        {
            if (c.x < minX) minX = c.x;
            if (c.y < minY) minY = c.y;
            if (c.x > maxX) maxX = c.x;
            if (c.y > maxY) maxY = c.y;
        }

        float m  = BEACH + 0.6f;
        float x0 = minX - m, x1 = maxX + 1 + m;
        float z0 = minY - m, z1 = maxY + 1 + m;

        int nx = Mathf.CeilToInt((x1 - x0) / RES) + 1;
        int nz = Mathf.CeilToInt((z1 - z0) / RES) + 1;

        // ── výškové pole ────────────────────────────────────────────────────
        var grid = new Vector3[nx, nz];
        for (int i = 0; i < nx; i++)
        {
            for (int j = 0; j < nz; j++)
            {
                float wx = x0 + i * RES;
                float wz = z0 + j * RES;

                float dist = NearestLandDist(land, wx, wz); // 0 = uvnitř pevniny
                float t = Mathf.Clamp01((BEACH - dist) / BEACH);
                t = t * t * (3f - 2f * t); // smoothstep → měkký přechod

                float h = Mathf.Lerp(DEEP_Y, LAND_Y, t);

                // Lehké vlnky jen tam, kde je souš (ať pláž zůstane hladká).
                if (t > 0.65f)
                    h += (Mathf.PerlinNoise(wx * 0.55f + 11.3f, wz * 0.55f + 4.7f) - 0.5f) * 0.14f;

                grid[i, j] = new Vector3(wx, h, wz);
            }
        }

        // ── triangulace (flat shading — vrcholy se nesdílí) ─────────────────
        var verts = new List<Vector3>();
        var tris  = new List<int>();

        for (int i = 0; i < nx - 1; i++)
        {
            for (int j = 0; j < nz - 1; j++)
            {
                Vector3 a = grid[i, j];
                Vector3 b = grid[i + 1, j];
                Vector3 c = grid[i, j + 1];
                Vector3 d = grid[i + 1, j + 1];

                // Celý čtverec hluboko pod vodou → přeskoč.
                if (a.y <= DEEP_Y + 0.03f && b.y <= DEEP_Y + 0.03f &&
                    c.y <= DEEP_Y + 0.03f && d.y <= DEEP_Y + 0.03f)
                    continue;

                AddTri(verts, tris, a, c, b);
                AddTri(verts, tris, b, c, d);
            }
        }

        var mesh = new Mesh { name = "IslandTerrain" };
        if (verts.Count > 65000)
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddTri(List<Vector3> verts, List<int> tris, Vector3 a, Vector3 b, Vector3 c)
    {
        int i = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
    }

    // Vzdálenost bodu [wx,wz] k nejbližšímu pevninovému políčku (0 = uvnitř).
    private static float NearestLandDist(HashSet<Vector2Int> land, float wx, float wz)
    {
        int cx = Mathf.FloorToInt(wx);
        int cz = Mathf.FloorToInt(wz);
        float best = float.MaxValue;

        for (int dx = -3; dx <= 3; dx++)
        {
            for (int dy = -3; dy <= 3; dy++)
            {
                var cell = new Vector2Int(cx + dx, cz + dy);
                if (!land.Contains(cell)) continue;

                float px = Mathf.Clamp(wx, cell.x, cell.x + 1f);
                float pz = Mathf.Clamp(wz, cell.y, cell.y + 1f);
                float d = Mathf.Sqrt((wx - px) * (wx - px) + (wz - pz) * (wz - pz));
                if (d < best) best = d;
            }
        }
        return best == float.MaxValue ? 99f : best;
    }
}
