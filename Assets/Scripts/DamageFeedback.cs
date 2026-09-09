using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  DamageFeedback.cs
//  Krátká reakce na zásah TOHOTO hráče: červený záblesk přes obrazovku,
//  lehké cuknutí kamery a žbluňk částic u lodě. Aby bylo poznat, že to bolí,
//  ne že se jen hýbe HP bar.
//
//  Jeden na hráče — vytváří ho PlayerController.Start(). Zavolá se Play() z
//  DamageBoat / DamagePlayer. Nic se nezapojuje ve scéně.
//
//  [DefaultExecutionOrder(1000)] → cuknutí kamery se přičítá až PO tom, co si
//  kamera (CameraOrbit / MultiplayerManager) nastaví svou pozici.
// ─────────────────────────────────────────────────────────────────────────────

[DefaultExecutionOrder(1000)]
public class DamageFeedback : MonoBehaviour
{
    private const float FLASH_TIME = 0.35f; // jak dlouho doznívá červený záblesk
    private const float SHAKE_TIME = 0.30f; // jak dlouho cuká kamera
    private const float SHAKE_AMP  = 0.35f; // síla cuknutí (jednotky)

    private PlayerController player;
    private float flashUntil;
    private float shakeUntil;
    private Texture2D redTex;

    /// <summary>Zavolá PlayerController hned po AddComponent.</summary>
    public void Bind(PlayerController owner) => player = owner;

    /// <summary>Spustí reakci na zásah (záblesk + cuknutí + žbluňk).</summary>
    public void Play()
    {
        flashUntil = Time.time + FLASH_TIME;
        shakeUntil = Time.time + SHAKE_TIME;
        SpawnSplash();
    }

    void OnDestroy()
    {
        if (redTex != null) Destroy(redTex);
    }

    // Kamera tohoto hráče (P2 dostane svou od MultiplayerManageru, P1 = hlavní).
    private Transform Cam
        => player != null && player.viewCamera != null ? player.viewCamera
         : Camera.main != null ? Camera.main.transform : null;

    void LateUpdate()
    {
        if (Time.time >= shakeUntil) return;

        Transform cam = Cam;
        if (cam == null) return;

        // Klesající náhodné cuknutí — přičte se k už nastavené pozici kamery.
        float k = (shakeUntil - Time.time) / SHAKE_TIME; // 1 → 0
        float amp = SHAKE_AMP * k * k;
        cam.position += new Vector3(
            (Random.value - 0.5f) * 2f,
            (Random.value - 0.5f) * 2f,
            (Random.value - 0.5f) * 2f) * amp;
    }

    void OnGUI()
    {
        if (Time.time >= flashUntil) return;

        float a = (flashUntil - Time.time) / FLASH_TIME; // 1 → 0
        if (redTex == null)
        {
            redTex = new Texture2D(1, 1);
            redTex.SetPixel(0, 0, Color.white);
            redTex.Apply();
        }

        // Sólo = celá obrazovka, split screen = půlka tohoto hráče.
        float x = 0f, w = Screen.width;
        if (MultiplayerManager.IsMultiplayer && player != null)
        {
            w = Screen.width * 0.5f;
            x = player.playerIndex == 1 ? Screen.width * 0.5f : 0f;
        }

        GUI.color = new Color(0.8f, 0.05f, 0.05f, a * 0.4f);
        GUI.DrawTexture(new Rect(x, 0, w, Screen.height), redTex);
        GUI.color = Color.white;
    }

    // Krátký výbuch bílých částic u lodě (voda vystříkne).
    private void SpawnSplash()
    {
        if (player == null) return;

        var go = new GameObject("HitSplash");
        go.transform.position = player.transform.position + Vector3.up * 0.1f;

        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop            = false;
        main.duration        = 0.6f;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(2.5f, 5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
        main.gravityModifier = 1.4f;
        main.startColor      = new Color(0.85f, 0.92f, 1f, 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 40;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius    = 0.25f;

        var psr = go.GetComponent<ParticleSystemRenderer>();
        var sh  = Shader.Find("Sprites/Default");
        if (sh != null) psr.material = new Material(sh);
        psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        ps.Play();
        Destroy(go, 1.5f);
    }
}
