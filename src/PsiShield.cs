using System;
using HarmonyLib;

namespace PsionClass
{
    // Psi-Shield applies ConHolyShield; mark the instance so it reads as Psi-Shield (refVal2 is saved with the condition).
    [HarmonyPatch(typeof(Act), nameof(Act.Perform), new[] { typeof(Chara), typeof(Card), typeof(Point) })]
    static class PsiShieldMark
    {
        static void Postfix(Act __instance, Chara _cc, Card _tc)
        {
            try
            {
                if (__instance.id != PsionData.PsiShield) return;
                foreach (var who in new[] { _tc as Chara, _cc })
                {
                    if (who == null) continue;
                    foreach (var con in who.conditions) if (con.source.alias == "ConHolyShield") con.refVal2 = PsionData.PsiShield;
                }
            }
            catch (Exception e) { Plugin.Report(e); }
        }
    }

    [HarmonyPatch(typeof(BaseCondition), nameof(BaseCondition.GetText))]
    static class PsiShieldName
    {
        static void Postfix(BaseCondition __instance, ref string __result)
        {
            if (__instance.refVal2 == PsionData.PsiShield && __instance.source?.alias == "ConHolyShield")
                __result = UiText.Pick("Psi-Shield", "サイシールド", "灵能护盾");
        }
    }
}
