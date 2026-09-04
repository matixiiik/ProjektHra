using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  GameSession.cs
//  Jediné místo, kde "žije" stav hry (GameData), a to i při přechodu mezi
//  scénami. Hlavní scéna SampleScene a scéna interiéru majáku
//  (LighthouseInterior) sdílí ta stejná data právě přes tuhle třídu.
//
//  Objekt se vytváří sám (Ensure) a přežívá načtení jiné scény
//  (DontDestroyOnLoad). Ve scéně se nemusí nic připravovat.
//
//  Tok dat:
//   • GridManager v SampleScene načte save a předá ho sem (SetData).
//   • Cokoli (hráč, obchody) čte a mění GameSession.Instance.Data.
//   • Save() zapíše na disk a upozorní posluchače (OnDataChanged).
// ─────────────────────────────────────────────────────────────────────────────

public class GameSession : MonoBehaviour
{
    /// <summary>Jediná instance. Nikdy se neničí při změně scény.</summary>
    public static GameSession Instance { get; private set; }

    /// <summary>Veškerý stav hry (ekonomika, mřížka, questy…).</summary>
    public GameData Data;

    /// <summary>Vyvolá se po Save() a SetData() — poslouchá HUD interiéru apod.</summary>
    public event System.Action OnDataChanged;

    /// <summary>
    /// True jen během jednoho snímku po návratu z majáku do SampleScene.
    /// Podle toho hlavní menu pozná, že se nemá zobrazit (hra pokračuje).
    /// </summary>
    public static bool ReturningFromLighthouse;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Zajistí, že instance existuje. Volá GridManager i interiér majáku.</summary>
    public static GameSession Ensure()
    {
        if (Instance == null)
            new GameObject("GameSession").AddComponent<GameSession>();
        return Instance;
    }

    /// <summary>Nastaví nová data (nová hra / načtení slotu).</summary>
    public void SetData(GameData data)
    {
        Data = data;
        OnDataChanged?.Invoke();
    }

    /// <summary>Uloží aktuální data na disk a upozorní posluchače.</summary>
    public void Save()
    {
        if (Data != null) SaveManager.SaveGame(Data);
        OnDataChanged?.Invoke();
    }

    /// <summary>Jen upozorní posluchače (bez zápisu na disk).</summary>
    public void NotifyChanged() => OnDataChanged?.Invoke();
}
