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
                    // one cast per turn, like vanilla weapon practice
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
                if (pc.ai is AI_PsiPractice || (a.id != PsionData.Telekinesis && a.id != PsionData.PsiStrike)) return; // TK and Psi-Strike (a melee swing): like weapon practice; spells stay manual as in vanilla
                pc.SetAI(new AI_PsiPractice { target = tc, actId = a.id });
            }
            catch (Exception e) { Plugin.Report(e); }
        }
    }
}

namespace PsionClass
{
    // Vanilla TK reads the mouse target for the PC; in practice and auto-combat, target the AI's target instead (same checks).
    [HarmonyLib.HarmonyPatch(typeof(ActTelekinesis), nameof(ActTelekinesis.CanPerform))]
    static class PracticeTkTarget
    {
        [HarmonyLib.HarmonyPriority(HarmonyLib.Priority.First)]
        static void Postfix(ActTelekinesis __instance, ref bool __result)
        {
            // Vanilla TK reads the mouse target for the PC. In practice use the dummy; in auto-combat use the AI's
            // current enemy (otherwise TK almost always fails CanPerform and auto-combat falls back to melee).
            // A pull retarget (TelekinesisAutoMode) wins over the combat target while it checks a ranged foe.
            Card tc = TelekinesisAutoMode.Aim ?? (Act.CC?.ai is AI_PsiPractice ai ? ai.target : (Act.CC != null && Act.CC.IsPC && Act.CC.ai is GoalCombat gc ? gc.tc : null));
            if (tc == null) return;
            Act.TC = tc;
            __result = !tc.pos.Equals(Act.CC.pos) && tc.trait.CanBeHeld && tc.trait.CanBeTeleported && !tc.IsMultisize
                       && __instance.ListPath(Act.CC.pos, tc.pos).Count > 1;
            if (__result) Act.TP.Set(tc.pos);
        }
    }
}
