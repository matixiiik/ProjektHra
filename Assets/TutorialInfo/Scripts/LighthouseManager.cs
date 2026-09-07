using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// ─────────────────────────────────────────────────────────────────────────────
//  LighthouseManager.cs
//  Vstup do majáku a návrat z něj.
//
//  Maják je samostatná dlaždice na ostrově (TileType.Lighthouse). Hráč u ní
//  stojí pěšky a dá `E` (hráč 1) / `Numpad1` (hráč 2) → PlayerController zavolá
//  Enter(). Uvnitř majáku jsou oba obchody (upgrade + quest).
//
//  SÓLO: `E` u majáku uloží hru a přepne na scénu LighthouseInterior (celá
//        obrazovka). Návrat řeší LighthouseInterior.ExitToIsland().
//  COOP: nejde přepnout celou scénu (vzalo by to i druhého hráče). Scéna majáku
//        se proto načte ADITIVNĚ vedle herní. Půlka toho hráče, co vešel, ukáže
//        interiér, druhý hráč hraje dál na své půlce. Návrat = odečtení té
//        scény (ExitCoop). Naráz může být uvnitř jen jeden hráč.
// ─────────────────────────────────────────────────────────────────────────────

public class LighthouseManager : MonoBehaviour
{
    /// <summary>Jediná instance ve scéně (PlayerController si ji přes ni volá).</summary>
    public static LighthouseManager Instance { get; private set; }

    /// <summary>Který hráč je zrovna "v majáku" (-1 = nikdo).</summary>
    public static int InsidePlayerIndex { get; private set; } = -1;

    /// <summary>Který hráč zrovna vchází (čte LighthouseInterior při Awake additivní scény).</summary>
    public static int PendingPlayerIndex { get; private set; } = -1;

    private const string InteriorScene = "LighthouseInterior";
    private bool switching; // právě probíhá načítání / odečítání scény

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Hráč vešel do majáku.</summary>
    public void Enter(int playerIndex)
    {
        if (switching) return;
        if (InsidePlayerIndex >= 0) return; // někdo už uvnitř je (zatím jen jeden naráz)

        InsidePlayerIndex   = playerIndex;
        PendingPlayerIndex  = playerIndex;

        var grid = FindFirstObjectByType<GridManager>();
        if (grid != null && grid.gameData != null)
        {
            // po návratu ať ten hráč stojí pěšky u majáku
            if (playerIndex == 0) grid.gameData.isOnFoot = true;
            grid.Save();
        }

        if (MultiplayerManager.IsMultiplayer)
            StartCoroutine(EnterCoop());   // scéna navíc, druhý hráč hraje dál
        else
            SceneManager.LoadScene(InteriorScene); // sólo = plné přepnutí
    }

    // Coop: načti scénu majáku aditivně. Rozdělení kamer a zmražení toho hráče,
    // co vešel, si po Awake udělá LighthouseInterior (přes MultiplayerManager).
    private IEnumerator EnterCoop()
    {
        switching = true;
        yield return SceneManager.LoadSceneAsync(InteriorScene, LoadSceneMode.Additive);
        switching = false;
    }

    /// <summary>SÓLO: hráč vyšel z majáku (jen vynuluje příznak, scénu přepíná ExitToIsland).</summary>
    public void Exit()
    {
        InsidePlayerIndex  = -1;
        PendingPlayerIndex = -1;
    }

    /// <summary>COOP: hráč vyšel z majáku — odečti scénu majáku a vrať mu normální ovládání.</summary>
    public void ExitCoop()
    {
        if (switching) return;
        StartCoroutine(ExitCoopRoutine());
    }

    private IEnumerator ExitCoopRoutine()
    {
        switching = true;
        InsidePlayerIndex  = -1;
        PendingPlayerIndex = -1;
        MultiplayerManager.EndLighthouseSplit();

        if (SceneManager.GetSceneByName(InteriorScene).isLoaded)
            yield return SceneManager.UnloadSceneAsync(InteriorScene);

        switching = false;
    }
}
