using System.Collections;
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
//  Stejně tak kolem ostrovů se dno zvedne až k jejich úpatí, aby ostrov
//  "vyrůstal ze dna" a nekončil pod vodou uříznutý.
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
    private const int   SCAN_RADIUS  = 44;    // v kolika políčkách kolem hledat vraky/ostrovy

    // Stejná hloubka jako konec pláže (IslandTerrain.DEEP_Y) — jinak se dno u ostrova
    // potkává s koncem pláže v jiné výšce a je vidět ostrý schod/hrana (ověřeno v Play,
    // viz .claude/story-plan.md fáze 5). SeaFloor na rozdíl od IslandTerrain nemá
    // žádného rodiče s posunem v ose Y, takže si IslandTerrain.Y_OFFSET musí připočítat
    // sám — jinak by zůstal drobný (0,1 j) zbytkový schod.
    private const float SHELF_Y       = IslandTerrain.DEEP_Y + IslandTerrain.Y_OFFSET;
    private const float SHELF_CORE    = 2.5f;  // do téhle vzdálenosti od pevniny je dno rovnou na SHELF_Y (síť dna je hrubá)
    private const float SHELF_RADIUS  = 15f;   // za jádrem se dno lineárně svažuje zpátky do hloubky

    // Přepočet dna je drahý (tisíce vrcholů × okolní ostrovy a vraky), proto se
    // nedělá naráz v jednom snímku: sběr políček i výška vrcholů se počítají po
    // částech, po jednom kousku za snímek (coroutine Rebuild). Hotové dno se
    // pak použije najednou. Dřív to byl jeden zásek kolem 40 ms při každém
    // posunu hráče o STEP a ještě jednou za sekundu.
    private const int   COLUMNS_PER_FRAME = 12;   // sloupců políček prohledaných za snímek
    private const int   VERTS_PER_FRAME   = 400;  // vrcholů dna přepočítaných za snímek
    private const float RESCAN_SECONDS    = 1f;   // jak často se dno přepočítá, i když hráč stojí

    private Transform   p1;
    private Transform   p2;
    private float       nextP2Scan;
    private float       nextRescan;      // občas přepočítat dno, i když hráč stojí (mohl se dogenerovat nový vrak)
    private bool        building;        // právě běží Rebuild
    private GridManager grid;

    private Mesh       mesh;
    private Vector3[]  verts;
    private float[]    heights;          // nově spočtené výšky (použijí se až všechny naráz)
    private Vector2Int appliedSnap = new Vector2Int(int.MaxValue, int.MaxValue);

    private readonly List<Vector2Int>    wrecks  = new List<Vector2Int>();
    private readonly List<Vector2Int>    land    = new List<Vector2Int>();   // všechna pevninová políčka v okolí
    private readonly List<Vector2Int>    shore   = new List<Vector2Int>();   // jen pobřežní (mají souseda, který není pevnina)
    private readonly HashSet<Vector2Int> landSet = new HashSet<Vector2Int>();

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

        // První dno hned a celé (ještě nic nevidíme, na zásek nikdo nečeká).
        var first = Rebuild(CurrentSnap());
        while (first.MoveNext()) { }
    }

    void BuildMesh()
    {
        int n = Mathf.RoundToInt(SIZE / STEP) + 1;
        verts   = new Vector3[n * n];
        heights = new float[n * n];
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

    void OnDisable()
    {
        building = false; // vypnutí zastaví coroutinu, ať příznak nezůstane viset
    }

    void LateUpdate()
    {
        if (p2 == null && MultiplayerManager.IsMultiplayer && Time.time >= nextP2Scan)
        {
            nextP2Scan = Time.time + 0.5f;
            foreach (var pc in PlayerController.All)
                if (pc.playerIndex == 1) { p2 = pc.transform; break; }
        }

        if (building) return; // předchozí přepočet ještě dobíhá

        // Přepočítat, když se hráč posunul o další políčko sítě dna — nebo občas i
        // když stojí (mohl se opodál dogenerovat nový vrak).
        Vector2Int target = CurrentSnap();
        if (target != appliedSnap || Time.time >= nextRescan)
            StartCoroutine(Rebuild(target));
    }

    // Kam patří střed dna: průměr hráčů, zaokrouhlený na mřížku sítě (STEP).
    Vector2Int CurrentSnap()
    {
        Vector3 c;
        if (p1 != null && p2 != null) c = (p1.position + p2.position) * 0.5f;
        else if (p1 != null)          c = p1.position;
        else                          c = transform.position;

        return new Vector2Int(Mathf.RoundToInt(c.x / STEP), Mathf.RoundToInt(c.z / STEP));
    }

    // Přepočítá dno kolem daného středu po částech (jeden kousek za snímek) a
    // teprve když je celé hotové, přesune plochu a nahraje nové výšky.
    IEnumerator Rebuild(Vector2Int snap)
    {
        building = true;
        float ox = snap.x * STEP;
        float oz = snap.y * STEP;
        int   cx = Mathf.RoundToInt(ox);
        int   cz = Mathf.RoundToInt(oz);

        // 1) Vraky a pevnina v okolí (po několika sloupcích za snímek).
        wrecks.Clear();
        land.Clear();
        if (grid != null)
        {
            for (int x = cx - SCAN_RADIUS; x <= cx + SCAN_RADIUS; x += COLUMNS_PER_FRAME)
            {
                int xTo = Mathf.Min(x + COLUMNS_PER_FRAME - 1, cx + SCAN_RADIUS);
                grid.CollectFloorFeatures(x, xTo, cz, SCAN_RADIUS, wrecks, land);
                yield return null;
            }
        }
        FindShoreTiles();

        // 2) Výška vrcholů (po kouscích).
        for (int start = 0; start < verts.Length; start += VERTS_PER_FRAME)
        {
            int end = Mathf.Min(start + VERTS_PER_FRAME, verts.Length);
            for (int k = start; k < end; k++)
                heights[k] = FloorHeight(verts[k].x + ox, verts[k].z + oz);
            yield return null;
        }

        // 3) Hotovo → použít naráz (žádné poloviční dno).
        for (int k = 0; k < verts.Length; k++) verts[k].y = heights[k];
        transform.position = new Vector3(ox, 0f, oz);
        mesh.vertices = verts;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        appliedSnap = snap;
        nextRescan  = Time.time + RESCAN_SECONDS;
        building    = false;
    }

    // Z pevninových políček nechá jen pobřežní. Nejbližší pevnina ke vrcholu
    // dna ležícímu ve vodě je vždycky pobřežní políčko, vnitřní políčka se
    // tedy dají vynechat — ostrov má stovky políček, pobřeží jen desítky.
    void FindShoreTiles()
    {
        landSet.Clear();
        foreach (var t in land) landSet.Add(t);

        shore.Clear();
        foreach (var t in land)
        {
            if (!landSet.Contains(t + Vector2Int.right) || !landSet.Contains(t + Vector2Int.left)
             || !landSet.Contains(t + Vector2Int.up)    || !landSet.Contains(t + Vector2Int.down))
                shore.Add(t);
        }
    }

    // Výška dna ve světovém bodě: Perlinův šum + mělčina pod nejbližším vrakem
    // + svah k úpatí nejbližšího ostrova. Obojí závisí jen na vzdálenosti k
    // NEJBLIŽŠÍMU vraku / pobřeží, stačí tedy najít nejmenší vzdálenost (bez
    // odmocniny pro každou dvojici).
    float FloorHeight(float wx, float wz)
    {
        float noise  = Mathf.PerlinNoise(wx * NOISE + 500f, wz * NOISE + 500f);
        float floorY = Mathf.Lerp(FLOOR_MIN, FLOOR_MAX, noise * noise);

        // Nejbližší vrak → plynulý nájezd dna vzhůru (přírodní mělčina).
        float nearestWreck = float.MaxValue;
        for (int t = 0; t < wrecks.Count; t++)
        {
            float dx = wx - wrecks[t].x;
            float dz = wz - wrecks[t].y;
            float sq = dx * dx + dz * dz;
            if (sq < nearestWreck) nearestWreck = sq;
        }
        if (nearestWreck < SHOAL_RADIUS * SHOAL_RADIUS)
        {
            float s = Mathf.Clamp01(1f - Mathf.Sqrt(nearestWreck) / SHOAL_RADIUS);
            s = s * s * (3f - 2f * s); // smoothstep
            floorY = Mathf.Max(floorY, Mathf.Lerp(floorY, SHOAL_Y, s));
        }

        // Okolí ostrova → dno se plynule zvedne až k jeho úpatí (SHELF_Y),
        // takže ostrov "vyrůstá ze dna" a nekončí pod vodou uříznutý. Falloff
        // je lineární (kužel/svah), ne plochá deska — a dál se přes Max()
        // vrací k náhodnému Perlinovu dnu.
        float nearestShore = float.MaxValue;
        for (int t = 0; t < shore.Count; t++)
        {
            float dx = wx - (shore[t].x + 0.5f);
            float dz = wz - (shore[t].y + 0.5f);
            float sq = dx * dx + dz * dz;
            if (sq < nearestShore) nearestShore = sq;
        }
        float reach = SHELF_CORE + SHELF_RADIUS;
        if (nearestShore < reach * reach)
        {
            float d = Mathf.Sqrt(nearestShore);
            float s = Mathf.Clamp01(1f - Mathf.Max(0f, d - SHELF_CORE) / SHELF_RADIUS);
            floorY = Mathf.Max(floorY, Mathf.Lerp(FLOOR_MIN, SHELF_Y, s));
        }

        return floorY;
    }
}
