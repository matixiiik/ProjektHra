// Soubor: HarborManager.cs
using UnityEngine;
using System.Collections;

public class HarborManager : MonoBehaviour
{
    // Ceny vylepšení (můžeš je nastavit v Unity Editoru)
    public int speedUpgradeCost = 150; 
    public int rodUpgradeCost = 100;

    // Odkaz na GridManager pro přístup k GameData
    private GridManager gridManager;

    void Start()
    {
        // Najdi GridManager na scéně
        gridManager = FindFirstObjectByType<GridManager>(); 

        if (gridManager == null)
        {
            Debug.LogError("Chyba: Na scéně chybí GridManager!");
        }
    }

    // Veřejná metoda pro nákup vylepšení rychlosti lodi
    public void BuySpeedUpgrade()
    {
        GameData data = gridManager.gameData;
        
        if (data.hasSpeedUpgrade)
        {
            Debug.Log("Vylepšení rychlosti už máš.");
            return;
        }

        if (data.coins >= speedUpgradeCost)
        {
            data.coins -= speedUpgradeCost;
            data.hasSpeedUpgrade = true;
            Debug.Log($"Koupil jsi vylepšení Rychlosti za {speedUpgradeCost} mincí! Nový stav mincí: {data.coins}");
            gridManager.Save(); 
        }
        else
        {
            Debug.Log($"Nemáš dostatek mincí! Potřebuješ {speedUpgradeCost}, máš jen {data.coins}.");
        }
    }

    // Veřejná metoda pro nákup vylepšení rybářského prutu
    public void BuyRodUpgrade()
    {
        GameData data = gridManager.gameData;
        
        if (data.hasRodUpgrade)
        {
            Debug.Log("Vylepšení prutu už máš.");
            return;
        }

        if (data.coins >= rodUpgradeCost)
        {
            data.coins -= rodUpgradeCost;
            data.hasRodUpgrade = true;
            Debug.Log($"Koupil jsi vylepšení Prutu za {rodUpgradeCost} mincí! Nový stav mincí: {data.coins}");
            gridManager.Save();
        }
        else
        {
            Debug.Log($"Nemáš dostatek mincí! Potřebuješ {rodUpgradeCost}, máš jen {data.coins}.");
        }
    }
    
    // Prozatím jen vypíše možnosti do konzole
    public void DisplayShopOptions()
    {
        Debug.Log("--- Přístav (Obchod) ---");
        
        string speedStatus = gridManager.gameData.hasSpeedUpgrade ? "Již zakoupeno" : $"Cena: {speedUpgradeCost} mincí";
        Debug.Log($"1. Vylepšení Rychlosti: {speedStatus}");
        
        string rodStatus = gridManager.gameData.hasRodUpgrade ? "Již zakoupeno" : $"Cena: {rodUpgradeCost} mincí";
        Debug.Log($"2. Vylepšení Prutu: {rodStatus}");
    }
}