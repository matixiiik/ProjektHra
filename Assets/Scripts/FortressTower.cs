using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  FortressTower.cs
//  Strážní věž v rohu hradeb Pirátského ostrova. Je složená ze tří dílů Kenney
//  Pirate Kitu (spodek, prostředek, vršek — na věžích s dělem cimbuří, jinde
//  strážní věžička se stříškou), takže se dá ROZBÍT: po několika zásazích
//  dělovou koulí se díly odlomí, popadají a věž se zřítí (zůstane jen hromada
//  sutin). Dělo, které na věži stálo, se zřítí s ní.
//
//  Zbořené věže se pamatují v GameData.fortressTowersDown (bit = pořadí věže),
//  ať po načtení hry nevyrostou znovu. Věž (i sutiny) blokuje chůzi i střely
//  nepřátel; hráčovy koule ji poškozují (jen dělové, ne pěší zbraň — viz heavy).
//
//  Staví je MegaIslandMarker.BuildWalls. Seznam All udržuje OnEnable/OnDisable
//  (stejný vzor jako PirateShip.All) — čte ho AimAssist, střely a chůze.
// ─────────────────────────────────────────────────────────────────────────────

public class FortressTower : MonoBehaviour
{
    public const float MAX_HP        = 6f;    // velká loď = 3 zásahy, střední = 6
    public const float RADIUS        = 0.85f; // poloměr věže (blokuje chůzi i střely)
    public const float CANNON_HEIGHT = 3.02f; // podlaha horního dílu s cimbuřím (změřeno z modelu: 2 + 2,02 × 0,5)
    private const float SCALE        = 0.5f;  // stejné měřítko jako hradby (MegaIslandMarker.WALL_SCALE)
    private const float HEIGHT       = 3.4f;  // celková výška (3 díly při měřítku 0,5)

    private static readonly List<FortressTower> all = new List<FortressTower>();
    public static IReadOnlyList<FortressTower> All => all;

    public int  Index { get; private set; }   // pořadí věže (bit v GameData.fortressTowersDown)
    public bool Standing { get; private set; } = true;
    public HostileIslandCannon Cannon;        // dělo na věži (může být null)
    public bool HasLiveCannon => Cannon != null;

    /// <summary>Bod, kam míří AimAssist (zhruba do půlky výšky věže).</summary>
    public Vector3 AimPoint => transform.position + Vector3.up * 1.4f;

    private float hp = MAX_HP;
    private readonly List<Transform> pieces = new List<Transform>(); // díly odspodu nahoru

    void OnEnable()  { all.Add(this); }
    void OnDisable() { all.Remove(this); }

    /// <summary>Postaví věž. `ruined` = věž už byla dřív zbořená → jen sutiny.</summary>
    public static FortressTower Spawn(int index, Vector3 pos, float yRotation, bool watchTop, bool ruined, Transform parent, Material mat)
    {
        var go = new GameObject("FortressTower" + index);
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, yRotation, 0f);

        var t = go.AddComponent<FortressTower>();
        t.Index = index;
        if (ruined) { t.Standing = false; t.BuildRubble(mat); return t; }

        // Tři díly na sobě: spodek (1 j), prostředek (1 j), vršek (1,4 j).
        t.AddPiece("tower-base",   0f, mat);
        t.AddPiece("tower-middle", 1f, mat);
        t.AddPiece(watchTop ? "tower-watch" : "tower-top", 2f, mat);
        return t;
    }

    private void AddPiece(string model, float y, Material mat)
    {
        var prefab = Resources.Load<GameObject>("PirateKit/" + model);
        GameObject go;
        if (prefab != null)
        {
            go = Instantiate(prefab, transform, false);
            go.transform.localScale = Vector3.one * SCALE;
            if (mat != null) foreach (var mr in go.GetComponentsInChildren<MeshRenderer>()) mr.sharedMaterial = mat;
        }
        else
        {
            // Náhrada z primitiv, kdyby model v Resources chyběl.
            go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(1.5f, 0.5f, 1.5f);
            y += 0.5f;
        }
        go.name = model;
        go.transform.localPosition = new Vector3(0f, y, 0f);
        foreach (var col in go.GetComponentsInChildren<Collider>()) Destroy(col);
        pieces.Add(go.transform);
    }

    // Sutiny: spodní díl "zapadlý" do země a nakloněný + pár kamenů kolem.
    private void BuildRubble(Material mat)
    {
        AddPiece("tower-base", -0.55f, mat);
        if (pieces.Count > 0) pieces[0].localRotation = Quaternion.Euler(8f, 20f, -6f);

        Material stone = mat;
        for (int i = 0; i < 5; i++)
        {
            var rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(rock.GetComponent<Collider>());
            rock.name = "Rubble";
            rock.transform.SetParent(transform, false);
            float a = i * 72f * Mathf.Deg2Rad;
            rock.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.75f, 0.08f, Mathf.Sin(a) * 0.75f);
            rock.transform.localScale    = Vector3.one * (0.22f + 0.06f * (i % 3));
            rock.transform.localRotation = Quaternion.Euler(i * 23f, i * 47f, i * 11f);
            var r = rock.GetComponent<MeshRenderer>();
            r.sharedMaterial = RubbleMaterial();
        }
    }

    private static Material rubbleMat;
    private static Material RubbleMaterial()
    {
        if (rubbleMat != null) return rubbleMat;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        rubbleMat = new Material(sh);
        Color c = new Color(0.55f, 0.50f, 0.46f);
        if (rubbleMat.HasProperty("_BaseColor")) rubbleMat.SetColor("_BaseColor", c);
        if (rubbleMat.HasProperty("_Color"))     rubbleMat.SetColor("_Color", c);
        return rubbleMat;
    }

    // ── Zásahy ───────────────────────────────────────────────────────────────

    /// <summary>Stojí v bodě `p` (střela) něco z věže? Stojící věž do své výšky, sutiny jen nízko.</summary>
    public bool Contains(Vector3 p)
    {
        float dx = p.x - transform.position.x, dz = p.z - transform.position.z;
        if (dx * dx + dz * dz > RADIUS * RADIUS) return false;
        return p.y <= (Standing ? HEIGHT : 0.5f);
    }

    /// <summary>Hráčova dělová koule trefila věž. Když na ní stojí dělo a koule letí
    /// v jeho výšce, dostane zásah dělo; jinak věž. Pěší zbraň (heavy = false) kámen nerozbije.</summary>
    public void TakeHit(Vector3 hitPoint, float damage, bool heavy)
    {
        if (!Standing) return;

        if (Cannon != null && hitPoint.y >= CANNON_HEIGHT - 0.6f)
        {
            Cannon.TakeHit(damage);
            return;
        }
        if (!heavy) return;

        hp -= damage;
        SoundManager.PlayHit();
        StartCoroutine(Shake());
        if (hp <= 0f) Collapse();
    }

    // Krátké zatřesení po zásahu — hráč vidí, že věž dostala ránu.
    private IEnumerator Shake()
    {
        Vector3 basePos = transform.position;
        for (float t = 0f; t < 0.25f; t += Time.deltaTime)
        {
            float k = 1f - t / 0.25f;
            transform.position = basePos + new Vector3(Mathf.Sin(t * 90f) * 0.05f * k, 0f, Mathf.Cos(t * 77f) * 0.05f * k);
            yield return null;
        }
        transform.position = basePos;
    }

    // Zřícení: dělo spadne s věží, díly se rozletí a zapadnou, zůstanou sutiny.
    private void Collapse()
    {
        Standing = false;
        if (Cannon != null) { Cannon.TakeHit(999f); Cannon = null; } // dělo se zřítí s věží (odměna jako za dělo)

        var grid = FindFirstObjectByType<GridManager>();
        if (grid != null)
        {
            grid.gameData.fortressTowersDown |= 1 << Index;
            grid.Save();
            grid.NotifyWorldChanged();
        }
        SoundManager.PlaySink();
        SoundManager.PlayThunder(0.6f); // hluboké dunění padajícího kamene
        if (CombatDirector.Instance != null)
            CombatDirector.Instance.Toast(Loc.T("Věž se zřítila!", "The tower collapses!"));
        SpawnDust();
        StartCoroutine(FallApart());
    }

    private IEnumerator FallApart()
    {
        // Každý díl dostane vlastní směr pádu — horní padají dál a rychleji.
        var dirs = new Vector3[pieces.Count];
        for (int i = 0; i < pieces.Count; i++)
        {
            float a = (Index * 97 + i * 131) % 360 * Mathf.Deg2Rad;
            dirs[i] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
        }

        const float DURATION = 1.4f;
        for (float t = 0f; t < DURATION; t += Time.deltaTime)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i] == null) continue;
                float k = i + 1;
                pieces[i].localPosition += (dirs[i] * 0.6f * k + Vector3.down * (1.2f + t * 4f)) * Time.deltaTime;
                pieces[i].localRotation *= Quaternion.Euler(dirs[i].z * 60f * k * Time.deltaTime, 0f, -dirs[i].x * 60f * k * Time.deltaTime);
            }
            yield return null;
        }

        // Úklid: díly pryč, místo nich sutiny (stejné jako po načtení hry).
        Material mat = null;
        if (pieces.Count > 0 && pieces[0] != null)
        {
            var mr = pieces[0].GetComponentInChildren<MeshRenderer>();
            if (mr != null) mat = mr.sharedMaterial;
        }
        foreach (var p in pieces) if (p != null) Destroy(p.gameObject);
        pieces.Clear();
        BuildRubble(mat);
    }

    // Oblak prachu: krátký výbuch šedých částic.
    private void SpawnDust()
    {
        var go = new GameObject("TowerDust");
        go.transform.position = transform.position + Vector3.up * 1.5f;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop = false;
        main.duration        = 0.5f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1.5f, 4f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        main.startColor      = new Color(0.62f, 0.58f, 0.52f, 0.7f);
        main.gravityModifier = 0.25f;
        main.stopAction      = ParticleSystemStopAction.Destroy; // po dohrání se objekt sám smaže

        var em = ps.emission;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 45) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = 1.0f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = grad;

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = DustMaterial();
        ps.Play();
    }

    // Měkký kulatý "obláček" (bílý střed, průhledné okraje) — ať prach nejsou hranaté čtverečky.
    private static Texture2D SoftPuffTexture()
    {
        const int S = 32;
        var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                float dx = (x - S / 2f + 0.5f) / (S / 2f), dy = (y - S / 2f + 0.5f) / (S / 2f);
                float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        tex.Apply();
        return tex;
    }

    private static Material dustMat;
    private static Material DustMaterial()
    {
        if (dustMat != null) return dustMat;
        Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Transparent");
        dustMat = new Material(sh);
        if (dustMat.HasProperty("_BaseColor")) dustMat.SetColor("_BaseColor", new Color(0.62f, 0.58f, 0.52f, 0.55f));
        Texture2D puff = SoftPuffTexture();
        if (dustMat.HasProperty("_BaseMap")) dustMat.SetTexture("_BaseMap", puff);
        if (dustMat.HasProperty("_MainTex")) dustMat.SetTexture("_MainTex", puff);
        if (dustMat.HasProperty("_Surface"))  dustMat.SetFloat("_Surface", 1f);
        if (dustMat.HasProperty("_SrcBlend")) dustMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (dustMat.HasProperty("_DstBlend")) dustMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        if (dustMat.HasProperty("_ZWrite"))   dustMat.SetFloat("_ZWrite", 0f);
        dustMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        dustMat.renderQueue = 3050;
        return dustMat;
    }
}
