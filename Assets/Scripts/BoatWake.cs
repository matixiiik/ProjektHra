using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  BoatWake.cs
//  Pěna / vlnky za lodí, když pluje. Je to malý ParticleSystem, který jede za
//  lodí daného hráče. Pokaždé, co se loď kus posune, přidá pár částic — takže
//  když loď stojí, nic se neděje, a když pluje, táhne se za ní stopa, co mizí.
//
//  Objekt si vytváří PlayerController.Start() (jeden na hráče) a taky
//  PirateShip.Spawn() (jeden na pirátskou loď — přes BindShip). Není potřeba
//  nic zapojovat ve scéně. Materiál částice = jemná bílá tečka generovaná v kódu.
// ─────────────────────────────────────────────────────────────────────────────

public class BoatWake : MonoBehaviour
{
    private const float WATER_Y = -0.12f; // zhruba výška hladiny (OceanSurface.seaLevel)

    private PlayerController player;       // režim "za hráčovou lodí"
    private Transform        shipOverride; // režim "za libovolnou lodí" (piráti)
    private ParticleSystem   ps;

    private Vector3 lastPos;
    private bool    hasLast;

    /// <summary>Pěna za hráčovou lodí. Zavolá PlayerController hned po AddComponent.</summary>
    public void Bind(PlayerController owner) => player = owner;

    /// <summary>Pěna za libovolnou lodí (pirát). ship = kořen modelu lodě.</summary>
    public void BindShip(Transform ship) => shipOverride = ship;

    void Start() => BuildParticles();

    void LateUpdate()
    {
        // Vlastník zmizel (konec split-screenu / potopený pirát) → ukliď se.
        if (player == null && shipOverride == null) { Destroy(gameObject); return; }
        if (ps == null) return;

        // Loď, za kterou pěnu táhneme, a jestli se zrovna "pluje".
        Transform boat;
        bool sailing;
        if (player != null)
        {
            boat    = player.boatModel;
            sailing = !player.IsOnFoot && player.enabled && player.gameObject.activeInHierarchy;
        }
        else
        {
            boat    = shipOverride;
            sailing = true; // pirátská loď pluje pořád
        }

        // Drž se kousek za lodí, na hladině.
        Vector3 p    = boat != null ? boat.position : transform.position;
        Vector3 back = boat != null ? -boat.forward : Vector3.back;
        Vector3 pos  = new Vector3(p.x + back.x * 0.5f, WATER_Y, p.z + back.z * 0.5f);
        transform.position = pos;
        if (boat != null)
            transform.rotation = Quaternion.LookRotation(new Vector3(boat.forward.x, 0f, boat.forward.z), Vector3.up);

        // Kolik loď ujela od minule (jen po hladině).
        float moved = hasLast ? Vector2.Distance(new Vector2(pos.x, pos.z), new Vector2(lastPos.x, lastPos.z)) : 0f;
        lastPos = pos;
        hasLast = true;

        // Za každý kousek pohybu pár částic pěny.
        if (sailing && moved > 0.03f)
            ps.Emit(Mathf.Clamp(Mathf.RoundToInt(moved * 9f), 1, 10));
    }

    void BuildParticles()
    {
        ps = gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop            = true;
        main.startLifetime   = 1.6f;
        main.startSpeed      = 0.1f;
        main.startSize       = new ParticleSystem.MinMaxCurve(0.45f, 0.9f);
        main.startColor      = new Color(1f, 1f, 1f, 0.9f);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 400;

        var emission = ps.emission;      // pěnu přidáváme ručně z LateUpdate podle ujeté dráhy
        emission.rateOverTime     = 0f;
        emission.rateOverDistance = 0f;

        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale     = new Vector3(0.9f, 0.02f, 0.5f);

        var colOverLife = ps.colorOverLifetime;
        colOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.82f, 0.9f, 1f), 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.8f, 0.35f), new GradientAlphaKey(0f, 1f) });
        colOverLife.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.4f, 1f, 1.7f));

        var psr = ps.GetComponent<ParticleSystemRenderer>();
        var sh  = Shader.Find("Sprites/Default");
        if (sh != null)
        {
            var mat = new Material(sh) { mainTexture = SoftDot() };
            psr.material = mat;
        }
        psr.sortingOrder      = 5;
        psr.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
        psr.receiveShadows     = false;

        ps.Play();
    }

    // Jemná bílá tečka (kruhový gradient), aby částice nebyly ostré čtverečky.
    private static Texture2D SoftDot()
    {
        const int s = 32;
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - d);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
            }
        t.Apply();
        return t;
    }
}
