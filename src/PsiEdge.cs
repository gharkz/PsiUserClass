using System;
using System.Collections.Generic;
using HarmonyLib;

namespace PsionClass
{
    // Psionic Edge: a passive trait. +N effective tool hardness when harvesting, +N to learned crafting/utility skills.
    // Always on, free. N = floor(1.52 * ln(Psicraft + 1)).
    internal static class PsiEdge
    {
        internal static readonly int[] LinkedSkills = { 255, 256, 257, 258, 259, 260, 261, 287, 288, 280, 281, 293 }; // crafts + lockpicking/stealing/disarm
        static readonly Dictionary<int, Dictionary<int, int>> links = new Dictionary<int, Dictionary<int, int>>();
        static readonly System.Reflection.MethodInfo modLink = AccessTools.Method(typeof(ElementContainer), "ModLink");

        internal static int N(Chara c) => (int)Math.Floor(1.52 * Math.Log(c.elements.ValueWithoutLink(PsionData.Psicraft) + 1));
        internal static bool Has(Chara c) => c != null && c.elements.ValueWithoutLink(PsionData.PsiEdge) > 0;

        // Keep the trait's displayed value and the skill links in step with Psicraft. Learned skills only.
        internal static void Refresh(Chara c)
        {
            if (!PsionData.IsPsion(c)) return;
            int n = N(c);
            if (c.elements.ValueWithoutLink(PsionData.PsiEdge) != n) c.SetFeat(PsionData.PsiEdge, n, msg: false);
            if (!links.TryGetValue(c.uid, out var have)) links[c.uid] = have = new Dictionary<int, int>();
            foreach (var s in LinkedSkills)
            {
                int want = c.elements.HasBase(s) ? n : 0;
                have.TryGetValue(s, out int cur);
                if (want != cur) { modLink.Invoke(c.elements, new object[] { s, want - cur }); have[s] = want; }
            }
        }
        internal static void Reset() => links.Clear(); // vLink isn't saved; start clean on each load
    }

    [HarmonyPatch(typeof(Game), nameof(Game.Load))]
    static class EdgeResetOnLoad { static void Prefix() => PsiEdge.Reset(); }

    // Harvest: +N effective tool level (free).
    [HarmonyPatch(typeof(BaseTaskHarvest), nameof(BaseTaskHarvest.SetTarget))]
    static class EdgeHarvest
    {
        static void Postfix(BaseTaskHarvest __instance, Chara c)
        {
            try
            {
                if (c == null || !PsiEdge.Has(c) || !c.elements.HasBase(__instance.idEle)) return;
                var t = __instance;
                t.toolLv += PsiEdge.N(c);
                t.efficiency = Math.Max(50, (t.toolLv + 5) * 100 / ((t.reqLv + 5) * 140 / 100));
                t.difficulty = t.IsTooHard ? 3 : t.efficiency > 150 ? 0 : t.efficiency > 100 ? 1 : 2;
            }
            catch (Exception e) { Plugin.Report(e); }
        }
    }
}

namespace PsionClass
{
    // Saves from the stance build list Psionic Edge as an ability; it is a trait now. Drop it before the list is built.
    [HarmonyLib.HarmonyPatch(typeof(CharaAbility), nameof(CharaAbility.Refresh))]
    static class DropOldEdgeAbility
    {
        static void Prefix(CharaAbility __instance)
        {
            __instance.owner?._listAbility?.RemoveAll(x => System.Math.Abs(x) == PsionData.PsiEdge);
        }
    }
}
