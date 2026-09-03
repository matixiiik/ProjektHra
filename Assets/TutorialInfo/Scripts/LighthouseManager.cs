using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  LighthouseManager.cs
//  Vstup do majáku a návrat z něj.
//
//  Maják je samostatná dlaždice na ostrově (TileType.Lighthouse). Hráč u ní
//  stojí pěšky a dá `E` → PlayerController zavolá Enter(). Uvnitř majáku jsou
//  oba obchody (upgrade + quest).
//
//  FÁZE 2a: zatím jen zaznamená, že se do majáku "vešlo". Skutečný přechod do
//  samostatné scény LighthouseInterior doplní fáze 2b.
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

    /// <summary>Hráč vešel do majáku.</summary>
    public void Enter(int playerIndex)
    {
        // Ve split-screenu do majáku pustíme jen hráče 1 (interiér je jedna kamera).
        if (MultiplayerManager.IsMultiplayer && playerIndex != 0) return;

        InsidePlayerIndex = playerIndex;
        Debug.Log("Vstup do majáku (hráč " + playerIndex + ") — TODO: načíst scénu LighthouseInterior (fáze 2b)");
    }

    /// <summary>Hráč vyšel z majáku ven na ostrov.</summary>
    public void Exit()
    {
        InsidePlayerIndex = -1;
    }
}
