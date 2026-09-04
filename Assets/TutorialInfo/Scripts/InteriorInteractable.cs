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

    /// <summary>Vykoná akci tohoto bodu.</summary>
    public void Trigger()
    {
        switch (action)
        {
            case InteriorAction.UpgradeShop:
                var us = FindFirstObjectByType<UpgradeShopManager>();
                if (us != null) us.Open(0);
                break;

            case InteriorAction.QuestShop:
                var qs = FindFirstObjectByType<QuestShopManager>();
                if (qs != null) qs.Open(0);
                break;

            case InteriorAction.Exit:
                LighthouseInterior.ExitToIsland();
                break;
        }
    }
}
