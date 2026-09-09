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
//        se načte ADITIVNĚ jednou a JSOU v ní OBA hráči zároveň (jeden modrý,
//        druhý červený) — každý ovládá svou postavičku a vidí na své půlce
//        obrazovky tu samou místnost. Poslední, kdo odejde, scénu odečte.
// ─────────────────────────────────────────────────────────────────────────────

public class LighthouseManager : MonoBehaviour
{
    /// <summary>Jediná instance ve scéně (PlayerController si ji přes ni volá).</summary>
    public static LighthouseManager Instance { get; private set; }

    /// <summary>Který hráč zrovna vchází jako PRVNÍ (čte LighthouseInterior při Awake).</summary>
    public static int PendingPlayerIndex { get; private set; } = -1;

    // Kdo je zrovna uvnitř majáku (0 = P1, 1 = P2).
    private static readonly bool[] inside = new bool[2];

    /// <summary>Je tenhle hráč zrovna v majáku?</summary>
    public static bool IsInside(int playerIndex)
        => playerIndex >= 0 && playerIndex < 2 && inside[playerIndex];

    /// <summary>První hráč, co je uvnitř (-1 = nikdo). Kvůli zpětné kompatibilitě.</summary>
    public static int InsidePlayerIndex => inside[0] ? 0 : (inside[1] ? 1 : -1);

    private const string InteriorScene = "LighthouseInterior";
    private bool  switching;      // právě probíhá načítání / odečítání scény
    private Scene interiorScene;  // coop: jediná načtená scéna majáku

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
        if (switching) return;
        if (playerIndex >= 0 && playerIndex < 2 && inside[playerIndex]) return; // ten hráč už uvnitř je

        var grid = FindFirstObjectByType<GridManager>();
        if (grid != null && grid.gameData != null)
        {
            if (playerIndex == 0) grid.gameData.isOnFoot = true; // po návratu ať P1 stojí pěšky
            grid.Save();
        }

        if (!MultiplayerManager.IsMultiplayer)
        {
            inside[0]          = true;
            PendingPlayerIndex = 0;
            SceneManager.LoadScene(InteriorScene); // sólo = plné přepnutí
            return;
        }

        // Coop.
        inside[playerIndex] = true;

        bool sceneUp = interiorScene.IsValid() && interiorScene.isLoaded;
        if (sceneUp && LighthouseInterior.Instance != null)
        {
            // Maják už je otevřený druhým hráčem → jen přidej tuhle postavičku.
            LighthouseInterior.Instance.AddPlayer(playerIndex);
        }
        else
        {
            // Nikdo uvnitř — načti scénu majáku. Prvního hráče si nastaví
            // LighthouseInterior sám při Awake podle PendingPlayerIndex.
            PendingPlayerIndex = playerIndex;
            StartCoroutine(EnterCoop());
        }
    }

    private IEnumerator EnterCoop()
    {
        switching = true;
        yield return SceneManager.LoadSceneAsync(InteriorScene, LoadSceneMode.Additive);
        interiorScene = SceneManager.GetSceneAt(SceneManager.sceneCount - 1);
        switching = false;
    }

    /// <summary>SÓLO: hráč vyšel z majáku (jen vynuluje příznaky, scénu přepíná ExitToIsland).</summary>
    public void Exit()
    {
        inside[0] = false;
        inside[1] = false;
        PendingPlayerIndex = -1;
    }

    /// <summary>COOP: daný hráč vyšel z majáku. Když odešel poslední, odečte se scéna.</summary>
    public void ExitCoop(int playerIndex)
    {
        if (switching) return;

        if (playerIndex >= 0 && playerIndex < 2) inside[playerIndex] = false;

        if (LighthouseInterior.Instance != null)
            LighthouseInterior.Instance.RemovePlayer(playerIndex);

        if (!inside[0] && !inside[1])
            StartCoroutine(UnloadCoop());
    }

    private IEnumerator UnloadCoop()
    {
        switching = true;
        PendingPlayerIndex = -1;
        if (interiorScene.IsValid() && interiorScene.isLoaded)
            yield return SceneManager.UnloadSceneAsync(interiorScene);
        switching = false;
    }
}
