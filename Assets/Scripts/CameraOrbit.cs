using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  CameraOrbit.cs
//  Kamera obíhá kolem hráče. Drž PRAVÉ tlačítko myši a hýbej myší → kamera
//  se otáčí (vodorovně i svisle, svisle omezeno). Bez držení drží poslední úhel.
//
//  KOLEČKO MYŠI = zoom. Rolováním od sebe se kamera přibližuje; když se přiblíží
//  moc, hra se přepne do POHLEDU Z PRVNÍ OSOBY (kamera v očích hráče, jeho model
//  se schová). Rolováním k sobě se zase oddálí zpět (maximum = výchozí vzdálenost).
//
//  Je na objektu "Main Camera", který je potomkem hráče. Hráč se neotáčí, takže
//  lokální prostor = světový (co do rotace). Pivot = počátek hráče + malá výška.
// ─────────────────────────────────────────────────────────────────────────────

public class CameraOrbit : MonoBehaviour
{
    [Tooltip("Vzdálenost kamery od hráče (při startu = nejvzdálenější zoom).")]
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

    [Tooltip("Výška očí nad počátkem hráče v pohledu z první osoby.")]
    public float firstPersonHeight = 1.3f;

    // Co se v první osobě NESMÍ schovat, i když to leží pod hráčem (kamera je jeho potomek) —
    // puška v ruce na obrazovce (viz PlayerController.BuildFirstPersonWeapon).
    [HideInInspector] public Transform keepVisibleRoot;

    private Vector3 lastMouse;

    // ── Zoom + první osoba ───────────────────────────────────────────────────
    private const float MIN_THIRD_DISTANCE = 3f;    // nejblíž ve třetí osobě; blíž už = první osoba
    private const float ZOOM_PER_NOTCH     = 0.16f; // o kolik % se změní vzdálenost jedním cvaknutím kolečka
    private const float FP_MIN_PITCH       = -70f;  // v první osobě jde koukat i nahoru
    private const float FP_MAX_PITCH       = 80f;

    private float maxDistance;      // nejvzdálenější zoom (výchozí vzdálenost z Inspectoru)
    private float targetDistance;   // kam se kamera plynule blíží
    private bool  firstPerson;      // true = pohled z očí hráče
    private bool  zoomReady;

    private PlayerController owner;                                  // hráč, ke kterému kamera patří
    private readonly List<Renderer> hiddenRenderers = new List<Renderer>(); // co jsme v první osobě schovali
    private readonly List<Renderer> scanBuffer      = new List<Renderer>(); // pomocný seznam (bez alokací každý snímek)
    private float nextScanTime;                                             // kdy znovu schovat nově zapnuté části modelu

    /// <summary>Nejvzdálenější zoom (výchozí vzdálenost) — z něj vychází i kamera hráče 2.</summary>
    public float MaxDistance => zoomReady ? maxDistance : distance;

    /// <summary>Je zrovna zapnutý pohled z první osoby?</summary>
    public bool IsFirstPerson => firstPerson;

    void LateUpdate()
    {
        if (!zoomReady)
        {
            maxDistance    = Mathf.Max(distance, MIN_THIRD_DISTANCE + 1f);
            targetDistance = maxDistance;
            owner          = GetComponentInParent<PlayerController>();
            zoomReady      = true;
        }

        // Neotáčej, když je otevřené menu / obchod / konzole / mapa / deník (myš tam klikáš).
        bool uiBlocking = MainMenuManager.IsVisible
                       || GameConsole.IsOpen
                       || UpgradeShopManager.AnyShopOpen
                       || MapScreen.IsOpen
                       || JournalScreen.IsOpen
                       || LetterScreen.IsOpen;

        if (!uiBlocking)
        {
            HandleZoom();

            if (Input.GetMouseButtonDown(1))
                lastMouse = Input.mousePosition;

            if (Input.GetMouseButton(1))
            {
                Vector3 delta = Input.mousePosition - lastMouse;
                yaw  += delta.x * sensitivity;
                pitch = Mathf.Clamp(pitch - delta.y * sensitivity,
                                    firstPerson ? FP_MIN_PITCH : minPitch,
                                    firstPerson ? FP_MAX_PITCH : maxPitch);
                lastMouse = Input.mousePosition;
            }
        }

        // V první osobě občas schovej nově zapnuté části modelu (nasednutí do lodě apod.).
        if (firstPerson && Time.unscaledTime >= nextScanTime)
        {
            nextScanTime = Time.unscaledTime + 0.25f;
            HideOwnRenderers();
        }

        // Plynulé přibližování (unscaled, ať zoom funguje i při pauze her).
        float shown = firstPerson ? 0f : targetDistance;
        distance = Mathf.Lerp(distance, shown, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
        if (Mathf.Abs(distance - shown) < 0.01f) distance = shown;

        // Vrácení z první osoby do třetí: pitch vrať do povoleného rozsahu.
        if (!firstPerson) pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 pivot  = new Vector3(0f, firstPerson ? firstPersonHeight : pivotHeight, 0f);

        transform.localRotation = rot;
        transform.localPosition = pivot + rot * new Vector3(0f, 0f, -distance);
    }

    // Kolečko: dopředu (od sebe) = přiblížit, dozadu = oddálit.
    void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) < 0.01f) return;

        if (firstPerson)
        {
            // Oddalování z první osoby → zpátky do třetí (na malou vzdálenost).
            if (scroll < 0f)
            {
                SetFirstPerson(false);
                targetDistance = MIN_THIRD_DISTANCE * 1.4f;
            }
            return;
        }

        targetDistance *= 1f - scroll * ZOOM_PER_NOTCH;

        if (targetDistance < MIN_THIRD_DISTANCE)
        {
            // Přiblížil ses "moc" → první osoba.
            targetDistance = MIN_THIRD_DISTANCE;
            SetFirstPerson(true);
        }
        else if (targetDistance > maxDistance)
        {
            targetDistance = maxDistance; // dál než výchozí se oddálit nedá
        }
    }

    // Zapne / vypne pohled z první osoby: schová / vrátí model hráče (loď i panáček).
    void SetFirstPerson(bool on)
    {
        if (firstPerson == on) return;
        firstPerson = on;

        if (on)
        {
            hiddenRenderers.Clear();
            HideOwnRenderers();
            pitch = Mathf.Clamp(pitch, FP_MIN_PITCH, FP_MAX_PITCH);
        }
        else
        {
            RestoreHiddenRenderers();
        }
    }

    // Schová všechno, co je právě vidět z těla hráče (loď, panáček, držené věci).
    // Volá se při vstupu do první osoby a pak každých 0,25 s — po nasednutí/vystoupení
    // se totiž zapnou nové části modelu.
    void HideOwnRenderers()
    {
        if (owner == null) return;

        owner.GetComponentsInChildren(false, scanBuffer);
        foreach (var r in scanBuffer)
        {
            // Nechej na pokoji čáry (kroužek práce), stopy a částice — to není tělo hráče.
            if (keepVisibleRoot != null && r.transform.IsChildOf(keepVisibleRoot)) continue; // viewmodel zbraně
            if (r.enabled && !(r is LineRenderer) && !(r is TrailRenderer) && !(r is ParticleSystemRenderer))
            {
                r.enabled = false;
                hiddenRenderers.Add(r);
            }
        }
        scanBuffer.Clear();
    }

    void RestoreHiddenRenderers()
    {
        foreach (var r in hiddenRenderers)
            if (r != null) r.enabled = true;
        hiddenRenderers.Clear();
    }

    void OnDisable()
    {
        // Kdyby se kamera vypnula uprostřed první osoby, ať hráč nezůstane neviditelný.
        if (firstPerson)
        {
            firstPerson = false;
            RestoreHiddenRenderers();
        }
    }
}
