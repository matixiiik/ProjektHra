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
    // Postavičky ve hře jsou ~1,2 j vysoké → zvětšíme ~2,7×.
    public const float DEFAULT_SCALE = 1.85f;

    private static Texture2D colormap;
    private static bool      colormapTried;

    /// <summary>
    /// Vytvoří model postavy jako dítě `parent` (localPosition 0). Vrací instanci,
    /// nebo null, když model "Characters/{modelName}" v Resources není.
    /// `tint` se násobí s texturou (bílá = beze změny, šedá = "starší/vybledlý").
    /// `controllerName` = jméno Animator Controlleru v Resources/Characters
    /// (např. "PlayerAnim" nebo "SitAnim"); null = bez animací (statická póza).
    /// </summary>
    public static GameObject TryBuild(Transform parent, string modelName, float scale, Color tint,
                                      string controllerName = null)
    {
        var prefab = Resources.Load<GameObject>("Characters/" + modelName);
        if (prefab == null) return null;

        var go = Object.Instantiate(prefab, parent, false);
        go.name = "CharModel";
        go.transform.localPosition    = Vector3.zero;
        go.transform.localEulerAngles = Vector3.zero;
        go.transform.localScale       = Vector3.one * scale;

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

        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.sharedMaterial     = mat;
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
