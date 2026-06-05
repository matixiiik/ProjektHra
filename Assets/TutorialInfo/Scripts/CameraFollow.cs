using UnityEngine;

/// <summary>
/// Sleduje cílový Transform se stejným offsetem jako při inicializaci.
/// Offset se vypočítá automaticky první snímek po nastavení targetu.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;

    private Vector3 offset;
    private bool    initialized;

    public void SetTarget(Transform t)
    {
        target      = t;
        initialized = false; // přepočítá offset při prvním LateUpdate
    }

    void LateUpdate()
    {
        if (target == null) return;

        if (!initialized)
        {
            offset      = transform.position - target.position;
            initialized = true;
        }

        transform.position = target.position + offset;
    }
}
