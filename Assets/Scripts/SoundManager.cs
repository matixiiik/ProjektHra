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
    private AudioClip handgunClip;
    private AudioClip thunderClip;
    private AudioClip chordClip;
    private AudioClip rainClip;

    // ── Hudba (fáze B příběhu) — smyčky akordů generované kódem, viz MakeMusicLoop.
    // Dvě AudioSource kvůli prolínání: jedna doznívá, druhá nastupuje.
    public const int MUSIC_NONE = 0, MUSIC_PIRATE = 1, MUSIC_GRAVEYARD = 2, MUSIC_FINAL = 3, MUSIC_BOSS = 4, MUSIC_ENDING = 5;
    [Range(0f, 1f)] public float musicVolume = 0.13f;
    private AudioSource musicA, musicB;      // musicA = právě hrající, musicB = doznívající
    private AudioSource rainSource;          // šum deště (bouře u Posledního ostrova)
    private float       rainTarget;          // 0..1, jak silně má déšť šumět
    private int         currentTrack = MUSIC_NONE;
    private readonly AudioClip[] musicClips = new AudioClip[6]; // generují se, až jsou poprvé potřeba
    private const float MUSIC_FADE = 2.5f;   // sekundy prolínání skladeb

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

        musicA     = gameObject.AddComponent<AudioSource>();
        musicB     = gameObject.AddComponent<AudioSource>();
        rainSource = gameObject.AddComponent<AudioSource>();
        foreach (var src in new[] { musicA, musicB, rainSource })
        {
            src.playOnAwake = false;
            src.loop        = true;
            src.volume      = 0f;
        }

        clickClip  = MakeClickClip();
        coinClip   = MakeCoinClip();
        splashClip = MakeSplashClip();
        doorClip   = MakeDoorClip();
        waveClip   = MakeWaveClip();
        cannonClip  = MakeCannonClip();
        hitClip     = MakeHitClip();
        sinkClip    = MakeSinkClip();
        handgunClip = MakeHandgunClip();
        thunderClip = MakeThunderClip();
        chordClip   = MakeChordClip();
        rainClip    = MakeRainClip();
    }

    // Plynulé prolínání hudby a deště (unscaledDeltaTime — v pauze ať hudba nezamrzne v půlce).
    void Update()
    {
        float step = Time.unscaledDeltaTime / MUSIC_FADE * musicVolume;
        float targetA = currentTrack == MUSIC_NONE ? 0f : musicVolume;
        musicA.volume = Mathf.MoveTowards(musicA.volume, targetA, step);
        musicB.volume = Mathf.MoveTowards(musicB.volume, 0f,      step);
        if (musicB.isPlaying && musicB.volume <= 0.0001f) musicB.Stop();

        float rainVol = rainTarget * 0.32f;
        rainSource.volume = Mathf.MoveTowards(rainSource.volume, rainVol, Time.unscaledDeltaTime * 0.2f);
        if (rainVol > 0f && !rainSource.isPlaying) { rainSource.clip = rainClip; rainSource.Play(); }
        if (rainVol <= 0f && rainSource.isPlaying && rainSource.volume <= 0.0001f) rainSource.Stop();
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
    public static void PlayHandgun(){ SoundManager m = Ensure(); m.PlayOneShotInternal(m.handgunClip, 0.75f); }
    public static void PlayThunder(float loudness) { SoundManager m = Ensure(); m.PlayOneShotInternal(m.thunderClip, Mathf.Clamp01(loudness)); }
    public static void PlayChapterChord() { SoundManager m = Ensure(); m.PlayOneShotInternal(m.chordClip, 0.8f); }

    /// <summary>Přepne hudbu na skladbu `track` (MUSIC_…) s prolnutím. Volat se smí každý
    /// snímek — když už ta skladba hraje, nic se nestane.</summary>
    public static void SetMusic(int track)
    {
        SoundManager m = Ensure();
        if (track == m.currentTrack) return;
        m.currentTrack = track;

        // Dosud hrající skladba se stane "doznívající" a nová začne potichu od nuly.
        AudioSource old = m.musicA;
        m.musicA = m.musicB;
        m.musicB = old;
        m.musicA.Stop();
        if (track == MUSIC_NONE) return;

        m.musicA.clip   = m.GetMusicClip(track);
        m.musicA.volume = 0f;
        m.musicA.Play();
    }

    /// <summary>Jak silně má šumět déšť (0 = vůbec, 1 = liják). Prolíná se samo.</summary>
    public static void SetRain(float amount) { Ensure().rainTarget = Mathf.Clamp01(amount); }

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

    // Výstřel pěší zbraně (pistole) — mnohem kratší a ostřejší než dělová rána,
    // žádné hluboké dunění, jen rychlé "prásk" (jasný šum) + krátký klesající tón.
    AudioClip MakeHandgunClip()
    {
        int sr = 22050;
        int n  = Mathf.RoundToInt(sr * 0.12f);
        float[] data = new float[n];
        System.Random rng = new System.Random(14);
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float u    = i / (float)n;
            float t    = i / (float)sr;
            float env  = Mathf.Exp(-26f * u);                                  // rychlý útlum, kratší než dělo
            float crack = (float)(rng.NextDouble() * 2.0 - 1.0);
            prev = prev * 0.5f + crack * 0.5f;                                 // méně filtrovaný šum = jasnější "prásk"
            float tone = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(950f, 380f, u) * t);
            data[i] = (prev * 0.75f + tone * 0.4f) * env;
        }
        return MakeClip("HandgunSfx", data, sr);
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

    // ── Hudba a bouře (fáze B) ────────────────────────────────────────────────

    // Frekvence tónu podle MIDI čísla (69 = A4 = 440 Hz, o 12 výš = o oktávu výš).
    static float Note(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

    // Skladba se vygeneruje až ve chvíli, kdy je poprvé potřeba (ať start hry nezdržuje).
    AudioClip GetMusicClip(int track)
    {
        if (musicClips[track] != null) return musicClips[track];
        switch (track)
        {
            // Pirátský ostrov: moll, "námořnická" — Am F G Em, rozložené akordy + basa.
            case MUSIC_PIRATE:
                musicClips[track] = MakeMusicLoop("MusicPirate",
                    new[] { new[] { 57, 60, 64 }, new[] { 53, 57, 60 }, new[] { 55, 59, 62 }, new[] { 52, 55, 59 } },
                    3f, 4, true, false, 0.8f);
                break;
            // Hřbitov lodí: pomalé tajemné plochy bez rytmu — Dm Bb Gm A.
            case MUSIC_GRAVEYARD:
                musicClips[track] = MakeMusicLoop("MusicGraveyard",
                    new[] { new[] { 50, 53, 57, 62 }, new[] { 46, 50, 53, 58 }, new[] { 43, 50, 55, 58 }, new[] { 45, 49, 52, 57 } },
                    4.5f, 0, false, false, 1f);
                break;
            // Poslední ostrov (bouře): tíživé Cm Ab Bb G, pulzující basa.
            case MUSIC_FINAL:
                musicClips[track] = MakeMusicLoop("MusicFinal",
                    new[] { new[] { 48, 55, 60, 63 }, new[] { 44, 51, 56, 60 }, new[] { 46, 53, 58, 62 }, new[] { 43, 50, 55, 59 } },
                    3.5f, 2, true, false, 0.9f);
                break;
            // Souboj s bossem: rychlé Em C D B, osminové arpeggio + bubny.
            case MUSIC_BOSS:
                musicClips[track] = MakeMusicLoop("MusicBoss",
                    new[] { new[] { 52, 55, 59 }, new[] { 48, 52, 55 }, new[] { 50, 54, 57 }, new[] { 47, 51, 54 } },
                    2f, 8, true, true, 0.7f);
                break;
            // Závěr (titulky): klidné C G Am F.
            default:
                musicClips[track] = MakeMusicLoop("MusicEnding",
                    new[] { new[] { 48, 55, 60, 64 }, new[] { 43, 55, 59, 62 }, new[] { 45, 57, 60, 64 }, new[] { 41, 53, 57, 60 } },
                    4f, 2, false, false, 1f);
                break;
        }
        return musicClips[track];
    }

    /// <summary>
    /// Smyčka akordů. Každý akord hraje `chordSeconds` a skládá se z:
    ///   • plochy ("pad") — tóny akordu jako tiché sinusovky s měkkým náběhem; doznívá
    ///     ještě kousek do dalšího akordu (index se na konci smyčky přetočí na začátek,
    ///     takže smyčka nemá slyšitelný šev);
    ///   • arpeggia — `arpPerChord` krátkých "brnknutí" po tónech akordu (0 = žádné);
    ///   • basy — kořenový tón o oktávu níž na každou dobu (když `bass`);
    ///   • bubnů — krátké údery na každou dobu (když `drums`).
    /// </summary>
    AudioClip MakeMusicLoop(string name, int[][] chords, float chordSeconds, int arpPerChord, bool bass, bool drums, float padLevel)
    {
        int sr = 22050;
        int chordLen = Mathf.RoundToInt(chordSeconds * sr);
        int n = chordLen * chords.Length;
        float[] data = new float[n];
        System.Random rng = new System.Random(name.Length * 31);

        for (int c = 0; c < chords.Length; c++)
        {
            int start = c * chordLen;
            int[] chord = chords[c];

            // Plocha: náběh 0,5 s, doznění 0,8 s přes začátek dalšího akordu.
            int padLen = chordLen + Mathf.RoundToInt(0.8f * sr);
            for (int i = 0; i < padLen; i++)
            {
                float t   = i / (float)sr;
                float env = Mathf.Min(1f, t / 0.5f) * Mathf.Clamp01((padLen - i) / (0.8f * sr));
                float vib = 1f + 0.003f * Mathf.Sin(2f * Mathf.PI * 4.5f * t); // jemné vibrato = "živější" zvuk
                float v = 0f;
                foreach (int note in chord)
                {
                    float f = Note(note);
                    v += Mathf.Sin(2f * Mathf.PI * f * vib * t) + 0.25f * Mathf.Sin(4f * Mathf.PI * f * t);
                }
                data[(start + i) % n] += v / chord.Length * env * 0.22f * padLevel;
            }

            // Arpeggio: brnknutí s rychlým útlumem, tóny akordu o oktávu výš.
            if (arpPerChord > 0)
            {
                int stepLen = chordLen / arpPerChord;
                for (int k = 0; k < arpPerChord; k++)
                {
                    float f = Note(chord[k % chord.Length] + 12);
                    int len = Mathf.Min(stepLen * 2, Mathf.RoundToInt(0.6f * sr));
                    for (int i = 0; i < len; i++)
                    {
                        float t = i / (float)sr;
                        data[(start + k * stepLen + i) % n] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-7f * t) * 0.16f;
                    }
                }
            }

            // Basa a bubny na každou dobu (4 doby na akord).
            int beatLen = chordLen / 4;
            for (int b = 0; b < 4; b++)
            {
                int bStart = start + b * beatLen;
                if (bass)
                {
                    float f = Note(chord[0] - 12);
                    for (int i = 0; i < beatLen; i++)
                    {
                        float t = i / (float)sr;
                        data[(bStart + i) % n] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-4f * t) * 0.22f;
                    }
                }
                if (drums)
                {
                    bool kick = b % 2 == 0; // 1. a 3. doba = basový buben, 2. a 4. = šumová "virblová" rána
                    int len = Mathf.RoundToInt((kick ? 0.18f : 0.09f) * sr);
                    for (int i = 0; i < len; i++)
                    {
                        float t = i / (float)sr;
                        float env = Mathf.Exp(-(kick ? 20f : 40f) * t);
                        float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                        float hit = kick ? Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(110f, 45f, t * 5f) * t) : noise * 0.5f;
                        data[(bStart + i) % n] += hit * env * 0.3f;
                    }
                }
            }
        }

        // Pojistka proti přebuzení (součet hlasů může přelézt ±1).
        float peak = 0f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        if (peak > 0.95f) for (int i = 0; i < n; i++) data[i] *= 0.95f / peak;

        return MakeClip(name, data, sr);
    }

    // Hrom — dlouhé hluboké dunění: silně filtrovaný šum, prudký náraz a pomalé doznění.
    AudioClip MakeThunderClip()
    {
        int sr = 22050;
        int n  = Mathf.RoundToInt(sr * 2.8f);
        float[] data = new float[n];
        System.Random rng = new System.Random(21);
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float u = i / (float)n;
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            prev = prev * 0.97f + noise * 0.03f;                          // hodně tlumené = hluboké
            float env  = Mathf.Min(1f, u * 25f) * Mathf.Exp(-2.2f * u);    // rychlý náraz, dlouhé doznění
            float roll = 0.7f + 0.3f * Mathf.Sin(2f * Mathf.PI * 3f * u);  // "převalování" hromu
            data[i] = Mathf.Clamp(prev * env * roll * 6f, -1f, 1f);
        }
        return MakeClip("ThunderSfx", data, sr);
    }

    // Akord úvodní karty kapitoly — rozložený dur (C E G C), každý tón dozní.
    AudioClip MakeChordClip()
    {
        int sr = 22050;
        int n  = Mathf.RoundToInt(sr * 3f);
        float[] data = new float[n];
        int[] notes = { 60, 64, 67, 72 };
        for (int k = 0; k < notes.Length; k++)
        {
            int start = Mathf.RoundToInt(k * 0.12f * sr); // tóny nastupují kousek po sobě
            float f = Note(notes[k]);
            for (int i = start; i < n; i++)
            {
                float t = (i - start) / (float)sr;
                float env = Mathf.Min(1f, t / 0.03f) * Mathf.Exp(-1.4f * t);
                data[i] += (Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * f * t)) * env * 0.16f;
            }
        }
        return MakeClip("ChapterChord", data, sr);
    }

    // Déšť — jasný šum (méně filtrovaný než moře) s občasnou hlasitější kapkou, smyčka.
    AudioClip MakeRainClip()
    {
        int sr = 22050;
        int n  = sr * 3;
        float[] data = new float[n];
        System.Random rng = new System.Random(22);
        float prev = 0f;
        for (int i = 0; i < n; i++)
        {
            float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
            prev = prev * 0.55f + noise * 0.45f;
            float drop = rng.NextDouble() < 0.0008 ? 1.5f : 1f;
            data[i] = prev * 0.5f * drop;
        }
        return MakeClip("RainAmbient", data, sr);
    }
}
