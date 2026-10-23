using System.Collections.Generic;
using UnityEngine;

/// <summary>One-time, instance-owned audience colours; authored skin, faces and textures stay intact.</summary>
public sealed partial class SimBarCharacterVisual
{
    private static readonly Color[] AudienceTopColours =
    {
        new Color(0.16f, 0.48f, 0.48f), new Color(0.75f, 0.53f, 0.17f),
        new Color(0.65f, 0.25f, 0.22f), new Color(0.18f, 0.29f, 0.48f),
        new Color(0.37f, 0.51f, 0.35f), new Color(0.46f, 0.28f, 0.49f),
        new Color(0.77f, 0.72f, 0.59f), new Color(0.24f, 0.28f, 0.32f)
    };
    private static readonly Color[] AudienceTrouserColours =
    {
        new Color(0.11f, 0.16f, 0.25f), new Color(0.27f, 0.31f, 0.35f),
        new Color(0.23f, 0.35f, 0.48f), new Color(0.32f, 0.25f, 0.17f),
        new Color(0.28f, 0.33f, 0.22f), new Color(0.085f, 0.095f, 0.11f),
        new Color(0.33f, 0.17f, 0.19f), new Color(0.46f, 0.47f, 0.48f)
    };
    private static readonly Color[] AudienceHairColours =
    {
        new Color(0.07f, 0.065f, 0.06f), new Color(0.19f, 0.105f, 0.055f),
        new Color(0.35f, 0.12f, 0.055f), new Color(0.32f, 0.22f, 0.13f),
        new Color(0.58f, 0.43f, 0.25f), new Color(0.72f, 0.64f, 0.47f),
        new Color(0.40f, 0.42f, 0.44f), new Color(0.065f, 0.09f, 0.13f)
    };

    private Dictionary<Material, Material>[] audienceTintCaches;
    private bool audienceAppearanceApplied;

    private void ApplyAudienceAppearance(Color outfit)
    {
        if (role != "guest" || model == null || audienceAppearanceApplied) return;
        audienceAppearanceApplied = true;
        audienceTintCaches = new Dictionary<Material, Material>[4];
        for (int i = 0; i < audienceTintCaches.Length; i++) audienceTintCaches[i] = new Dictionary<Material, Material>();

        int selection = Mathf.Abs(variant % AudienceTopColours.Length);
        Color top = Color.Lerp(AudienceTopColours[(selection * 3 + 1) % AudienceTopColours.Length], outfit, 0.18f);
        Color trousers = AudienceTrouserColours[(selection * 5 + 3) % AudienceTrouserColours.Length];
        int hairSelection = Mathf.Abs((variant * 7 + variant / 6) % AudienceHairColours.Length);
        Color hair = AudienceHairColours[hairSelection];
        Color shoes = Color.Lerp(trousers, new Color(0.12f, 0.13f, 0.15f), 0.65f);
        Color[] colours = { top, trousers, shoes, hair };

        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            Material[] slots = renderer.sharedMaterials;
            bool changed = false;
            for (int slot = 0; slot < slots.Length; slot++)
            {
                Material source = slots[slot];
                int region = AudienceMaterialRegion(renderer, source);
                if (region < 0) continue;
                Dictionary<Material, Material> cache = audienceTintCaches[region];
                if (!cache.TryGetValue(source, out Material tinted))
                {
                    // A separate region cache also permits a suit's shared source
                    // material to have different jacket and trouser colours.
                    tinted = new Material(source) { name = "Audience_" + variant + "_" + region + "_" + source.name };
                    Color original = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor")
                        : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                    Color colour = Color.Lerp(original, colours[region], region == 3 ? 0.94f : region == 2 ? 0.52f : 0.76f);
                    colour.a = original.a;
                    if (tinted.HasProperty("_BaseColor")) tinted.SetColor("_BaseColor", colour);
                    if (tinted.HasProperty("_Color")) tinted.SetColor("_Color", colour);
                    cache.Add(source, tinted);
                    ownedMaterials.Add(tinted);
                }
                slots[slot] = tinted;
                changed = true;
            }
            if (changed) renderer.sharedMaterials = slots;
        }
    }

    private static int AudienceMaterialRegion(Renderer renderer, Material source)
    {
        if (source == null) return -1;
        string materialName = source.name.ToLowerInvariant();
        if (materialName.Contains("skin") || materialName.Contains("eye") || materialName.Contains("teeth")
            || materialName.Contains("mouth") || materialName.Contains("gold") || materialName.Contains("metal")
            || materialName.Contains("chrome")) return -1;
        string part = renderer.name.ToLowerInvariant();
        if (part.Contains("head"))
            return materialName.Contains("hair") || materialName.Contains("moustache") || materialName.Contains("beard") ? 3 : -1;
        if (part.Contains("body")) return 0;
        if (part.Contains("leg") || part.Contains("pants")) return 1;
        if (part.Contains("feet")) return 2;
        return -1;
    }
}
