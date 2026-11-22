using System;
using HarmonyLib;
using PsionClass;

// Psi-Strike: your ordinary melee attack carried to a distant target by the mind (a thrown blade called back).
// It is the game's own melee attack with weapon reach lifted: same weapons or martial arts, same damage, same skill XP.
public class ActPsiStrike : Spell
{
    internal const int Range = 8;
    public override int PerformDistance => Range;
    public override bool IsHostileAct => true;

    public override bool CanPerform()
    {
        var cc = Act.CC;
        if (cc == null) return false;
        if (cc.IsPC && !(cc.ai is GoalCombat)) Act.TC = EClass.scene.mouseTarget.card;
        var tc = Act.TC;
        if (tc == null || !tc.isChara || tc == cc || !tc.IsAliveInCurrentZone) return false;
        int d = cc.Dist(tc);
        if (d > Range || !cc.CanSeeLos(tc)) return false;
        if (!cc.IsPC || cc.ai is GoalCombat)
        {
            if (d <= cc.body.GetMeleeDistance()) return false; // in reach: AutoCombat swings normally
        }
        Act.TP.Set(tc.pos);
        return true;
    }

    public override bool Perform()
    {
        var cc = Act.CC; var tc = Act.TC;
        if (cc == null || tc == null) return false;
        PsionCombat.SoundAct = PsionData.PsiStrike;
        try { cc.PlaySound("telekinesis"); } finally { PsionCombat.SoundAct = 0; }
        tc.pos.PlayEffect("telekinesis");
        // The swing comes from the square just before the target on a straight line from you (a hidden bolt path),
        // so line effects like pierce run from there through the target instead of starting at your feet.
        Point from = null;
        var line = EClass._map.ListPointsInLine(cc.pos, tc.pos, cc.Dist(tc) + 1, returnOnBlocked: false);
        int at = line.FindIndex(p => p.Equals(tc.pos));
        if (at > 0 && !line[at - 1].Equals(cc.pos)) from = line[at - 1].Copy();
        PsiStrikeContext.Caster = cc; PsiStrikeContext.From = from;
        try { new ActPsiStrikeMelee().Perform(cc, tc); }
        finally { PsiStrikeContext.Caster = null; PsiStrikeContext.From = null; }
        return true;
    }
}

// The melee attack itself, minus the reach limit.
public class ActPsiStrikeMelee : ActMelee
{
    public override bool UseWeaponDist => false;
    public override int PerformDistance => ActPsiStrike.Range;
    public override bool CanPerform() => Act.TC != null && Act.TC.IsAliveInCurrentZone && Act.CC.CanSeeLos(Act.TC);
}

namespace PsionClass
{
    internal static class PsiStrikeContext
    {
        [ThreadStatic] internal static Chara Caster;
        [ThreadStatic] internal static Point From;
    }

    // During a Psi-Strike, lines the melee code draws from the attacker's tile start at the melee-from square instead.
    [HarmonyPatch(typeof(Map), nameof(Map.ListPointsInLine))]
    static class PsiStrikeLineOrigin
    {
        static void Prefix(ref Point center)
        {
            var c = PsiStrikeContext.Caster;
            if (c != null && PsiStrikeContext.From != null && center != null && center.Equals(c.pos)) center = PsiStrikeContext.From;
        }
    }

    // Knockback pushes away from the attacker's tile: during a Psi-Strike, away from the melee-from square.
    [HarmonyPatch(typeof(Card), nameof(Card.TryMoveFrom))]
    static class PsiStrikeKnockbackOrigin
    {
        static void Prefix(ref Point p)
        {
            var c = PsiStrikeContext.Caster;
            if (c != null && PsiStrikeContext.From != null && p != null && p.Equals(c.pos)) p = PsiStrikeContext.From;
        }
    }

    // Fixed mana cost (default 12), independent of level.
    [HarmonyPatch(typeof(Element), nameof(Element.GetCost))]
    static class PsiStrikeCost
    {
        static void Postfix(Element __instance, Chara c, ref Act.Cost __result)
        {
            if (__instance.id != PsionData.PsiStrike || c == null) return;
            __result.type = Act.CostType.MP;
            __result.cost = PsionCombat.StrikeCost(c);
        }
    }
}
