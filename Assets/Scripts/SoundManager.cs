using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  SoundManager.cs
//  Zvuky celé hry. Klipy se negenerují z hotových souborů, ale rovnou v kódu
//  (sinusovky a filtrovaný šum přes AudioClip.Create) — hra tak nepotřebuje
//  žádné externí audio soubory a nemusí se řešit licence.
//
//  Objekt se vytváří sám, jakmile ho někdo poprvé potřebuje (Ensure()) —
//  stejný princip jako u minimapy pro P2. Nemusí se nic ručně přidávat
//  do scény. V každé scéně (SampleScene i LighthouseInterior) vznikne
//  vlastní instance, takže třeba hukot moře automaticky zmizí, když se
//  vejde dovnitř majáku.
// ─────────────────────────────────────────────────────────────────────────────

public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Range(0f, 1f)] public float sfxVolume     = 0.55f;
    [Range(0f, 1f)] public float ambientVolume = 0.16f;

    private AudioSource sfxSource;
    private AudioSource ambientSource;

    private AudioClip clickClip;
    private AudioClip coinClip;
    private AudioClip splashClip;
    private AudioClip doorClip;
    private AudioClip waveClip;
    private AudioClip cannonClip;
    private AudioClip hitClip;
    private AudioClip sinkClip;

    // Najde existující SoundManager ve scéně, nebo si ho (i s AudioSource) vytvoří.
    public static SoundManager Ensure()
    {
        if (Instance != null) return Instance;

        GameObject go = new GameObject("SoundManager");
        return go.AddComponent<SoundManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;

        ambientSource = gameObject.AddComponent<AudioSource>();
        ambientSource.playOnAwake = false;
        ambientSource.loop = true;

        clickClip  = MakeClickClip();
        coinClip   = MakeCoinClip();
        splashClip = MakeSplashClip();
        doorClip   = MakeDoorClip();
        waveClip   = MakeWaveClip();
        cannonClip = MakeCannonClip();
        hitClip    = MakeHitClip();
        sinkClip   = MakeSinkClip();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Veřejné API (volá se odjinud) ─────────────────────────────────────────
    public static void PlayClick()  { SoundManager m = Ensure(); m.PlayOneShotInternal(m.clickClip); }
    public static void PlayCoin()   { SoundManager m = Ensure(); m.PlayOneShotInternal(m.coinClip); }
    public static void PlaySplash() { SoundManager m = Ensure(); m.PlayOneShotInternal(m.splashClip); }
    public static void PlayDoor()   { SoundManager m = Ensure(); m.PlayOneShotInternal(m.doorClip); }
    public static void PlayCannon() { SoundManager m = Ensure(); m.PlayOneShotInternal(m.cannonClip, 0.9f); }
    public static void PlayHit()    { SoundManager m = Ensure(); m.PlayOneShotInternal(m.hitClip); }
    public static void PlaySink()   { SoundManager m = Ensure(); m.PlayOneShotInternal(m.sinkClip, 0.85f); }

    // Spustí smyčku hukotu moře (jednou, další volání nic nedělá, pokud už hraje).
    public static void StartWaves()
    {
        SoundManager m = Ensure();
        if (m.ambientSource.isPlaying && m.ambientSource.clip == m.waveClip) return;
        m.ambientSource.clip   = m.waveClip;
        m.ambientSource.volume = m.ambientVolume;
        m.ambientSource.Play();
    }

    // Pomocník pro GUILayout.Button — zahraje klik, když bylo tlačítko zmáčknuté,
    // a vrátí stejnou hodnotu dál (jde tak jen "obalit" původní podmínku).
    public static bool Click(bool pressed)
    {
        if (pressed) PlayClick();
        return pressed;
    }

    void PlayOneShotInternal(AudioClip clip, float scale = 1f)
    {
        if (clip != null) sfxSource.PlayOneShot(clip, sfxVolume * scale);
    }

    // ── Generování klipů (matematicky, žádné soubory) ─────────────────────────

    AudioClip MakeClip(string name, float[] samples, int sampleRate)
    {
        AudioClip clip = AudioClip.Create(name, samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    // Krátké "cvaknutí" pro UI tlačítka — rychle dozníva sinusovka.
    AudioClip MakeClickClip()
    {
        int sr = 22050;
        int n  = Mathf.RoundToInt(sr * 0.05f);
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t   = i / (float)sr;
            float env = 1f - i / (float)n;
            data[i] = Mathf.Sin(2f * Mathf.PI * 1400f * t) * env * 0.5f;
        }
        return MakeClip("ClickSfx", data, sr);
    }

    // Cinknutí mincí — dvě rychle po sobě jdoucí vysoké noty (arpeggio).
    AudioClip MakeCoinClip()
    {
        int sr = 22050;
        int n  = Mathf.RoundToInt(sr * 0.22f);
        int split = n / 2;
        float note1 = 1046.5f, note2 = 1568f; // C6, G6
        float[] data = new float[n];
        for (int i = 0; i < n; i++)
        {
            float t    = i / (float)sr;
            bool first = i < split;
            float freq = first ? note1 : note2;
            int localI = first ? i : i - split;
            float env  = 1f - localI / (float)split;
            data[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.5f;
        }
        return MakeClip("CoinSfx", data, sr);
    }

    // Šplouchnutí při rybaření — filtrovaný šum s rychlým útlumem.
    AudioClip MakeSplashClip()
    {
        int sr = 22050;
        int n  = Mathf.RoundToInt(sr * 0.35f);
        float[] data = new float[n];
        System.Random rng = new System.Random(1);
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            prev = prev * 0.75f + noise * 0.25f; // jednoduchá dolní propust = měkčí "vodnatý" zvuk
            float env = Mathf.Exp(-4f * (i / (float)n));
            data[i] = prev * env * 0.6f;
        }
        return MakeClip("SplashSfx", data, sr);
    }

    // Vrznutí dveří majáku — sinusovka klesající ve frekvenci s lehkým kolísáním.
    AudioClip MakeDoorClip()
    {
        int sr = 22050;
        int n  = Mathf.RoundToInt(sr * 0.6f);
        float[] data = new float[n];
        System.Random rng = new System.Random(2);
        for (int i = 0; i < n; i++)
        {
            float u    = i / (float)n; // 0..1
            float freq = Mathf.Lerp(320f, 140f, u) + (float)(rng.NextDouble() - 0.5) * 12f;
            float phase = 2f * Mathf.PI * freq * (i / (float)sr);
            float env   = Mathf.Sin(Mathf.PI * u) * 0.4f; // narůstá a zase odezní
            data[i] = Mathf.Sin(phase) * env;
        }
        return MakeClip("DoorSfx", data, sr);
    }

    // Dělová rána — tvrdý útok, hluboké dunění (nízká sinusovka) + prásknutí šumu,
    // rychlý útlum.
    AudioClip MakeCannonClip()
    {
        int sr = 22050;
        int n  = Mathf.RoundToInt(sr * 0.4f);
        float[] data = new float[n];
        System.Random rng = new System.Random(11);
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float u   = i / (float)n;
            float t   = i / (float)sr;
            float env = Mathf.Exp(-7f * u);                     // rychle dozní
            float boom = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(85f, 45f, u) * t); // klesající dunění
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            prev = prev * 0.6f + noise * 0.4f;                  // prásknutí (jasnější šum na začátku)
            float crack = prev * Mathf.Exp(-30f * u);
            data[i] = (boom * 0.8f + crack * 0.5f) * env;
        }
        return MakeClip("CannonSfx", data, sr);
    }

    // Zásah ("thunk") — krátký dřevěný úder: střední sinusovka s velmi rychlým
    // útlumem + drobné cvaknutí šumu.
    AudioClip MakeHitClip()
    {
        int sr = 22050;
        int n  = Mathf.RoundToInt(sr * 0.14f);
        float[] data = new float[n];
        System.Random rng = new System.Random(12);
        for (int i = 0; i < n; i++)
        {
            float u   = i / (float)n;
            float t   = i / (float)sr;
            float env = Mathf.Exp(-22f * u);
            float body = Mathf.Sin(2f * Mathf.PI * 165f * t);
            float tick = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-60f * u);
            data[i] = (body * 0.7f + tick * 0.4f) * env * 0.9f;
        }
        return MakeClip("HitSfx", data, sr);
    }

    // Potopení — pomalý klesající sten (sinusovka 200→55 Hz) + bublavý šum,
    // hlasitost nejdřív naroste a pak odezní.
    AudioClip MakeSinkClip()
    {
        int sr = 22050;
        int n  = Mathf.RoundToInt(sr * 1.15f);
        float[] data = new float[n];
        System.Random rng = new System.Random(13);
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float u    = i / (float)n;
            float t    = i / (float)sr;
            float freq = Mathf.Lerp(200f, 55f, u * u);
            float tone = Mathf.Sin(2f * Mathf.PI * freq * t);
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            prev = prev * 0.85f + noise * 0.15f;               // dolní propust = bublání
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u * 1.1f)) * 0.9f;
            data[i] = (tone * 0.6f + prev * 0.5f) * env;
        }
        return MakeClip("SinkSfx", data, sr);
    }

    // Hukot moře na pozadí — filtrovaný šum s pomalým "dýcháním" hlasitosti, smyčka.
    AudioClip MakeWaveClip()
    {
        int sr = 22050;
        int n  = sr * 4; // 4s smyčka
        float[] data = new float[n];
        System.Random rng = new System.Random(3);
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            prev = prev * 0.965f + noise * 0.035f; // silná dolní propust = hukot, ne syčení
            float t     = i / (float)sr;
            float swell = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 0.15f * t);
            data[i] = prev * swell * 2.2f;
        }
        return MakeClip("WaveAmbient", data, sr);
    }
}
