using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  MegaIslandMarker.cs
//  Střed příběhového "mega ostrova" — zatím jen kamenný obelisk s teplým
//  přísvitem, ať je poznat, že jde o zvláštní místo. Vytváří ho
//  GridManager.PlaceMegaIsland (a znovu po načtení save).
//
//  Tady se budou později přidávat věci, které na mega ostrově musí hráč splnit
//  (zadá je uživatel). Zatím je to čistě orientační bod.
// ─────────────────────────────────────────────────────────────────────────────

public class MegaIslandMarker : MonoBehaviour
{
    void Start()
    {
        Material stone = MakeMat(new Color(0.42f, 0.40f, 0.38f));
        Material glow  = MakeMat(new Color(0.55f, 0.85f, 0.95f));
        if (glow.HasProperty("_EmissionColor"))
        {
            glow.EnableKeyword("_EMISSION");
            glow.SetColor("_EmissionColor", new Color(0.35f, 0.7f, 0.95f) * 2f);
        }

        // Stupňovitý podstavec + obelisk.
        Box("Base",   new Vector3(0f, 0.15f, 0f), new Vector3(3.0f, 0.3f, 3.0f), stone);
        Box("Base2",  new Vector3(0f, 0.45f, 0f), new Vector3(2.2f, 0.3f, 2.2f), stone);
        Box("Shaft",  new Vector3(0f, 2.4f, 0f),  new Vector3(0.8f, 3.6f, 0.8f), stone);
        Box("Cap",    new Vector3(0f, 4.4f, 0f),  new Vector3(0.5f, 0.5f, 0.5f), glow);

        var lightGo = new GameObject("MarkerLight");
        lightGo.transform.SetParent(transform, false);
        lightGo.transform.localPosition = new Vector3(0f, 4.4f, 0f);
        var l = lightGo.AddComponent<Light>();
        l.type      = LightType.Point;
        l.color     = new Color(0.55f, 0.85f, 0.95f);
        l.range     = 14f;
        l.intensity = 2.5f;
    }

    private void Box(string name, Vector3 localPos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localScale    = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
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
