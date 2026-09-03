using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  HarborManager.cs  — ⚠️ STARÁ VERZE, UŽ SE NEPOUŽÍVÁ
//
//  Tohle byl úplně první pokus o obchod: fungoval jen přes Debug.Log do konzole
//  Unity a měl jen dvě vylepšení. Nahradil ho plnohodnotný UpgradeShopManager
//  (okno s tlačítky, víc vylepšení, podpora dvou hráčů).
//
//  Soubor je tu schválně ponechaný: ve scéně na něj pořád odkazuje jeden
//  (nečinný) objekt "HarborManager". Kdyby se skript smazal, Unity by u toho
//  objektu hlásilo "missing script". Nic nevolá jeho metody, takže hru nijak
//  neovlivňuje.
// ─────────────────────────────────────────────────────────────────────────────

public class HarborManager : MonoBehaviour
{
    public int speedUpgradeCost = 150;
    public int rodUpgradeCost   = 100;

    private GridManager gridManager;

    void Start()
    {
        gridManager = FindFirstObjectByType<GridManager>();
        if (gridManager == null)
            Debug.LogError("Chyba: Na scéně chybí GridManager!");
    }

    // Zkusí koupit vylepšení: musí být ještě nekoupené a hráč musí mít dost mincí.
    private bool TryBuyUpgrade(ref bool upgradeFlag, int cost)
    {
        if (upgradeFlag) return false;
        GameData data = gridManager.gameData;
        if (data.coins < cost) return false;

        data.coins -= cost;
        upgradeFlag = true;
        gridManager.Save();
        return true;
    }

    public void BuySpeedUpgrade()
    {
        if (!TryBuyUpgrade(ref gridManager.gameData.hasSpeedUpgrade, speedUpgradeCost))
            Debug.Log(gridManager.gameData.hasSpeedUpgrade
                ? "Vylepšení rychlosti už máš."
                : $"Nemáš dostatek mincí! Potřebuješ {speedUpgradeCost}, máš jen {gridManager.gameData.coins}.");
    }

    public void BuyRodUpgrade()
    {
        if (!TryBuyUpgrade(ref gridManager.gameData.hasRodUpgrade, rodUpgradeCost))
            Debug.Log(gridManager.gameData.hasRodUpgrade
                ? "Vylepšení prutu už máš."
                : $"Nemáš dostatek mincí! Potřebuješ {rodUpgradeCost}, máš jen {gridManager.gameData.coins}.");
    }

    public void DisplayShopOptions()
    {
        Debug.Log("--- Přístav (Obchod) ---");
        string speedStatus = gridManager.gameData.hasSpeedUpgrade ? "Již zakoupeno" : $"Cena: {speedUpgradeCost} mincí";
        Debug.Log($"1. Vylepšení Rychlosti: {speedStatus}");
        string rodStatus = gridManager.gameData.hasRodUpgrade ? "Již zakoupeno" : $"Cena: {rodUpgradeCost} mincí";
        Debug.Log($"2. Vylepšení Prutu: {rodStatus}");
    }
}
