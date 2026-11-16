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
        const string KeyTkDice = "psion_tk_dice"; // float multiplier; default 2
        internal static float TkDiceMult(Chara c) =>
            float.TryParse(c.GetStr(KeyTkDice), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) && v > 0 ? v : 2f;
        internal static void SetTkDiceMult(Chara c, float v) =>
            c.SetStr(KeyTkDice, Math.Abs(v - 2f) < 0.0001f ? null : v.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        internal static void ApplyTkDice(Card c, Dice d)
        {
            if (d != null && c is Chara ch && ch.HasElement(PsionData.FeatPsion))
                d.sides = Math.Max(1, (int)Math.Floor(d.sides * TkDiceMult(ch))); // floor
        }
        internal static string Label(TkMode m) => m switch
        {
            TkMode.PullOnly => UiText.Pick("Pull distant foes only", "離れた敵を引き寄せるのみ", "仅拉近远处敌人"),
            TkMode.PushOnly => UiText.Pick("Hurl adjacent foes only", "隣接する敵を放り飛ばすのみ", "仅抛飞相邻敌人"),
            TkMode.Both => UiText.Pick("Pull and hurl", "引き寄せと放り飛ばし", "拉近与抛飞"),
            _ => UiText.Pick("Don't use", "使わない", "不使用"),
        };
        internal static readonly int[] PowerIds = PsionData.Unlocks.Select(u => u.id).Distinct().ToArray();

        // Which non-hostile bystanders block auto-combat Telekinesis when on the travel path (all on by default).
        internal enum Bystander { Allies = 0, NeutralCreatures = 1, NeutralNpcs = 2, FriendlyNpcs = 3, Evolved = 4, BigDaddy = 5 }
        const string KeyPassThrough = "psion_tk_passthrough"; // bystander kinds allowed in the path (unchecked)
        static HashSet<int> PassThrough(Chara c) =>
            new HashSet<int>((c.GetStr(KeyPassThrough) ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse));
        static bool DefaultOn(Bystander k) => true;
        internal static bool Blocks(Chara c, Bystander k) => DefaultOn(k) != PassThrough(c).Contains((int)k);
        internal static void ToggleBlock(Chara c, Bystander k)
        {
            var set = PassThrough(c);
            if (!set.Remove((int)k)) set.Add((int)k);
            c.SetStr(KeyPassThrough, set.Count == 0 ? null : string.Join(",", set));
        }
        internal static string BystanderLabel(Bystander k) => k switch
        {
            Bystander.Allies => UiText.Pick("Allies (party and their summons)", "味方(仲間と召喚物)", "友方(队友及其召唤物)"),
            Bystander.NeutralCreatures => UiText.Pick("Neutral animals and monsters", "中立の動物・モンスター", "中立的动物与怪物"),
            Bystander.NeutralNpcs => UiText.Pick("Neutral NPCs", "中立のNPC", "中立NPC"),
            Bystander.FriendlyNpcs => UiText.Pick("Friendly NPCs", "友好的なNPC", "友好NPC"),
            Bystander.Evolved => UiText.Pick("Evolved monsters", "進化した魔物", "进化的怪物"),
            _ => UiText.Pick("Big Daddy", "ビッグダディ", "大老爹"),
        };
        // Classify a non-hostile character on the path; null = hostile (never blocks).
        internal static IEnumerable<Bystander> Kinds(Chara caster, Chara ch)
        {
            if (ch.IsHostile(caster)) yield break; // hostiles are always fair game
            if (ch.c_bossType == BossType.Evolved) yield return Bystander.Evolved;
            if (ch.id.StartsWith("big_daddy", StringComparison.Ordinal)) yield return Bystander.BigDaddy; // every variant
            if (Kind(caster, ch) is Bystander k) yield return k;
        }
        internal static Bystander? Kind(Chara caster, Chara ch)
        {
            if (ch.IsHostile(caster)) return null;
            if (ch.IsPCFactionOrMinion || ch.IsPCParty) return Bystander.Allies;
            if (ch.hostility >= Hostility.Friend) return Bystander.FriendlyNpcs;
            return ch.IsHumanSpeak ? Bystander.NeutralNpcs : Bystander.NeutralCreatures;
        }
        const string KeyTkFixed = "psion_tk_fixed", KeyTkFixedOn = "psion_tk_fixed_on"; // value is kept while switched off
        internal static int TkFixedValue(Chara c) => int.TryParse(c.GetStr(KeyTkFixed), out int v) && v >= 0 ? v : 3;
        internal static void SetTkFixedValue(Chara c, int v) => c.SetStr(KeyTkFixed, v.ToString());
        internal static bool TkFixedOn(Chara c) => !string.IsNullOrEmpty(c.GetStr(KeyTkFixedOn));
        internal static void SetTkFixedOn(Chara c, bool on) => c.SetStr(KeyTkFixedOn, on ? "1" : null);
        internal static int? TkFixed(Chara c) => TkFixedOn(c) ? TkFixedValue(c) : (int?)null;
        const string KeyTkMp = "psion_tk_mp"; // TK mana cost multiplier for Psions; default 0.5, 0 = free
        internal static float TkMpMult(Chara c) =>
            float.TryParse(c.GetStr(KeyTkMp), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) && v >= 0 ? v : 0.5f;
        internal static void SetTkMpMult(Chara c, float v) =>
            c.SetStr(KeyTkMp, Math.Abs(v - 0.5f) < 0.0001f ? null : v.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        const string KeyDmgOnly = "psion_tk_dmgonly", KeyCombo = "psion_tk_combo";
        internal static bool DamageOnly(Chara c) => !string.IsNullOrEmpty(c.GetStr(KeyDmgOnly));
        internal static void SetDamageOnly(Chara c, bool on) => c.SetStr(KeyDmgOnly, on ? "1" : null);
        internal static bool Combo(Chara c) => !string.IsNullOrEmpty(c.GetStr(KeyCombo));
        internal static void SetCombo(Chara c, bool on) => c.SetStr(KeyCombo, on ? "1" : null);
        // A pull lands the target on the path's last square (beside the caster); a hurl from there needs room beyond it.
        internal static bool PullSetsUpHurl(ActTelekinesis tk, Chara cc, List<Point> pullPath)
        {
            if (pullPath.Count == 0) return false;
            var landing = pullPath[pullPath.Count - 1];
            return landing.Distance(cc.pos) == 1 && tk.ListPath(cc.pos, landing).Count > 1;
        }
        const string KeyBonusOff = "psion_ai_bonus_off";
        internal static bool BonusOn(Chara c) => string.IsNullOrEmpty(c.GetStr(KeyBonusOff)); // default on
        internal static void SetBonusOn(Chara c, bool on) => c.SetStr(KeyBonusOff, on ? null : "1");
        internal static void OpenConfig() => SettingsUi.Open();
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
            // ListPath is mode-correct: a pull lists the squares between target and caster (never past the target);
            // a hurl lists the squares beyond the target. Check only the path this cast will travel.
            var path = __instance.ListPath(cc.pos, tc.pos);
            // Damage-only: hurls always deal collision damage; a pull deals none, so it is allowed only as a
            // Pull-and-hurl setup whose landing square leaves room for the follow-up hurl.
            if (PsionCombat.DamageOnly(cc) && !adjacent &&
                !(mode == PsionCombat.TkMode.Both && PsionCombat.Combo(cc) && PsionCombat.PullSetsUpHurl(__instance, cc, path)))
            { __result = false; return; }
            foreach (var p in path)
                if (!p.Equals(tc.pos) && p.ListCharas().Any(ch => ch != cc && ch != tc && PsionCombat.Kinds(cc, ch).Any(k => PsionCombat.Blocks(cc, k))))
                { __result = false; return; }
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
                // Always-visible group (the Details toggle's row), not the Details-only section.
                var anchor = __instance.toggleDetail;
                var parent = anchor.transform.parent;
                var existing = parent.Find(Name);
                if (!PsionData.IsPsion(EClass.pc)) { if (existing) existing.gameObject.SetActive(false); return; }
                if (existing) { existing.gameObject.SetActive(true); existing.GetComponent<UIButton>()?.mainText.SetText(ClassName.Current + UiText.Pick(" settings...", "設定...", "设置...")); return; }
                var b = UnityEngine.Object.Instantiate(anchor, parent);
                b.name = Name;
                // The Details toggle is pinned in the corner (ignoreLayout); make ours a normal row under the tactics dropdown.
                var le = b.GetComponent<UnityEngine.UI.LayoutElement>(); if (le) le.ignoreLayout = false;
                var row = __instance.ddTactics.transform; while (row.parent != parent && row.parent != null) row = row.parent;
                b.transform.SetSiblingIndex(row.parent == parent ? row.GetSiblingIndex() + 1 : parent.childCount - 1);
                ((RectTransform)b.transform).sizeDelta = new Vector2(200, 30);
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)parent);
                b.SetToggle(false, null);
                b.onClick.RemoveAllListeners();
                foreach (var img in b.GetComponentsInChildren<UnityEngine.UI.Image>(true)) img.enabled = false; // drop the checkbox square
                var surface = new GameObject("Psion action surface", typeof(RectTransform), typeof(UnityEngine.UI.Image));
                var sr = (RectTransform)surface.transform; sr.SetParent(b.transform, false); sr.SetAsFirstSibling();
                sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one; sr.offsetMin = new Vector2(-4, -2); sr.offsetMax = new Vector2(4, 2);
                var si = surface.GetComponent<UnityEngine.UI.Image>(); si.color = new Color(.38f, .28f, .16f, .14f);
                var ol = surface.AddComponent<UnityEngine.UI.Outline>(); ol.effectColor = new Color(.38f, .28f, .16f, .35f); ol.effectDistance = new Vector2(1, -1);
                b.targetGraphic = si;
                b.mainText.alignment = TextAnchor.MiddleCenter;
                var mt = b.mainText.rectTransform; mt.anchorMin = Vector2.zero; mt.anchorMax = Vector2.one; mt.offsetMin = mt.offsetMax = Vector2.zero;
                b.onClick.AddListener(() => PsionCombat.OpenConfig());
                b.mainText.SetText(ClassName.Current + UiText.Pick(" settings...", "設定...", "设置..."));
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
            if (id == "SpTelekinesis") PsionCombat.ApplyTkDice(c, __result);
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
            if (ele?.id == PsionData.Telekinesis) PsionCombat.ApplyTkDice(c, __result);
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

namespace PsionClass
{
    // A Psion's Telekinesis mana cost x multiplier (default 0.5, floor; 0 = free).
    [HarmonyLib.HarmonyPatch(typeof(Element), nameof(Element.GetCost))]
    static class PsionTelekinesisCost
    {
        static void Postfix(Element __instance, Chara c, ref Act.Cost __result)
        {
            if (__instance.id != PsionData.Telekinesis || c == null || !c.HasElement(PsionData.FeatPsion) || __result.type != Act.CostType.MP) return;
            var fixedCost = PsionCombat.TkFixed(c);
            if (fixedCost.HasValue) { __result.cost = fixedCost.Value; return; } // ignores level scaling
            float m = PsionCombat.TkMpMult(c);
            __result.cost = m <= 0 ? 0 : Math.Max(1, (int)Math.Floor(__result.cost * m));
        }
    }
}

namespace PsionClass
{
    // Psion strategy: per-power priority bonus on the auto-combat ability list (PC uses its settings; NPCs the defaults).
    [HarmonyLib.HarmonyPatch]
    static class PsionStrategyBonus
    {
        static System.Collections.Generic.IEnumerable<System.Reflection.MethodBase> TargetMethods()
        {
            yield return HarmonyLib.AccessTools.Method(typeof(GoalCombat), nameof(GoalCombat.BuildAbilityList));
            yield return HarmonyLib.AccessTools.Method(typeof(GoalAutoCombat), nameof(GoalAutoCombat.BuildAbilityList));
        }
        static void Postfix(GoalCombat __instance)
        {
            try
            {
                var owner = __instance.owner;
                if (owner == null || !owner.HasElement(PsionData.FeatPsion)) return;
                // Player: overlay on any chosen strategy (toggle). NPC Psions: only with their own psion tactics.
                if (owner.IsPC ? !PsionCombat.BonusOn(owner) : owner.tactics?.source?.id != PsionData.JobId) return;
                var cfg = owner.IsPC ? owner : null;
                foreach (var a in __instance.abilities)
                {
                    if (a.act == null || Array.IndexOf(PsionCombat.PowerIds, a.act.id) < 0) continue;
                    a.priorityMod = PowerConfig.Bonus(cfg, a.act.id); // set (not add): BuildAbilityList may run twice
                }
            }
            catch (Exception e) { Plugin.Report(e); }
        }
    }
}
