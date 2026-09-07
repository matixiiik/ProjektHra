using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// ─────────────────────────────────────────────────────────────────────────────
//  LighthouseManager.cs
//  Vstup do majáku a návrat z něj.
//
//  Maják je samostatná dlaždice na ostrově (TileType.Lighthouse). Hráč u ní
//  stojí pěšky a dá `E` → PlayerController zavolá Enter(). Uvnitř majáku jsou
//  oba obchody (upgrade + quest).
//
//  SÓLO: `E` u majáku uloží hru a přepne na scénu LighthouseInterior (celá
//        obrazovka). Návrat řeší LighthouseInterior.ExitToIsland().
//  COOP: nejde přepnout celou scénu (vzalo by to i hráče 2). Scéna majáku se
//        proto načte ADITIVNĚ vedle herní. Půlka hráče 1 ukáže interiér,
//        hráč 2 hraje dál na své půlce. Návrat = odečtení té scény (ExitCoop).
// ─────────────────────────────────────────────────────────────────────────────

public class LighthouseManager : MonoBehaviour
{
    /// <summary>Jediná instance ve scéně (PlayerController si ji přes ni volá).</summary>
    public static LighthouseManager Instance { get; private set; }

    /// <summary>Který hráč je zrovna "v majáku" (-1 = nikdo). Split-screen: jen P1 může.</summary>
    public static int InsidePlayerIndex { get; private set; } = -1;

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
        // Ve split-screenu do majáku pustíme jen hráče 1 (interiér je jedna kamera).
        if (MultiplayerManager.IsMultiplayer && playerIndex != 0) return;
        if (switching) return;

        InsidePlayerIndex = playerIndex;

        var grid = FindFirstObjectByType<GridManager>();
        if (grid != null && grid.gameData != null)
        {
            grid.gameData.isOnFoot = true; // po návratu ať hráč stojí pěšky u majáku
            grid.Save();
        }

        if (MultiplayerManager.IsMultiplayer)
            StartCoroutine(EnterCoop());   // scéna navíc, hráč 2 hraje dál
        else
            SceneManager.LoadScene(InteriorScene); // sólo = plné přepnutí
    }

    // Coop: načti scénu majáku aditivně. Rozdělení kamer a zmražení hráče 1
    // si po Awake udělá LighthouseInterior (volá MultiplayerManager.BeginLighthouseSplit).
    private IEnumerator EnterCoop()
    {
        switching = true;
        yield return SceneManager.LoadSceneAsync(InteriorScene, LoadSceneMode.Additive);
        switching = false;
    }

    /// <summary>SÓLO: hráč vyšel z majáku ven na ostrov (jen vynuluje příznak, scénu přepíná ExitToIsland).</summary>
    public void Exit()
    {
        InsidePlayerIndex = -1;
    }

    /// <summary>COOP: hráč 1 vyšel z majáku — odečti scénu majáku a vrať mu normální ovládání.</summary>
    public void ExitCoop()
    {
        if (switching) return;
        StartCoroutine(ExitCoopRoutine());
    }

    private IEnumerator ExitCoopRoutine()
    {
        switching = true;
        InsidePlayerIndex = -1;
        MultiplayerManager.EndLighthouseSplit();

        if (SceneManager.GetSceneByName(InteriorScene).isLoaded)
            yield return SceneManager.UnloadSceneAsync(InteriorScene);

        switching = false;
    }
}
