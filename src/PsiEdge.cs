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
        // What the trait has added, kept on each skill's own Element object: when the game rebuilds a character's skills
        // (new game start, reload) the fresh Element starts at 0 here too, so the bookkeeping can never drift into a
        // penalty (a per-character table once did: -2 crafting, -4 lockpicking on new characters).
        sealed class Applied { public int N; }
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Element, Applied> links = new System.Runtime.CompilerServices.ConditionalWeakTable<Element, Applied>();
        static readonly System.Reflection.MethodInfo modLink = AccessTools.Method(typeof(ElementContainer), "ModLink");

        internal static int N(Chara c) => (int)Math.Floor(1.52 * Math.Log(c.elements.ValueWithoutLink(PsionData.Psicraft) + 1));
        internal static bool Has(Chara c) => c != null && c.elements.ValueWithoutLink(PsionData.PsiEdge) > 0;

        // Keep the trait's displayed value and the skill links in step with Psicraft. Learned skills only.
        internal static void Refresh(Chara c)
        {
            if (!PsionData.IsPsion(c)) return;
            int n = N(c);
            if (c.elements.ValueWithoutLink(PsionData.PsiEdge) != n) c.SetFeat(PsionData.PsiEdge, n, msg: false);
            foreach (var s in LinkedSkills)
            {
                var e = c.elements.GetElement(s);
                int cur = e != null && links.TryGetValue(e, out var a) ? a.N : 0;
                int want = e != null && c.elements.HasBase(s) ? n : 0;
                if (want == cur) continue;
                modLink.Invoke(c.elements, new object[] { s, want - cur });
                e = c.elements.GetElement(s);
                if (e != null) links.GetOrCreateValue(e).N = want;
            }
        }
        internal static void Reset() { } // kept for the load hook; per-Element tracking needs no clearing
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
