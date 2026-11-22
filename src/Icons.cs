using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using UnityEngine;

namespace PsionClass
{
    internal static class Icons
    {
        internal static readonly Dictionary<int, Sprite> ById = new Dictionary<int, Sprite>();
        internal static void Load(string dir)
        {
            Add(PsionData.PsiShield, Path.Combine(dir, "assets", "psishield.png"));
            Add(PsionData.PsiTeleport, Path.Combine(dir, "assets", "psiteleport.png"));
            Add(PsionData.PsiHurl, Path.Combine(dir, "assets", "psihurl.png"));
            Add(PsionData.PsiStrike, Path.Combine(dir, "assets", "psistrike.png"));
        }
        static void Add(int id, string path)
        {
            if (!File.Exists(path)) return;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            ImageConversion.LoadImage(tex, File.ReadAllBytes(path));
            ById[id] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(.5f, .5f), 100);
        }
    }

    // Ability icons: every path (ability window, hotbar, tooltips) resolves through SourceElement.Row.GetSprite,
    // which looks the alias up on Elin's icon sheet and falls back to a generic icon for our new aliases.
    [HarmonyPatch(typeof(SourceElement.Row), nameof(SourceElement.Row.GetSprite))]
    static class AbilityIcon
    {
        static void Postfix(SourceElement.Row __instance, ref Sprite __result)
        {
            if (Icons.ById.TryGetValue(__instance.id, out var s)) __result = s;
        }
    }

    // Side-list buff icon for psionic-cast shields.
    [HarmonyPatch(typeof(BaseStats), nameof(BaseStats.GetSprite))]
    static class PsiShieldBuffIcon
    {
        static void Postfix(BaseStats __instance, ref Sprite __result)
        {
            if (__instance is BaseCondition c && c.refVal2 == PsionData.PsiShield && c.source?.alias == "ConHolyShield"
                && Icons.ById.TryGetValue(PsionData.PsiShield, out var s)) __result = s;
        }
    }
}
