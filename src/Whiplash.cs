using System;
using HarmonyLib;

namespace PsionClass
{
    internal static class Whiplash
    {
        internal const int EleMind = 914, EleImpact = 925;
        internal static readonly string[] ExtraRaces = { "hand" };
        [ThreadStatic] internal static bool ItemPowered;   // current DamageEle comes from a rod/scroll/etc.
        [ThreadStatic] internal static int CurrentAct;     // current DamageEle's act id
        [ThreadStatic] internal static bool Reflecting;

        internal static bool IsTarget(Card c) =>
            c is Chara ch && (ch.IsUndead || Array.IndexOf(ExtraRaces, ch.race?.id) >= 0);

        // The Psion responsible for a hit: a Psion caster, or the Psion master of a Mind/kinetic funnel.
        internal static Chara SourcePsion(Card origin, out Chara reflectTo)
        {
            reflectTo = null;
            if (!(origin is Chara o)) return null;
            if (o.HasElement(PsionData.FeatPsion)) { reflectTo = o; return o; }
            if (o.id == "bit" && o.master != null && o.master.HasElement(PsionData.FeatPsion)) { reflectTo = o; return o.master; }
            return null;
        }

        // Effective Necropathy: feat tier + gear enchant, capped at 3.
        internal static int Necropathy(Chara psion) =>
            Math.Min(3, psion.elements.ValueWithoutLink(PsionData.FeatNecropathy) + Math.Max(0, psion.Evalue(PsionData.NecropathyEnchant)));
    }

    [HarmonyPatch(typeof(ActEffect), nameof(ActEffect.DamageEle))]
    static class DamageEleContext
    {
        static void Prefix(ActRef actref, out (bool, int) __state)
        {
            __state = (Whiplash.ItemPowered, Whiplash.CurrentAct);
            Whiplash.ItemPowered = actref.refThing != null;
            Whiplash.CurrentAct = actref.act?.id ?? 0;
        }
        static Exception Finalizer(Exception __exception, (bool, int) __state)
        {
            (Whiplash.ItemPowered, Whiplash.CurrentAct) = __state;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Card), nameof(Card.DamageHP), new[] { typeof(long), typeof(int), typeof(int), typeof(AttackSource), typeof(Card), typeof(bool), typeof(Thing), typeof(Chara), typeof(int) })]
    static class WhiplashDamage
    {
        static bool Prefix(Card __instance, ref long dmg, int ele, int eleP, Card origin)
        {
            try
            {
                if (Whiplash.Reflecting || dmg <= 0) return true;
                // Kinetic powers are physical: armor applies (spell damage normally skips PV).
                if (ele == Whiplash.EleImpact && Array.IndexOf(PsionData.KineticIds, Whiplash.CurrentAct) >= 0 && !Whiplash.ItemPowered)
                    dmg = __instance.ApplyProtection(dmg);
                if (ele != Whiplash.EleMind || Whiplash.ItemPowered || !Whiplash.IsTarget(__instance)) return true;
                var psion = Whiplash.SourcePsion(origin, out var reflectTo);
                if (psion == null) return true;
                int n = Whiplash.Necropathy(psion);
                if (n >= 3) return true;
                if (n == 2) { dmg /= 2; return true; }
                if (n == 1)
                {
                    if (reflectTo.IsPC) Msg.Say(UiText.Pick("The dead mind slips from your grasp.", "死者の精神は掴めずにすり抜けた。", "死者的精神从你的掌握中滑走了。"));
                    return false;
                }
                long reflected = dmg;
                Whiplash.Reflecting = true;
                try
                {
                    if (reflectTo.IsPCParty || reflectTo.IsPC)
                        Msg.Say(UiText.Pick("Your mind recoils from the dead and wounds you!", "死者に触れた精神が跳ね返り、あなたを傷つけた!", "触及死者的精神反弹回来,伤害了你!"));
                    if (reflectTo == psion) reflectTo.DamageHP(reflected, Whiplash.EleMind, eleP, AttackSource.None, __instance);
                    else reflectTo.DamageHP(reflected, AttackSource.None, __instance); // Mind funnels are Mind-immune: untyped
                }
                finally { Whiplash.Reflecting = false; }
                return false;
            }
            catch (Exception e) { Plugin.Report(e); return true; }
        }
    }

    // Necropathy 2/3 pierce resistance (native penetration levels) for Psion mind damage on whiplash targets.
    [HarmonyPatch(typeof(Card), nameof(Card.GetResistPenetrationLevel))]
    static class NecropathyPenetration
    {
        static void Postfix(Card __instance, int idEle, Card origin, ref int __result)
        {
            try
            {
                if (idEle != Whiplash.EleMind || !Whiplash.IsTarget(__instance)) return;
                var psion = Whiplash.SourcePsion(origin, out _);
                if (psion == null) return;
                int n = Whiplash.Necropathy(psion);
                if (n >= 2) __result += n - 1;
            }
            catch (Exception e) { Plugin.Report(e); }
        }
    }
}

namespace PsionClass
{
    // Mind and kinetic (Impact) funnels inherit the Psion caster's Psicraft, which raises their level.
    [HarmonyLib.HarmonyPatch(typeof(Chara), nameof(Chara.MakeMinion))]
    static class FunnelInherit
    {
        static void Postfix(Chara __instance, Chara _master)
        {
            try
            {
                var bit = __instance;
                if (bit.id != "bit" || _master == null || !_master.HasElement(PsionData.FeatPsion)) return;
                int ele = bit.c_idMainElement;
                if (ele != Whiplash.EleMind && ele != Whiplash.EleImpact) return;
                int psi = _master.elements.ValueWithoutLink(PsionData.Psicraft);
                bit.elements.SetBase(PsionData.Psicraft, psi);
                if (psi > bit.LV) bit.SetLv(psi);
            }
            catch (System.Exception e) { Plugin.Report(e); }
        }
    }
}
