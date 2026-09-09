using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  SoloPause.cs
//  V SÓLO hře zastaví čas (Time.timeScale = 0), když má hráč otevřený obchod,
//  dialog s dědou nebo velkou mapu — ať ho pirát nesejme, když nakupuje.
//
//  V COOPU se NEpauzuje (druhý hráč hraje dál). Tam místo toho platí, že hráč
//  s otevřeným oknem nedostane zásah — to řeší PlayerController.ModalOpen.
//
//  Pauza z menu / smrti / hlavního menu si timeScale řídí sama — SoloPause do
//  toho nešahá a vrací čas do chodu jen tehdy, když ho sám zastavil.
//
//  POZOR: interiér majáku (LighthouseInterior) je vlastní scéna, kde se CHODÍ —
//  ten se NEpauzuje (jinak by nešlo hýbat). Pauzu tam řeší jen otevřené obchody.
//
//  Vytváří se sám z GridManager.Awake() (SoloPause.Ensure()).
// ─────────────────────────────────────────────────────────────────────────────

public class SoloPause : MonoBehaviour
{
    private static SoloPause instance;
    private bool weStoppedTime;

    public static void Ensure()
    {
        if (instance == null)
            instance = new GameObject("SoloPause").AddComponent<SoloPause>();
    }

    void OnDestroy()
    {
        // Kdyby nás zničila změna scény (sólo vstup do majáku) uprostřed pauzy,
        // vrať čas do chodu — jinak by v nové scéně timeScale zůstal 0.
        if (weStoppedTime) Time.timeScale = 1f;
        if (instance == this) instance = null;
    }

    void Update()
    {
        // V coopu se nepauzuje.
        if (MultiplayerManager.IsMultiplayer) { Release(); return; }

        // Jiný "vlastník" časomíry (menu / smrt) — nešahej na to.
        if (MainMenuManager.IsVisible || DeathScreen.IsOpen || PauseMenu.IsPaused)
            return;

        bool modal = UpgradeShopManager.AnyShopOpen
                  || MapScreen.IsOpen
                  || (StoryNpc.Instance != null && StoryNpc.Instance.IsTalking);

        if (modal && !weStoppedTime)
        {
            Time.timeScale = 0f;
            weStoppedTime  = true;
        }
        else if (!modal && weStoppedTime)
        {
            Release();
        }
    }

    // Vrátí čas do chodu, ale jen když ho zastavil právě SoloPause.
    private void Release()
    {
        if (!weStoppedTime) return;
        Time.timeScale = 1f;
        weStoppedTime  = false;
    }
}
