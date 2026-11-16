using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace PsionClass
{
    internal static class Powers
    {
        // Grants every power the character's base Psicraft has unlocked. Innate (no stock); spellbook level carries over.
        internal static void Grant(Chara c)
        {
            int lv = c.elements.ValueWithoutLink(PsionData.Psicraft);
            foreach (var (need, id) in PsionData.Unlocks)
            {
                if (lv < need || c.ability.Has(id) || !PowerConfig.Enabled(c, id)) continue;
                Element absorbed = null;
                if (PsionData.KineticSource.TryGetValue(id, out var vanillaAlias))
                    absorbed = c.elements.GetElement(EClass.sources.elements.alias[vanillaAlias].id);
                c.ability.Add(id, 100, pt: false);
                var e = c.elements.GetOrCreateElement(id);
                if (absorbed != null)
                {
                    if (absorbed.vBase > e.vBase) { e.vBase = absorbed.vBase; e.vExp = absorbed.vExp; }
                    c.elements.Remove(absorbed.id);
                }
                if (e is Spell) e.vPotential = 0; // innate now: drop leftover stock
                if (c.IsPC) c.Say("learnSkill", c, e.Name);
            }
            PsiEdge.Refresh(c);
            if (c.IsPC) LayerAbility.Redraw();
        }
    }

    // Per-character power opt-out (balance). Off: the power stops being innate and is an ordinary spell again
    // (level and XP kept, stock 0). On: granted again on the spot; any stock gathered meanwhile is cleared.
    internal static class PowerConfig
    {
        const string Key = "psion_powers_off";
        static HashSet<int> Disabled(Chara c) =>
            new HashSet<int>((c.GetStr(Key) ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse));
        internal static bool Enabled(Chara c, int id) => !Disabled(c).Contains(id);
        internal static void Toggle(Chara c, int id)
        {
            var d = Disabled(c);
            bool nowOff = !d.Remove(id);
            if (nowOff) d.Add(id);
            c.SetStr(Key, d.Count == 0 ? null : string.Join(",", d));
            if (nowOff) { if (c.ability.Has(id)) c.ability.Remove(id); }
            else Powers.Grant(c);
            LayerAbility.Redraw();
        }
    }

    // PC: runs on creation, skill level-up, learning, feat change and load (Game.ApplyFix).
    [HarmonyPatch(typeof(ElementContainerCard), nameof(ElementContainerCard.CheckSkillActions))]
    static class UnlockOnSkillCheck
    {
        static void Postfix(ElementContainerCard __instance)
        {
            try { if (__instance.owner is Chara c && c.IsPC && PsionData.IsPsion(c)) Powers.Grant(c); }
            catch (Exception e) { Plugin.Report(e); }
        }
    }

    // NPC Psions: Psicraft = LV + 5..15, with every power that level unlocks.
    [HarmonyPatch(typeof(Chara), nameof(Chara.OnCreate))]
    static class NpcPsion
    {
        static void Postfix(Chara __instance)
        {
            try
            {
                var c = __instance;
                if (c.IsPC || !PsionData.IsPsion(c)) return;
                int target = c.LV + 5 + EClass.rnd(11);
                c.elements.ModBase(PsionData.Psicraft, target - c.elements.ValueWithoutLink(PsionData.Psicraft));
                Powers.Grant(c);
            }
            catch (Exception e) { Plugin.Report(e); }
        }
    }

    // Class item: the Psion guidebook.
    [HarmonyPatch(typeof(Player), nameof(Player.CreateEquip))]
    static class StartingGuide
    {
        static void Postfix()
        {
            try { if (PsionData.IsPsion(EClass.pc)) EClass.pc.AddCard(ThingGen.CreateRedBook("guide_psion")); }
            catch (Exception e) { Plugin.Report(e); }
        }
    }

    // Spellbooks of an acquired power give Psicraft XP (1/10 of a known-skill skillbook) instead of stock.
    [HarmonyPatch(typeof(Chara), nameof(Chara.GainAbility))]
    static class SpellbookToXp
    {
        static bool Prefix(Chara __instance, int ele, int mtp, Thing origin)
        {
            try
            {
                var c = __instance;
                if (!c.IsPC || !PsionData.IsPsion(c) || !c.ability.Has(ele)) return true;
                int xp;
                if (origin?.trait is TraitBaseSpellbook) xp = origin.IsBlessed ? 150 : origin.IsCursed ? 50 : 100;
                else if (origin == null) xp = 100 * Math.Max(mtp, 100) / 100; // dream/sleep stock (Dream Waker raises mtp)
                else return true; // rods etc. stay vanilla
                c.elements.ModExp(PsionData.Psicraft, xp);
                Msg.Say(UiText.Pick("The insight flows into your Psicraft.", "その知見がサイクラフトに流れ込む。", "这份领悟流入了你的灵能术。"));
                return false;
            }
            catch (Exception e) { Plugin.Report(e); return true; }
        }
    }

    // Devour/genes never copy Psicraft (skills have no gene filter natively).
    [HarmonyPatch(typeof(DNA), nameof(DNA.GenerateWithGene))]
    static class NoPsicraftGenes
    {
        static void Postfix(DNA __instance)
        {
            try
            {
                var v = __instance.vals; bool changed = false;
                for (int i = v.Count - 2; i >= 0; i -= 2)
                    if (Array.IndexOf(PsionData.OwnElementIds, v[i]) >= 0) { v.RemoveRange(i, 2); changed = true; }
                if (changed) { __instance.CalcCost(); __instance.CalcSlot(); }
            }
            catch (Exception e) { Plugin.Report(e); }
        }
    }
}
