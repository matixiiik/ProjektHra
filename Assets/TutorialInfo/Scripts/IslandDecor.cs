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
    public float decorChance = 0.28f;

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
        Transform pick = decors[Random.Range(0, decors.Count)];
        pick.gameObject.SetActive(true);
        pick.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }
}
