using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  IslandDecor.cs
//  Ozdoba jedné ostrovní dlaždice (písek): kámen, palma nebo trs trávy.
//
//  Skript nic nerozhoduje náhodně — jen PŘEČTE, co má na dlaždici stát, z
//  uložených dat (TileStatus.decor / decorRot / decorScale / tileRot). O to,
//  které dlaždice ostrova dekoraci dostanou (5–8 na ostrov) a jakou, se stará
//  GridManager.AssignIslandDecor při vzniku ostrova. Data se ukládají do save,
//  takže ostrov vypadá po každém znovunačtení (návrat z majáku) STEJNĚ. Nová
//  hra vygeneruje ostrovy znovu.
//
//  Modely: vypnuté děti "Decor_*" v HarborPrefabu (palma, kámen, tráva) +
//  extra Kenney modely z Assets/Resources/IslandDecor/. Číslo modelu je
//  (decor - 2) % počet_modelů.
//
//  Skript je čistě vizuální — nemá vliv na hratelnost.
// ─────────────────────────────────────────────────────────────────────────────

public class IslandDecor : MonoBehaviour
{
    // Vestavěná palma "Decor_Palm" (v HarborPrefabu) je dost malá — zvětšíme ji,
    // ať je vůči majáku a panáčkovi věrohodnější (cca půl majáku).
    private const float PALM_SCALE = 2.1f;

    // Kenney model "palm-bend" má naopak velké nativní měřítko (bez úpravy je
    // vyšší než celý ostrov) — pro něj platí vlastní, mnohem menší měřítko.
    private const float EXTRA_PALM_SCALE = 0.38f;

    // ── Extra Kenney modely (načtou se jednou pro celou hru) ────────────────
    private struct ExtraDecor { public GameObject prefab; public float scale; public bool isPalm; }
    private static List<ExtraDecor> extras;
    private static bool             extrasLoaded;

    private static void LoadExtras()
    {
        extrasLoaded = true;
        extras = new List<ExtraDecor>();

        // (jméno souboru v Resources/IslandDecor, měřítko, je to palma)
        // Pozn.: kameny a ohnutá palma dostanou z pirátského atlasu (colormap)
        // rozumné barvy. Ploché "patch"/"grass" meshe mají jiné UV → braly by
        // špatné texely, proto je tu nepoužíváme.
        AddExtra("rocks-a",      0.42f, false);
        AddExtra("rocks-sand-b", 0.42f, false);
        AddExtra("palm-bend",    EXTRA_PALM_SCALE, true);
    }

    private static void AddExtra(string name, float scale, bool isPalm)
    {
        var go = Resources.Load<GameObject>("IslandDecor/" + name);
        if (go != null) extras.Add(new ExtraDecor { prefab = go, scale = scale, isPalm = isPalm });
    }

    void Awake()
    {
        if (!extrasLoaded) LoadExtras();

        // Posbírej vestavěné děti "Decor_..." a všechny vypni. Pořadí je stabilní
        // (sourozenci v prefabu) → číslo modelu vždy odkazuje na to samé.
        var builtin = new List<Transform>();
        Material decorMat = null;
        foreach (Transform child in transform)
            if (child.name.StartsWith("Decor_"))
            {
                child.gameObject.SetActive(false);
                builtin.Add(child);
                if (decorMat == null)
                {
                    var mr = child.GetComponentInChildren<MeshRenderer>(true);
                    if (mr != null) decorMat = mr.sharedMaterial; // PirateColormap
                }
            }

        // Přečti uložený stav téhle dlaždice.
        int gx = Mathf.RoundToInt(transform.position.x);
        int gy = Mathf.RoundToInt(transform.position.z);

        var data = GameSession.Instance != null ? GameSession.Instance.Data : null;
        if (data == null || !data.tileData.TryGetValue(gx + "," + gy, out var st)) return;

        // Natočení celé dlaždice (0/90/180/270), ať se mřížka písku neprozradí.
        transform.rotation = Quaternion.Euler(0f, st.tileRot * 90f, 0f);

        // decor: 0 = neurčeno (nemělo by nastat), 1 = záměrně bez dekorace.
        if (st.decor < 2) return;

        int total = builtin.Count + (extras != null ? extras.Count : 0);
        if (total == 0) return;

        int   index  = (st.decor - 2) % total;
        float rotDeg = st.decorRot;
        float scale  = (st.decorScale <= 0 ? 100 : st.decorScale) / 100f; // 0.80 .. 1.25

        Place(index, builtin, decorMat, rotDeg, scale);
    }

    // Postaví dekoraci s daným indexem (nejdřív vestavěné "Decor_*", pak extra Kenney).
    private void Place(int index, List<Transform> builtin, Material decorMat, float rotDeg, float scale)
    {
        if (index < builtin.Count)
        {
            var t = builtin[index];
            t.gameObject.SetActive(true);
            t.localRotation = Quaternion.Euler(0f, rotDeg, 0f);

            bool isPalm = t.name.Contains("Palm");
            float sc = isPalm ? PALM_SCALE : scale;
            t.localScale = Vector3.Scale(t.localScale, new Vector3(sc, sc, sc));
        }
        else
        {
            var ex = extras[index - builtin.Count];
            var go = Instantiate(ex.prefab, transform);
            go.name = "DecorExtra";
            go.transform.localPosition = new Vector3(0f, 0.02f, 0f);
            go.transform.localRotation = Quaternion.Euler(0f, rotDeg, 0f);

            float sc = ex.scale * (ex.isPalm ? 1f : scale);
            go.transform.localScale = new Vector3(sc, sc, sc);

            // Kenney fbx nemá materiál → dej mu ten z vestavěné dekorace.
            if (decorMat != null)
                foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
                    mr.sharedMaterial = decorMat;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);
        }
    }
}
