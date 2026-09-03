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
            selected.SetActive(true);

        // Řekni PlayerControlleru, který objekt je teď jeho loď (kvůli otáčení).
        if (player != null && selected != null)
            player.boatModel = selected.transform;
    }
}
