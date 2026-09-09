using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  IslandDecor.cs
//  Ozdoba jedné ostrovní dlaždice (písek). V Awake náhodně vybere jednu (občas
//  dvě) dekorace a pootočí je, aby ostrovy nevypadaly jako mřížka stejných
//  čtverců.
//
//  Základní dekorace jsou pod objektem jako vypnuté děti "Decor_*" (palma,
//  kámen, tráva — nastavené v HarborPrefabu). Navíc se ZA BĚHU přidávají další
//  Kenney modely z Assets/Resources/IslandDecor/ (kameny, trsy trávy, ohnutá
//  palma, záplaty s křovím), aby byly ostrovy pestřejší. Materiál se jim vezme
//  z existující "Decor_*" dlaždice (Kenney fbx žádný nemá).
//
//  Skript je čistě vizuální — nemá vliv na hratelnost.
//
//  Náhoda je SEEDOVANÁ podle souřadnic dlaždice + herního seedu
//  (GameData.worldSeed): stejná dlaždice vypadá po každém znovuvytvoření
//  (návrat z majáku, načtení hry) identicky, ale nová hra (nový worldSeed)
//  vygeneruje ostrovy jinak.
// ─────────────────────────────────────────────────────────────────────────────

public class IslandDecor : MonoBehaviour
{
    [Range(0f, 1f)]
    [Tooltip("Šance na dekoraci na 'povolené' dlaždici (šachovnicově každá druhá, " +
             "ať dekorace nikdy nestojí těsně u sebe). Každý ostrov si ji navíc " +
             "trochu posune nahoru/dolů, aby nebyly všechny stejně husté.")]
    public float decorChance = 0.30f;

    [Tooltip("Šance, že se přidá i druhá (menší) dekorace navrch.")]
    public float secondDecorChance = 0f;

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
        // rozumné barvy. Ploché "patch" a "grass" meshe jsou z jiného Kenney
        // balíčku, mají jiné UV → z pirátského atlasu by braly špatné (červené)
        // texely, proto je tu nepoužíváme.
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

        // Posbírej vestavěné děti "Decor_..." a všechny vypni.
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

        // Dekorace jen na "šachovnicově každé druhé" dlaždici → nikdy nestojí dvě
        // těsně vedle sebe (řeší přehuštění na malých ostrovech).
        int gx = Mathf.RoundToInt(transform.position.x);
        int gy = Mathf.RoundToInt(transform.position.z);
        if (((gx + gy) & 1) != 0) return; // "sudá" dlaždice zůstane holá

        // ── Deterministický náhodný generátor jen pro tuhle dlaždici ─────────
        // Seed = souřadnice dlaždice + herní seed. Díky tomu vypadá ostrov po
        // každém znovuvytvoření stejně. Po dokončení se globální RNG vrátí zpět,
        // ať se neovlivní zbytek hry.
        var prevRandom = Random.state;
        Random.InitState(TileDecorSeed(gx, gy));
        try
        {
            // Náhodné pootočení celé dlaždice (0/90/180/270), ať se textura
            // písku tolik neprozradí.
            transform.rotation = Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f);

            // Každý ostrov má vlastní "hustotu" dekorace odvozenou z jeho hrubé
            // pozice (ostrovy vznikají po 40 políčkách) — některé jsou skoro
            // holé, jiné o něco zarostlejší, ať nevypadají všechny stejně.
            int islandSeed = Mathf.RoundToInt(transform.position.x / 40f) * 73856093
                           ^ Mathf.RoundToInt(transform.position.z / 40f) * 19349663;
            float islandBias = -0.06f + ((islandSeed & 0xFFFF) / 65535f) * 0.14f; // -0.06 .. +0.08
            float chance = Mathf.Clamp01(decorChance + islandBias);

            if (Random.value > chance) return; // dlaždice zůstane holá

            PlaceOne(builtin, decorMat, center: true);
            if (Random.value < secondDecorChance)
                PlaceOne(builtin, decorMat, center: false);
        }
        finally
        {
            Random.state = prevRandom;
        }
    }

    // Deterministický seed pro dekoraci dlaždice [gx,gy] — kombinuje herní seed
    // (jiný pro každou novou hru) se souřadnicemi dlaždice.
    private static int TileDecorSeed(int gx, int gy)
    {
        int world = GameSession.Instance != null && GameSession.Instance.Data != null
            ? GameSession.Instance.Data.worldSeed : 0;
        unchecked
        {
            int h = world * 668265263;
            h = (h ^ gx) * 73856093;
            h = (h ^ gy) * 19349663;
            return h;
        }
    }

    // Vybere náhodně z vestavěných + extra modelů a jednu dekoraci na dlaždici položí.
    private void PlaceOne(List<Transform> builtin, Material decorMat, bool center)
    {
        int total = builtin.Count + (extras != null ? extras.Count : 0);
        if (total == 0) return;

        int pick = Random.Range(0, total);
        Vector3 offset = center
            ? Vector3.zero
            : new Vector3(Random.Range(-0.32f, 0.32f), 0f, Random.Range(-0.32f, 0.32f));

        if (pick < builtin.Count)
        {
            var t = builtin[pick];
            if (t.gameObject.activeSelf) return; // už je použitá (druhá dekorace)
            t.gameObject.SetActive(true);
            t.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            t.localPosition += offset;

            bool isPalm = t.name.Contains("Palm");
            float sc = isPalm ? PALM_SCALE : Random.Range(0.85f, 1.25f);
            if (!center && !isPalm) sc *= 0.7f;
            t.localScale = Vector3.Scale(t.localScale, new Vector3(sc, sc, sc));
        }
        else
        {
            var ex = extras[pick - builtin.Count];
            var go = Instantiate(ex.prefab, transform);
            go.name = "DecorExtra";
            go.transform.localPosition = new Vector3(offset.x, 0.02f, offset.z);
            go.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            float sc = ex.scale * (ex.isPalm ? 1f : Random.Range(0.85f, 1.2f));
            if (!center && !ex.isPalm) sc *= 0.7f;
            go.transform.localScale = new Vector3(sc, sc, sc);

            // Kenney fbx nemá materiál → dej mu ten z vestavěné dekorace.
            if (decorMat != null)
                foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
                    mr.sharedMaterial = decorMat;
            foreach (var col in go.GetComponentsInChildren<Collider>(true)) Destroy(col);
        }
    }
}
