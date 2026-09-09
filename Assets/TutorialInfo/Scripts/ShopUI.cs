using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  ShopUI.cs
//  Sdílené kousky vzhledu obchodů (IMGUI). Zatím jen zavírací křížek vpravo
//  nahoře v panelu — používá ho UpgradeShopManager i QuestShopManager, ať to
//  nemá každý zvlášť.
// ─────────────────────────────────────────────────────────────────────────────

public static class ShopUI
{
    private static GUIStyle xStyle;

    /// <summary>
    /// Posouvání sortimentu obchodu myší: kolečko řeší sám ScrollView, tohle
    /// navíc umožní scrollovat i tažením (podrž levé tlačítko a táhni myší
    /// nahoru/dolů). Volá se uvnitř OnGUI obchodu, `scroll` je pozice ScrollView.
    /// </summary>
    public static void HandleDragScroll(ref Vector2 scroll)
    {
        Event e = Event.current;
        if (e == null) return;

        if (e.type == EventType.MouseDrag && e.button == 0)
        {
            scroll.y += e.delta.y; // táhneš myš dolů → posuneš se na spodek nabídky
            e.Use();
        }
    }

    /// <summary>Nakreslí zavírací křížek v pravém horním rohu panelu obchodu
    /// (panel je Rect(panelX, panelY, panelW, ...)). Vrací true, když se klikne.</summary>
    public static bool CloseButton(float panelX, float panelY, float panelW)
    {
        if (xStyle == null)
        {
            xStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize  = 15,
                fontStyle = FontStyle.Bold,
                normal    = { textColor = Color.white, background = Solid(new Color(0.55f, 0.15f, 0.15f)) },
                hover     = { textColor = Color.white, background = Solid(new Color(0.8f,  0.2f,  0.2f)) }
            };
        }

        var r = new Rect(panelX + panelW - 36f, panelY + 6f, 30f, 30f);
        return SoundManager.Click(GUI.Button(r, "X", xStyle));
    }

    private static Texture2D Solid(Color c)
    {
        var t = new Texture2D(1, 1);
        t.SetPixel(0, 0, c);
        t.Apply();
        return t;
    }
}
