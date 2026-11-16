using System;
using System.Collections.Generic;
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

        // One window: backup reminder, auto-combat Telekinesis mode, then per-power on/off.
        internal static void OpenConfig()
        {
            var pc = EClass.pc;
            var rows = new List<int> { 0, -1 };                     // 0 = reminder, -1 = TK mode
            rows.AddRange(PsionData.Unlocks.Select(u => u.id).Distinct());
            var layer = EClass.ui.AddLayer<LayerList>();
            layer.SetHeader(UiText.Pick("Psion settings", "サイオン設定", "灵能者设置"));
            layer.SetSize(640, 0);
            layer.SetList2(rows, r =>
            {
                if (r == 0) return UiText.Pick("! Back up your save first. Turning a power back on clears its spell stock.",
                    "! 先にセーブをバックアップしてください。能力を再びオンにするとその呪文のストックは消えます。",
                    "! 请先备份存档。重新开启能力会清除其法术储备。");
                if (r == -1) return UiText.Pick("Auto-combat Telekinesis: ", "自動戦闘のテレキネシス: ", "自动战斗念动术: ") + Label(Tk);
                int lv = PsionData.Unlocks.First(u => u.id == r).lv;
                return (PowerConfig.Enabled(pc, r) ? "● " : "○ ") + EClass.sources.elements.map[r].GetName()
                    + UiText.Pick("  (Psicraft ", "  (サイクラフト", "  (灵能术") + lv + ")";
            }, (r, _) =>
            {
                if (r == 0) return;
                if (r == -1) Tk = (TkMode)(((int)Tk + 1) % 4);
                else PowerConfig.Toggle(pc, r);
                layer.list.List();
            }, (r, item) => { }, autoClose: false);
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
                b.mainText.SetText(UiText.Pick("Psion settings...", "サイオン設定...", "灵能者设置..."));
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

namespace PsionClass
{
    // Tooltips build TK's #calc dice through the Element overload; double the sides there too so the text matches.
    [HarmonyLib.HarmonyPatch(typeof(Dice), nameof(Dice.Create), new[] { typeof(Element), typeof(Card) })]
    static class PsionTelekinesisDiceTooltip
    {
        static void Postfix(Element ele, Card c, Dice __result)
        {
            if (__result != null && ele?.id == PsionData.Telekinesis && c is Chara ch && ch.HasElement(PsionData.FeatPsion)) __result.sides *= 2;
        }
    }
}

namespace PsionClass
{
    // Tooltip header: innate powers show "-" for stock, like ordinary abilities.
    [HarmonyLib.HarmonyPatch(typeof(Element), nameof(Element.AddHeaderAbility))]
    static class HideInnateStockTooltip
    {
        static bool Prefix(Element __instance, UINote n)
        {
            var pc = EClass.pc;
            if (!(__instance is Spell) || pc == null || !PsionData.IsPsion(pc) || !pc.ability.Has(__instance.id)) return true;
            var item = n.AddHeader("HeaderAbility", __instance.FullName.ToTitleCase(wholeText: true), __instance.GetSprite());
            item.text2.text = "-";
            (__instance as Act)?.SetImage(item.image1);
            return false;
        }
    }
}
