using System;
using System.Collections.Generic;
using HarmonyLib;

namespace PsionClass
{
    // Repeats a power on a training dummy until mana (or stamina) runs out, like vanilla weapon practice.
    public class AI_PsiPractice : AI_PracticeDummy
    {
        public int actId;

        public override AIProgress CreateProgress()
        {
            return new Progress_Custom
            {
                canProgress = () => !isFail(),
                onProgress = p =>
                {
                    if (p.progress % 3 != 0) return;
                    var act = ACT.dict.TryGetValue(EClass.sources.elements.map[actId].alias);
                    int cost = act?.GetCost(owner).cost ?? 0;
                    if (act == null || owner.mana.value < Math.Max(1, cost) || owner.stamina.value < 0 || !owner.CanSee(target)) { p.Cancel(); return; }
                    target.animeCounter = 0.01f;
                    owner.UseAbility(act, target, target.pos);
                },
            }.SetDuration(10000);
        }
    }

    [HarmonyPatch(typeof(Chara), nameof(Chara.UseAbility), new[] { typeof(Act), typeof(Card), typeof(Point), typeof(bool) })]
    static class StartPsiPractice
    {
        static void Postfix(Chara __instance, Act a, Card tc, bool __result)
        {
            try
            {
                var pc = __instance;
                if (!__result || !pc.IsPC || !PsionData.IsPsion(pc) || tc == null || !(tc.trait is TraitTrainingDummy)) return;
                if (pc.ai is AI_PsiPractice || a.id != PsionData.Telekinesis) return; // TK only: like thrown weapons; spells stay manual as in vanilla
                pc.SetAI(new AI_PsiPractice { target = tc, actId = a.id });
            }
            catch (Exception e) { Plugin.Report(e); }
        }
    }
}

namespace PsionClass
{
    // Vanilla TK reads the mouse target for the PC; during practice, target the dummy instead (same checks).
    [HarmonyLib.HarmonyPatch(typeof(ActTelekinesis), nameof(ActTelekinesis.CanPerform))]
    static class PracticeTkTarget
    {
        [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.First)]
        static void Postfix(ActTelekinesis __instance, ref bool __result)
        {
            if (!(Act.CC?.ai is AI_PsiPractice ai) || ai.target == null) return;
            var tc = ai.target; Act.TC = tc;
            __result = !tc.pos.Equals(Act.CC.pos) && tc.trait.CanBeHeld && tc.trait.CanBeTeleported && !tc.IsMultisize
                       && __instance.ListPath(Act.CC.pos, tc.pos).Count > 1;
            if (__result) Act.TP.Set(tc.pos);
        }
    }
}
