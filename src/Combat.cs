using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace PsionClass
{
    internal static class PsionCombat
    {
        const int KeyTkMode = 794001; // Chara int map key
        internal enum TkMode { Off = 0, PullOnly = 1, PushOnly = 2, Both = 3 }
        internal static TkMode Tk { get => (TkMode)EClass.pc.GetInt(KeyTkMode); set => EClass.pc.SetInt(KeyTkMode, (int)value); }
        internal static string Label(TkMode m) => m switch
        {
            TkMode.PullOnly => UiText.Pick("Pull distant foes only", "離れた敵を引き寄せるのみ", "仅拉近远处敌人"),
            TkMode.PushOnly => UiText.Pick("Hurl adjacent foes only", "隣接する敵を放り飛ばすのみ", "仅抛飞相邻敌人"),
            TkMode.Both => UiText.Pick("Pull and hurl", "引き寄せと放り飛ばし", "拉近与抛飞"),
            _ => UiText.Pick("Don't use", "使わない", "不使用"),
        };
        internal static readonly int[] PowerIds = PsionData.Unlocks.Select(u => u.id).Distinct().ToArray();

        internal static void OpenConfig()
        {
            var layer = EClass.ui.AddLayer<LayerList>();
            layer.SetHeader(UiText.Pick("Psion combat", "サイオンの戦術", "灵能者战术"));
            var modes = (TkMode[])Enum.GetValues(typeof(TkMode));
            layer.SetList2(modes.ToList(),
                m => (Tk == m ? "● " : "○ ") + UiText.Pick("Auto-combat Telekinesis: ", "自動戦闘のテレキネシス: ", "自动战斗念动术: ") + Label(m),
                (m, _) => { Tk = m; }, (m, item) => { }, autoClose: true);
        }
    }

    // Psicraft XP mirrors the spell XP each power earns (success and fizzle alike).
    [HarmonyPatch(typeof(Card), nameof(Card.ModExp), new[] { typeof(int), typeof(int) })]
    static class PsicraftXp
    {
        static void Postfix(Card __instance, int ele, int a)
        {
            if (a <= 0 || !(__instance is Chara c) || !PsionData.IsPsion(c) || Array.IndexOf(PsionCombat.PowerIds, ele) < 0) return;
            try { c.elements.ModExp(PsionData.Psicraft, a); } catch (Exception e) { Plugin.Report(e); }
        }
    }

    // Auto-combat Telekinesis follows the Psion combat setting; hurls never go through non-hostiles.
    [HarmonyPatch(typeof(ActTelekinesis), nameof(ActTelekinesis.CanPerform))]
    static class TelekinesisAutoMode
    {
        static void Postfix(ActTelekinesis __instance, ref bool __result)
        {
            if (!__result) return;
            var cc = Act.CC; var tc = Act.TC;
            if (cc == null || !cc.IsPC || !PsionData.IsPsion(cc) || !(cc.ai is GoalCombat) || tc == null) return;
            bool adjacent = tc.Dist(cc) == 1;
            var mode = PsionCombat.Tk;
            if (mode == PsionCombat.TkMode.Off || (mode == PsionCombat.TkMode.PullOnly && adjacent) || (mode == PsionCombat.TkMode.PushOnly && !adjacent))
            { __result = false; return; }
            if (adjacent)
                foreach (var p in __instance.ListPath(cc.pos, tc.pos))
                    if (!p.Equals(tc.pos) && p.ListCharas().Any(ch => ch != cc && ch != tc && !ch.IsHostile(cc))) { __result = false; return; }
        }
    }

    // One button in the strategy view, Psions only.
    [HarmonyPatch(typeof(ContentTactics), nameof(ContentTactics.Refresh))]
    static class StrategyButton
    {
        const string Name = "PsionCombatButton";
        static void Postfix(ContentTactics __instance)
        {
            try
            {
                var parent = __instance.toggleBTurbo.transform.parent;
                var existing = parent.Find(Name);
                if (!PsionData.IsPsion(EClass.pc)) { if (existing) existing.gameObject.SetActive(false); return; }
                if (existing) { existing.gameObject.SetActive(true); return; }
                var b = UnityEngine.Object.Instantiate(__instance.toggleBTurbo, parent);
                b.name = Name;
                b.transform.SetSiblingIndex(0);
                b.onClick.RemoveAllListeners();
                b.SetToggle(false, null);
                b.onClick.RemoveAllListeners();
                b.onClick.AddListener(() => PsionCombat.OpenConfig());
                b.mainText.SetText(UiText.Pick("Psion combat settings...", "サイオンの戦術設定...", "灵能者战术设置..."));
            }
            catch (Exception e) { Plugin.Report(e); }
        }
    }
}

namespace PsionClass
{
    // Innate powers never use stock, so hide the stock count on them (ability window, alt layout).
    [HarmonyLib.HarmonyPatch(typeof(ButtonAbility), nameof(ButtonAbility.SetAct))]
    static class HideInnateStock
    {
        static void Postfix(ButtonAbility __instance, Chara _chara, Element e)
        {
            try { if (_chara != null && PsionData.IsPsion(_chara) && _chara.ability.Has(e.id) && __instance.textStock) __instance.textStock.SetActive(false); }
            catch (System.Exception ex) { Plugin.Report(ex); }
        }
    }
}

namespace PsionClass
{
    // Same for abilities held on the hotbar/toolbelt.
    [HarmonyLib.HarmonyPatch(typeof(TraitAbility), nameof(TraitAbility.SetMainText))]
    static class HideInnateStockHotbar
    {
        static void Postfix(TraitAbility __instance, UIText t)
        {
            try
            {
                var pc = EClass.pc; var row = EClass.sources.elements.alias.TryGetValue(__instance.owner.c_idAbility ?? "");
                if (row != null && PsionData.IsPsion(pc) && pc.ability.Has(row.id)) t.SetActive(false);
            }
            catch (System.Exception ex) { Plugin.Report(ex); }
        }
    }
}

namespace PsionClass
{
    // A Psion's Telekinesis: collision dice sides x2 (1d8 -> 1d16). The spell tooltip uses the same dice, so it shows too.
    [HarmonyLib.HarmonyPatch(typeof(Dice), nameof(Dice.Create), new[] { typeof(string), typeof(int), typeof(Card), typeof(Act) })]
    static class PsionTelekinesisDice
    {
        static void Postfix(string id, Card c, Dice __result)
        {
            if (__result != null && id == "SpTelekinesis" && c is Chara ch && ch.HasElement(PsionData.FeatPsion)) __result.sides *= 2;
        }
    }
}
