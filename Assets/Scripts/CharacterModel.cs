using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
//  CharacterModel.cs
//  Načte low-poly model postavy (Kenney "Mini Characters", CC0) z
//  Assets/Resources/Characters/ a nasadí mu barevnou atlas-texturu (colormap).
//
//  Používá se místo postaviček složených z primitiv (děda, později hráč).
//  Když model v Resources není, TryBuild vrátí null a volající si postaví
//  postavu ze základních tvarů jako dřív (fallback).
//
//  MĚŘÍTKO / VÝŠKU případně dolaď konstantami tady nebo parametrem `scale`.
// ─────────────────────────────────────────────────────────────────────────────

public static class CharacterModel
{
    // Kenney "Mini Characters" FBX je maličký (~0,68 j vysoký při měřítku 1).
    // 1.6 → model je ve světě ~1,08 j vysoký (o něco menší než původní panáček,
    // aby se postava opticky vešla i do malé veslice).
    public const float DEFAULT_SCALE = 1.6f;

    // Světlá tělová barva — použij jako `skinTint`, když chceš normální obličej
    // i pod sytým barevným oblečením (viz níže). Stejný odstín jako dřívější
    // legacy materiál Assets/Materials/PlayerSkin.mat.
    public static readonly Color LightSkin = new Color(0.92f, 0.78f, 0.66f);

    private static Texture2D colormap;
    private static bool      colormapTried;

    /// <summary>
    /// Vytvoří model postavy jako dítě `parent` (localPosition 0). Vrací instanci,
    /// nebo null, když model "Characters/{modelName}" v Resources není.
    /// `tint` se násobí s texturou (bílá = beze změny, šedá = "starší/vybledlý")
    /// a normálně platí pro CELÝ model (tělo i obličej — colormap je jedna
    /// sdílená atlas textura, žádné oddělené UV pro kůži).
    /// `skinTint` (nepovinné): když je zadaný, dostane síťku "head-mesh" (obličej)
    /// TENHLE odstín místo `tint` — ať sytě barevné oblečení (modrá/červená
    /// P1/P2) nezabarví i kůži do stejné barvy. Použij `LightSkin`.
    /// `controllerName` = jméno Animator Controlleru v Resources/Characters
    /// (např. "PlayerAnim" nebo "SitAnim"); null = bez animací (statická póza).
    /// </summary>
    public static GameObject TryBuild(Transform parent, string modelName, float scale, Color tint,
                                      string controllerName = null, Color? skinTint = null)
    {
        var prefab = Resources.Load<GameObject>("Characters/" + modelName);
        if (prefab == null) return null;

        var go = Object.Instantiate(prefab, parent, false);
        go.name = "CharModel";
        go.transform.localPosition    = Vector3.zero;
        go.transform.localEulerAngles = Vector3.zero;

        // Cílem je, aby model měl VE SVĚTĚ rovnoměrné měřítko `scale` bez ohledu na
        // to, jak (i nerovnoměrně) je zmenšený/zvětšený rodič — interiérová
        // postavička je např. scale (0,5, 0,55, 0,5), tak dělíme každou osu zvlášť,
        // ať model není zploštělý.
        Vector3 pl = parent != null ? parent.lossyScale : Vector3.one;
        go.transform.localScale = new Vector3(
            scale / Mathf.Max(0.0001f, pl.x),
            scale / Mathf.Max(0.0001f, pl.y),
            scale / Mathf.Max(0.0001f, pl.z));

        if (!colormapTried)
        {
            colormapTried = true;
            colormap = Resources.Load<Texture2D>("Characters/colormap");
        }

        Shader sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        var mat = new Material(sh) { name = "CharColormap (runtime)" };
        if (colormap != null)
        {
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", colormap);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", colormap);
        }
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", tint);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color", tint);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.1f);

        // Volitelný samostatný materiál pro obličej (viz `skinTint` v komentáři výš).
        Material headMat = null;
        if (skinTint.HasValue)
        {
            headMat = new Material(sh) { name = "CharColormap-Head (runtime)" };
            if (colormap != null)
            {
                if (headMat.HasProperty("_BaseMap")) headMat.SetTexture("_BaseMap", colormap);
                if (headMat.HasProperty("_MainTex")) headMat.SetTexture("_MainTex", colormap);
            }
            if (headMat.HasProperty("_BaseColor")) headMat.SetColor("_BaseColor", skinTint.Value);
            if (headMat.HasProperty("_Color"))     headMat.SetColor("_Color", skinTint.Value);
            if (headMat.HasProperty("_Smoothness")) headMat.SetFloat("_Smoothness", 0.1f);
        }

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            bool isHead = headMat != null && r.gameObject.name.ToLowerInvariant().Contains("head");
            r.sharedMaterial     = isHead ? headMat : mat;
            r.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);

        // Animace: přiřaď Animator Controller z Resources (loco / sit …). Root
        // motion vypnutý — pozici řídíme sami, animace jen "hraje na místě".
        var anim = go.GetComponentInChildren<Animator>(true);
        if (anim != null)
        {
            if (!string.IsNullOrEmpty(controllerName))
            {
                var ctrl = Resources.Load<RuntimeAnimatorController>("Characters/" + controllerName);
                if (ctrl != null)
                {
                    anim.runtimeAnimatorController = ctrl;
                    anim.applyRootMotion = false;
                    anim.enabled = true;
                }
                else anim.enabled = false; // controller nenalezen → radši statická póza
            }
            else anim.enabled = false;
        }

        return go;
    }

    /// <summary>Animator na modelu vytvořeném přes TryBuild (nebo null).</summary>
    public static Animator GetAnimator(GameObject model)
        => model != null ? model.GetComponentInChildren<Animator>(true) : null;
}
