using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  LighthouseManager.cs
//  Vstup do majáku a návrat z něj.
//
//  Maják je samostatná dlaždice na ostrově (TileType.Lighthouse). Hráč u ní
//  stojí pěšky a dá `E` → PlayerController zavolá Enter(). Uvnitř majáku jsou
//  oba obchody (upgrade + quest).
//
//  `E` u majáku → uloží hru a načte scénu LighthouseInterior. Návrat zpět
//  řeší LighthouseInterior.ExitToIsland().
// ─────────────────────────────────────────────────────────────────────────────

public class LighthouseManager : MonoBehaviour
{
    /// <summary>Jediná instance ve scéně (PlayerController si ji přes ni volá).</summary>
    public static LighthouseManager Instance { get; private set; }

    /// <summary>Který hráč je zrovna "v majáku" (-1 = nikdo). Split-screen: jen P1 může.</summary>
    public static int InsidePlayerIndex { get; private set; } = -1;

    void Awake()
    {
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Hráč vešel do majáku → ulož hru a přepni na scénu interiéru.</summary>
    public void Enter(int playerIndex)
    {
        // Ve split-screenu do majáku pustíme jen hráče 1 (interiér je jedna kamera).
        if (MultiplayerManager.IsMultiplayer && playerIndex != 0) return;

        InsidePlayerIndex = playerIndex;

        var grid = FindFirstObjectByType<GridManager>();
        if (grid != null && grid.gameData != null)
        {
            grid.gameData.isOnFoot = true; // po návratu ať hráč stojí pěšky u majáku
            grid.Save();
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene("LighthouseInterior");
    }

    /// <summary>Hráč vyšel z majáku ven na ostrov.</summary>
    public void Exit()
    {
        InsidePlayerIndex = -1;
    }
}
