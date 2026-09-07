using System.Collections;
using System.Collections.Generic;
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
//  COOP: nejde přepnout celou scénu (vzalo by to i druhého hráče). Každý hráč,
//        co vejde, dostane VLASTNÍ kopii scény majáku načtenou ADITIVNĚ
//        (posunutou daleko od oceánu i od sebe navzájem). Můžou být v majáku
//        oba naráz — každý na své půlce obrazovky. Návrat = odečtení té
//        jeho kopie (ExitCoop).
// ─────────────────────────────────────────────────────────────────────────────

public class LighthouseManager : MonoBehaviour
{
    /// <summary>Jediná instance ve scéně (PlayerController si ji přes ni volá).</summary>
    public static LighthouseManager Instance { get; private set; }

    /// <summary>Který hráč zrovna vchází (čte LighthouseInterior při Awake additivní scény).</summary>
    public static int PendingPlayerIndex { get; private set; } = -1;

    // Kdo je zrovna uvnitř majáku (index 0 = P1, 1 = P2).
    private static readonly bool[] inside = new bool[2];

    /// <summary>Je tenhle hráč zrovna v majáku?</summary>
    public static bool IsInside(int playerIndex)
        => playerIndex >= 0 && playerIndex < 2 && inside[playerIndex];

    /// <summary>První hráč, co je uvnitř (-1 = nikdo). Kvůli zpětné kompatibilitě.</summary>
    public static int InsidePlayerIndex => inside[0] ? 0 : (inside[1] ? 1 : -1);

    private const string InteriorScene = "LighthouseInterior";
    private bool loading; // právě probíhá načítání / odečítání scény (jen jedno naráz)

    // Coop: kopie scény majáku patřící jednotlivým hráčům.
    private readonly Dictionary<int, Scene> playerScenes = new Dictionary<int, Scene>();

    void Awake()
    {
        Instance = this;
        inside[0] = false;
        inside[1] = false;
        PendingPlayerIndex = -1;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Hráč vešel do majáku.</summary>
    public void Enter(int playerIndex)
    {
        if (loading) return;
        if (playerIndex >= 0 && playerIndex < 2 && inside[playerIndex]) return; // ten hráč už uvnitř je

        var grid = FindFirstObjectByType<GridManager>();
        if (grid != null && grid.gameData != null)
        {
            // po návratu ať ten hráč stojí pěšky u majáku (jen P1 – P2 nemá isOnFoot v save)
            if (playerIndex == 0) grid.gameData.isOnFoot = true;
            grid.Save();
        }

        if (MultiplayerManager.IsMultiplayer)
        {
            inside[playerIndex]  = true;
            PendingPlayerIndex   = playerIndex;
            StartCoroutine(EnterCoop(playerIndex));
        }
        else
        {
            inside[0]           = true;
            PendingPlayerIndex  = 0;
            SceneManager.LoadScene(InteriorScene); // sólo = plné přepnutí
        }
    }

    // Coop: načti VLASTNÍ kopii scény majáku pro tohohle hráče. Posunutí,
    // rozdělení kamer a zmražení hráče si po Awake udělá LighthouseInterior.
    private IEnumerator EnterCoop(int playerIndex)
    {
        loading = true;
        yield return SceneManager.LoadSceneAsync(InteriorScene, LoadSceneMode.Additive);
        playerScenes[playerIndex] = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
        loading = false;
    }

    /// <summary>SÓLO: hráč vyšel z majáku (jen vynuluje příznaky, scénu přepíná ExitToIsland).</summary>
    public void Exit()
    {
        inside[0] = false;
        inside[1] = false;
        PendingPlayerIndex = -1;
    }

    /// <summary>COOP: hráč vyšel z majáku — odečti jeho kopii scény a vrať mu ovládání.</summary>
    public void ExitCoop(int playerIndex)
    {
        if (loading) return;
        StartCoroutine(ExitCoopRoutine(playerIndex));
    }

    private IEnumerator ExitCoopRoutine(int playerIndex)
    {
        loading = true;
        if (playerIndex >= 0 && playerIndex < 2) inside[playerIndex] = false;
        MultiplayerManager.EndLighthouseSplit(playerIndex);

        if (playerScenes.TryGetValue(playerIndex, out Scene sc))
        {
            playerScenes.Remove(playerIndex);
            if (sc.IsValid() && sc.isLoaded)
                yield return SceneManager.UnloadSceneAsync(sc);
        }
        loading = false;
    }
}
