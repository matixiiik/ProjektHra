using UnityEngine;

public class ShipModelSwitcher : MonoBehaviour
{
    public GameObject shipSmall;
    public GameObject shipMedium;
    public GameObject shipLarge;

    private GridManager grid;
    private PlayerController player;

    void Start()
    {
        grid   = FindFirstObjectByType<GridManager>();
        player = GetComponent<PlayerController>() ?? GetComponentInParent<PlayerController>();
        Apply();
    }

    public void Apply()
    {
        if (grid == null) return;

        bool isP2  = player != null && player.playerIndex == 1;
        int  level  = isP2 ? grid.gameData.player2ShipLevel : grid.gameData.shipLevel;
        bool onFoot = player != null ? player.IsOnFoot : grid.gameData.isOnFoot;

        // Deaktivuj vsechny lode
        if (shipSmall)  shipSmall.SetActive(false);
        if (shipMedium) shipMedium.SetActive(false);
        if (shipLarge)  shipLarge.SetActive(false);

        // Aktivuj spravnou lod (jen kdyz neni panacek na ostrove)
        GameObject selected = level == 0 ? shipSmall : level == 1 ? shipMedium : shipLarge;
        if (selected != null && !onFoot)
            selected.SetActive(true);

        // Aktualizuj referenci boatModel v PlayerController
        if (player != null && selected != null)
            player.boatModel = selected.transform;
    }
}
