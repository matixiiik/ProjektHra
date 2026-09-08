using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  ShipModelSwitcher.cs
//  Podle úrovně lodě (shipLevel 0/1/2) zapne správný 3D model lodě a zbylé dva
//  vypne. Když je hráč pěšky na ostrově, nechá vypnuté všechny.
//  Je na stejném objektu jako PlayerController (u P2 na jeho kopii).
// ─────────────────────────────────────────────────────────────────────────────

public class ShipModelSwitcher : MonoBehaviour
{
    public GameObject shipSmall;  // model pro úroveň 0
    public GameObject shipMedium; // model pro úroveň 1
    public GameObject shipLarge;  // model pro úroveň 2

    // O kolik posadit model lodě níž (pod objekt hráče), aby loď seděla v hladině
    // a nevypadala, že "lítá" nad vodou — hlavně při nakloněné kameře.
    // (Objekt hráče je ve výšce 0.5, hladina cca -0.22 → loď skončí kolem -0.15.)
    private const float BOAT_SINK = 0.62f;

    private GridManager      grid;
    private PlayerController player;

    void Start()
    {
        grid = FindFirstObjectByType<GridManager>();
        // Skript může být přímo na objektu hráče, nebo na jeho rodiči.
        player = GetComponent<PlayerController>() ?? GetComponentInParent<PlayerController>();
        Apply();
    }

    /// <summary>Zapne model odpovídající aktuální úrovni lodě daného hráče.</summary>
    public void Apply()
    {
        if (grid == null) return;

        // Který hráč jsme (P1 nebo P2) — každý má vlastní úroveň lodě a stav "pěšky".
        bool isP2   = player != null && player.playerIndex == 1;
        int  level  = isP2 ? grid.gameData.player2ShipLevel : grid.gameData.shipLevel;
        bool onFoot = player != null ? player.IsOnFoot : grid.gameData.isOnFoot;

        // Nejdřív vypni všechny lodě.
        if (shipSmall)  shipSmall.SetActive(false);
        if (shipMedium) shipMedium.SetActive(false);
        if (shipLarge)  shipLarge.SetActive(false);

        // Vyber model podle úrovně a zapni ho (jen když hráč není pěšky na ostrově).
        GameObject selected = level == 0 ? shipSmall
                            : level == 1 ? shipMedium
                            :              shipLarge;
        if (selected != null && !onFoot)
        {
            selected.SetActive(true);

            // Posaď model lodě níž k hladině (jinak "lítá" nad vodou).
            Vector3 lp = selected.transform.localPosition;
            selected.transform.localPosition = new Vector3(lp.x, -BOAT_SINK, lp.z);
        }

        // Řekni PlayerControlleru, který objekt je teď jeho loď (kvůli otáčení).
        if (player != null && selected != null)
            player.boatModel = selected.transform;
    }
}
