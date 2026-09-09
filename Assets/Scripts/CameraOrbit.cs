using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  CameraOrbit.cs
//  Kamera obíhá kolem hráče. Drž PRAVÉ tlačítko myši a hýbej myší → kamera
//  se otáčí (vodorovně i svisle, svisle omezeno). Bez držení drží poslední úhel.
//
//  Je na objektu "Main Camera", který je potomkem hráče. Hráč se neotáčí, takže
//  lokální prostor = světový (co do rotace). Pivot = počátek hráče + malá výška.
// ─────────────────────────────────────────────────────────────────────────────

public class CameraOrbit : MonoBehaviour
{
    [Tooltip("Vzdálenost kamery od hráče.")]
    public float distance = 14f;

    [Tooltip("Výchozí sklon (° dolů). Mění se svislým tahem myši.")]
    public float pitch = 52f;

    [Tooltip("Výchozí otočení kolem hráče (°). Mění se vodorovným tahem myši.")]
    public float yaw = 0f;

    [Tooltip("Citlivost otáčení.")]
    public float sensitivity = 0.18f;

    public float minPitch = 25f;
    public float maxPitch = 80f;

    [Tooltip("O kolik výš než počátek hráče se hráč promítne (aby nebyl úplně dole).")]
    public float pivotHeight = 0.8f;

    private Vector3 lastMouse;

    void LateUpdate()
    {
        // Neotáčej, když je otevřené menu / obchod / konzole (myš tam klikáš).
        bool uiBlocking = MainMenuManager.IsVisible
                       || GameConsole.IsOpen
                       || UpgradeShopManager.AnyShopOpen;

        if (!uiBlocking)
        {
            if (Input.GetMouseButtonDown(1))
                lastMouse = Input.mousePosition;

            if (Input.GetMouseButton(1))
            {
                Vector3 delta = Input.mousePosition - lastMouse;
                yaw  += delta.x * sensitivity;
                pitch = Mathf.Clamp(pitch - delta.y * sensitivity, minPitch, maxPitch);
                lastMouse = Input.mousePosition;
            }
        }

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot  = new Vector3(0f, pivotHeight, 0f);

        transform.localRotation = rot;
        transform.localPosition = pivot + rot * new Vector3(0f, 0f, -distance);
    }
}
