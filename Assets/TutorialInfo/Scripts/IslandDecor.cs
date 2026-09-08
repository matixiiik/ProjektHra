using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  IslandDecor.cs
//  Malá "ozdoba" ostrovní dlaždice (písek). V Awake náhodně zapne jednu
//  z podřízených dekorací (palma / kámen / nic) a náhodně ji pootočí,
//  aby ostrovy nevypadaly jako mřížka stejných čtverců.
//
//  Dekorace jsou pod objektem jako vypnuté děti pojmenované "Decor_*".
//  Skript je čistě vizuální — nemá žádný vliv na hratelnost (chození,
//  kolize, generování). Kdyby na dlaždici stálo molo/obchod, GridManager
//  na ni tenhle prefab vůbec nedá.
// ─────────────────────────────────────────────────────────────────────────────

public class IslandDecor : MonoBehaviour
{
    [Range(0f, 1f)]
    [Tooltip("Šance, že na dlaždici vyroste nějaká dekorace.")]
    public float decorChance = 0.5f;

    [Tooltip("Šance, že se přidá i druhá (menší) dekorace navrch.")]
    public float secondDecorChance = 0.25f;

    // Palmy jsou dost malé — zvětšíme je, ať jsou vůči majáku a panáčkovi
    // věrohodnější (cca půl majáku).
    private const float PALM_SCALE = 2.1f;

    void Awake()
    {
        // Posbírej děti pojmenované "Decor_..." a všechny je pro jistotu vypni.
        var decors = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in transform)
            if (child.name.StartsWith("Decor_"))
            {
                child.gameObject.SetActive(false);
                decors.Add(child);
            }

        if (decors.Count == 0) return;

        // Náhodné pootočení celé dlaždice kolem svislé osy (0/90/180/270),
        // ať se opakující se textury písku tolik neprozradí.
        transform.rotation = Quaternion.Euler(0f, Random.Range(0, 4) * 90f, 0f);

        if (Random.value > decorChance) return; // dlaždice zůstane holá

        // Zapni jednu náhodnou dekoraci a dej jí vlastní náhodné natočení.
        ShowDecor(decors[Random.Range(0, decors.Count)], center: true);

        // Občas přidej i druhou dekoraci (menší, trochu odsazenou), ať jsou
        // ostrovy hustší a živější.
        if (decors.Count > 1 && Random.value < secondDecorChance)
        {
            Transform second = decors[Random.Range(0, decors.Count)];
            if (!second.gameObject.activeSelf) ShowDecor(second, center: false);
        }
    }

    private void ShowDecor(Transform pick, bool center)
    {
        pick.gameObject.SetActive(true);
        pick.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        // Palma se zvětší; ostatní dekorace lehce zvariují velikost.
        bool isPalm = pick.name.Contains("Palm");
        float baseScale = isPalm ? PALM_SCALE : Random.Range(0.85f, 1.25f);
        pick.localScale = Vector3.Scale(pick.localScale, new Vector3(baseScale, baseScale, baseScale));

        // Druhá dekorace se odsadí ke kraji dlaždice, ať nestojí přesně na první.
        if (!center)
        {
            Vector3 off = new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));
            pick.localPosition += off;
            if (!isPalm) pick.localScale *= 0.7f; // menší
        }
    }
}
