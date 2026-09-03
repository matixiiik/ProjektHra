using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  CameraFollow.cs
//  Kamera plynule sleduje hráče. Odečte si vzdálenost (offset) mezi kamerou
//  a hráčem v prvním snímku a pak už jen drží pořád stejný odstup.
//  Díky tomu stačí kameru ve scéně nastavit "od oka" a skript si zbytek dopočítá.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Sleduje cílový Transform se stejným offsetem jako při inicializaci.
/// Offset se vypočítá automaticky první snímek po nastavení targetu.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target; // koho kamera sleduje (hráč)

    private Vector3 offset;      // pevná vzdálenost kamera → hráč
    private bool    initialized; // false = offset se musí (znovu) spočítat

    /// <summary>Nastaví nový cíl. Offset se přepočítá při příštím LateUpdate.</summary>
    public void SetTarget(Transform t)
    {
        target      = t;
        initialized = false;
    }

    // LateUpdate běží až po Update všech objektů → hráč už je na finální pozici,
    // takže kamera "necuká".
    void LateUpdate()
    {
        if (target == null) return;

        // První snímek: zapamatuj si aktuální odstup kamery od hráče.
        if (!initialized)
        {
            offset      = transform.position - target.position;
            initialized = true;
        }

        // Drž pořád stejný odstup.
        transform.position = target.position + offset;
    }
}
