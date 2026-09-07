using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  InteriorInteractable.cs
//  Jeden "bod zájmu" ve vnitřku majáku, ke kterému hráč přijde a dá E.
//   • UpgradeShop → otevře obchod s vylepšeními
//   • QuestShop   → otevře obchod s questy
//   • Exit        → odejít ven na ostrov
//
//  InteriorPlayer si najde nejbližší takový objekt v dosahu a zavolá Trigger().
// ─────────────────────────────────────────────────────────────────────────────

public enum InteriorAction { UpgradeShop, QuestShop, Exit }

public class InteriorInteractable : MonoBehaviour
{
    public InteriorAction action;

    [Tooltip("Na jakou vzdálenost (v metrech) sem hráč dosáhne.")]
    public float range = 1.6f;

    [Tooltip("Text, co se hráči ukáže, když je v dosahu.")]
    public string prompt = "E — otevřít";

    /// <summary>Vykoná akci tohoto bodu za daného hráče (0 = P1/sólo, 1 = P2 ve split).</summary>
    public void Trigger(int playerIndex)
    {
        switch (action)
        {
            case InteriorAction.UpgradeShop:
                var us = FindInMyScene<UpgradeShopManager>();
                if (us != null) us.Open(playerIndex);
                break;

            case InteriorAction.QuestShop:
                var qs = FindInMyScene<QuestShopManager>();
                if (qs != null) qs.Open(playerIndex);
                break;

            case InteriorAction.Exit:
                LighthouseInterior.ExitToIsland();
                break;
        }
    }

    // Najde komponentu přednostně ve STEJNÉ scéně jako tenhle bod (kvůli coopu,
    // kde vedle sebe běží scéna majáku i herní scéna a každá má svůj obchod).
    private T FindInMyScene<T>() where T : MonoBehaviour
    {
        foreach (var m in FindObjectsByType<T>(FindObjectsSortMode.None))
            if (m.gameObject.scene == gameObject.scene) return m;
        return FindFirstObjectByType<T>();
    }
}
