using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  ShipModelSwitcher.cs
//  Podle úrovně lodě (shipLevel 0/1/2/3) zapne správný 3D model lodě a ostatní
//  vypne. Když je hráč pěšky na ostrově, nechá vypnuté všechny.
//  Je na stejném objektu jako PlayerController (u P2 na jeho kopii).
//
//  Úrovně: 0 = veslice, 1 = malá plachetnice, 2 = střední loď, 3 = velká loď.
//  Co která úroveň umí (rychlost, rybaření, těžba) je v BoatStats.
// ─────────────────────────────────────────────────────────────────────────────

public class ShipModelSwitcher : MonoBehaviour
{
    public GameObject shipRow;    // model pro úroveň 0 (veslice — startovní loď)
    public GameObject shipSmall;  // model pro úroveň 1 (malá plachetnice)
    public GameObject shipMedium; // model pro úroveň 2 (střední loď)
    public GameObject shipLarge;  // model pro úroveň 3 (velká loď)

    // O kolik posadit model lodě níž (pod objekt hráče), aby kus trupu byl POD
    // hladinou a loď působila, že fakt pluje (ne že jen leží na vodě).
    // (Objekt hráče je ve výšce 0.5, hladina cca −0.22 → loď skončí kolem −0.43.)
    private const float BOAT_SINK = 0.93f;

    // Veslice je placatá loďka — sedí líp o kousek výš než plachetnice, ať je
    // víc na hladině (záporná hodnota = zvednout oproti BOAT_SINK).
    private const float ROW_SINK_ADJUST = -0.16f;

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
        bool isP2    = player != null && player.playerIndex == 1;
        int  level   = isP2 ? grid.gameData.player2ShipLevel  : grid.gameData.shipLevel;
        bool onFoot  = player != null ? player.IsOnFoot : grid.gameData.isOnFoot;
        bool wrecked = isP2 ? grid.gameData.player2BoatWrecked : grid.gameData.boatWrecked;
        bool hideBoat = onFoot || wrecked; // rozbitá loď se nezobrazuje (panáček plave)

        // Nejdřív vypni všechny lodě.
        if (shipRow)    shipRow.SetActive(false);
        if (shipSmall)  shipSmall.SetActive(false);
        if (shipMedium) shipMedium.SetActive(false);
        if (shipLarge)  shipLarge.SetActive(false);

        // Vyber model podle úrovně.
        GameObject selected = level <= 0 ? shipRow
                            : level == 1 ? shipSmall
                            : level == 2 ? shipMedium
                            :              shipLarge;
        // Pojistka pro staré savy / nezapojený model: spadni na malou plachetnici.
        if (selected == null) selected = shipSmall;

        if (selected != null)
        {
            // Posaď model lodě níž k hladině (jinak "lítá" nad vodou). Děláme to
            // vždycky — i když je model zrovna schovaný — ať plovoucí kopie
            // (ParkedBoat) i nasednutí bez volání Apply mají loď ve správné výšce.
            float sink = BOAT_SINK + (selected == shipRow ? ROW_SINK_ADJUST : 0f);
            Vector3 lp = selected.transform.localPosition;
            selected.transform.localPosition = new Vector3(lp.x, -sink, lp.z);

            // Model lodě je vidět jen když hráč pluje (ne pěšky, ne rozbitá loď).
            selected.SetActive(!hideBoat);
        }

        // Řekni PlayerControlleru, který objekt je teď jeho loď (kvůli otáčení).
        if (player != null && selected != null)
            player.boatModel = selected.transform;
    }
}
