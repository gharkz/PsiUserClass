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
            float.TryParse(c.GetStr(KeyTkDice), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) && v > 0 ? v : 1.5f;
        internal static void SetTkDiceMult(Chara c, float v) =>
            c.SetStr(KeyTkDice, Math.Abs(v - 1.5f) < 0.0001f ? null : v.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
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
        const string KeyHurlFull = "psion_hurl_full"; // Psi-Hurl always flies the full 20 tiles
        internal static bool HurlFull(Chara c) => !string.IsNullOrEmpty(c.GetStr(KeyHurlFull));
        internal static void SetHurlFull(Chara c, bool on) => c.SetStr(KeyHurlFull, on ? "1" : null);
        const string KeyTkcAlliesOff = "psion_tkc_allies_off"; // Telekinetic Control protects allies in your Psi-Hurl (default on)
        internal static bool TkControlAllies(Chara c) => string.IsNullOrEmpty(c.GetStr(KeyTkcAlliesOff));
        internal static void SetTkControlAllies(Chara c, bool on) => c.SetStr(KeyTkcAlliesOff, on ? null : "1");
        // AutoCombat may throw allies with Psi-Hurl (default off), optionally only at a Telekinetic Control rank (default on, rank 2).
        const string KeyAllyAmmo = "psion_hurl_allies", KeyAllyReqOff = "psion_hurl_allies_req_off", KeyAllyReqRank = "psion_hurl_allies_rank";
        internal static bool AllyAmmo(Chara c) => !string.IsNullOrEmpty(c.GetStr(KeyAllyAmmo));
        internal static void SetAllyAmmo(Chara c, bool on) => c.SetStr(KeyAllyAmmo, on ? "1" : null);
        internal static bool AllyReqOn(Chara c) => string.IsNullOrEmpty(c.GetStr(KeyAllyReqOff));
        internal static void SetAllyReqOn(Chara c, bool on) => c.SetStr(KeyAllyReqOff, on ? null : "1");
        internal static int AllyReqRank(Chara c) => c.GetStr(KeyAllyReqRank) == "1" ? 1 : 2;
        internal static void ToggleAllyReqRank(Chara c) => c.SetStr(KeyAllyReqRank, AllyReqRank(c) == 2 ? "1" : null);
        internal static bool AllyAmmoAllowed(Chara c) => AllyAmmo(c) && (!AllyReqOn(c)
            || (TkControlAllies(c) && c.elements.ValueWithoutLink(PsionData.FeatTkControl) >= AllyReqRank(c)));
        [ThreadStatic] internal static bool RawTkCost; // read Telekinesis's natural cost (skip the Psion multiplier)
        internal enum HurlCost { CurrentTk = 0, NaturalTk = 1, Fixed = 2 }
        const string KeyHurlCostMode = "psion_hurl_costmode", KeyHurlMult = "psion_hurl_mult", KeyHurlFixed = "psion_hurl_fixed";
        internal static HurlCost HurlCostMode(Chara c) => int.TryParse(c.GetStr(KeyHurlCostMode), out int v) && v >= 0 && v <= 2 ? (HurlCost)v : HurlCost.CurrentTk;
        internal static void SetHurlCostMode(Chara c, HurlCost m) => c.SetStr(KeyHurlCostMode, m == HurlCost.CurrentTk ? null : ((int)m).ToString());
        internal static float HurlMult(Chara c) =>
            float.TryParse(c.GetStr(KeyHurlMult), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) && v >= 0 ? v : 1.5f;
        internal static void SetHurlMult(Chara c, float v) =>
            c.SetStr(KeyHurlMult, Math.Abs(v - 1.5f) < 0.0001f ? null : v.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        internal static int HurlFixed(Chara c) => int.TryParse(c.GetStr(KeyHurlFixed), out int v) && v >= 0 ? v : 5;
        internal static void SetHurlFixed(Chara c, int v) => c.SetStr(KeyHurlFixed, v == 5 ? null : v.ToString());
        internal static string HurlCostLabel(HurlCost m) => m switch
        {
            HurlCost.NaturalTk => UiText.Pick("x natural Telekinesis cost", "x テレキネシス本来の消費", "x 念动术原始消耗"),
            HurlCost.Fixed => UiText.Pick("Fixed cost", "固定消費", "固定消耗"),
            _ => UiText.Pick("x current Telekinesis cost", "x 現在のテレキネシス消費", "x 当前念动术消耗"),
        };
        // AutoCombat Psi-Hurl direction when hurling an enemy (default random); optionally preferred over collision throws.
        internal enum HurlDirection { Random = 0, Away = 1, Toward = 2 }
        const string KeyHurlDir = "psion_hurl_dir", KeyHurlDirFirst = "psion_hurl_dirfirst";
        internal static HurlDirection HurlDir(Chara c) => int.TryParse(c.GetStr(KeyHurlDir), out int v) && v >= 0 && v <= 2 ? (HurlDirection)v : HurlDirection.Random;
        internal static void SetHurlDir(Chara c, HurlDirection d) => c.SetStr(KeyHurlDir, d == HurlDirection.Random ? null : ((int)d).ToString());
        internal static bool HurlDirFirst(Chara c) => !string.IsNullOrEmpty(c.GetStr(KeyHurlDirFirst));
        internal static void SetHurlDirFirst(Chara c, bool on) => c.SetStr(KeyHurlDirFirst, on ? "1" : null);
        internal static string HurlDirLabel(HurlDirection d) => d switch
        {
            HurlDirection.Away => UiText.Pick("Away from you", "自分から遠ざける", "远离你"),
            HurlDirection.Toward => UiText.Pick("Toward you", "自分へ引き寄せる", "朝向你"),
            _ => UiText.Pick("Random direction", "ランダムな方向", "随机方向"),
        };
        const string KeyStrikeCost = "psion_strike_cost"; // Psi-Strike fixed mana cost, default 12
        internal static int StrikeCost(Chara c) => int.TryParse(c.GetStr(KeyStrikeCost), out int v) && v >= 0 ? v : 12;
        internal static void SetStrikeCost(Chara c, int v) => c.SetStr(KeyStrikeCost, v == 12 ? null : v.ToString());
        // Ranged / keep-away enemies (archers, casters, performers that hang back): AutoCombat pulls them in (default on),
        // and optionally never hurls them away again (default off).
        const string KeyPullRangedOff = "psion_pull_ranged_off", KeyKeepClose = "psion_keep_close";
        internal static bool PullRanged(Chara c) => string.IsNullOrEmpty(c.GetStr(KeyPullRangedOff));
        internal static void SetPullRanged(Chara c, bool on) => c.SetStr(KeyPullRangedOff, on ? null : "1");
        internal static bool KeepClose(Chara c) => !string.IsNullOrEmpty(c.GetStr(KeyKeepClose));
        internal static void SetKeepClose(Chara c, bool on) => c.SetStr(KeyKeepClose, on ? "1" : null);
        internal static bool KeepsAway(Chara ch) => ch.ranged != null || ch.tactics.DestDist >= 2;
        // Telekinesis does the ranged pull (cheaper, lands beside you) whenever it's usable; Psi-Hurl only covers when it isn't.
        internal static bool TkPullsRanged(Chara cc, Card t) => cc.IsPC && PullRanged(cc) && Tk != TkMode.Off && t is Chara ch && ch.IsHostile(cc) && KeepsAway(ch);
        internal static bool TkCanPull(Chara cc) => cc.IsPC && PullRanged(cc) && Tk != TkMode.Off && cc.ability.Has(PsionData.Telekinesis);
        // A ranged foe still at range while pulling is on: only Telekinesis' pull (or Psi-Hurl toward you) may move it.
        internal static bool PullingAt(Chara cc, Card t) => cc.IsPC && PullRanged(cc) && t is Chara ch && ch.IsHostile(cc) && KeepsAway(ch) && cc.Dist(ch) >= 2;
        internal static bool HoldClose(Chara cc, Card t) => cc.IsPC && PullRanged(cc) && KeepClose(cc) && t is Chara ch && ch.IsHostile(cc) && KeepsAway(ch);
        const string KeyPreferTkOff = "psion_hurl_prefer_tk_off"; // AutoCombat leaves adjacent enemies to Telekinesis (default on)
        internal static bool PreferTk(Chara c) => string.IsNullOrEmpty(c.GetStr(KeyPreferTkOff));
        internal static void SetPreferTk(Chara c, bool on) => c.SetStr(KeyPreferTkOff, on ? null : "1");
        // The Telekinesis sound ("telekinesis") can be muted for all three powers or each one.
        [ThreadStatic] internal static int SoundAct; // the power playing sounds right now (0 = none)
        const string KeySndOff = "psion_tksnd_off";
        internal static readonly int[] SoundPowers = { PsionData.Telekinesis, PsionData.PsiHurl, PsionData.PsiStrike };
        internal static bool TkSoundOn(Chara c) => string.IsNullOrEmpty(c.GetStr(KeySndOff));
        internal static void SetTkSoundOn(Chara c, bool on) => c.SetStr(KeySndOff, on ? null : "1");
        // Per power: "" = the default Telekinesis sound, "-" = muted, otherwise another game sound id.
        internal static readonly string[] SoundChoices = { "", "-", "throw", "hit_slash", "hit_blunt", "fish_splash", "push", "whip", "fizzle" };
        internal static string SoundLabel(string v) => v switch
        {
            "" => UiText.Pick("Default", "標準", "默认"),
            "-" => UiText.Pick("Muted", "無音", "静音"),
            "throw" => UiText.Pick("Throw", "投擲", "投掷"),
            "hit_slash" => UiText.Pick("Slash", "斬撃", "斩击"),
            "hit_blunt" => UiText.Pick("Swat", "打撃", "打击"),
            "fish_splash" => UiText.Pick("Splash", "水しぶき", "水花"),
            "push" => UiText.Pick("Push", "押す", "推"),
            "whip" => UiText.Pick("Whip", "鞭", "鞭"),
            "fizzle" => UiText.Pick("Failed cast", "詠唱失敗", "施法失败"),
            _ => v,
        };
        static string SoundKey(int id) => "psion_tksnd_" + id;
        internal static string TkSound(Chara c, int id)
        {
            var v = c.GetStr(SoundKey(id)) ?? "";
            return Array.IndexOf(SoundChoices, v) >= 0 ? v : "";
        }
        internal static void SetTkSound(Chara c, int id, string v) => c.SetStr(SoundKey(id), string.IsNullOrEmpty(v) ? null : v);
        // Sound to play instead of "telekinesis" for the power playing now: null = mute, the same id = unchanged.
        internal static string TkSoundFor(int act)
        {
            var pc = EClass.pc;
            if (pc == null || act == 0 || !PsionData.IsPsion(pc)) return "telekinesis"; // Psion characters only
            if (!TkSoundOn(pc)) return null;
            var v = TkSound(pc, act);
            return v == "" ? "telekinesis" : v == "-" ? null : v;
        }
        // Telekinetic Control rank 2: passing a creature at least twice your weight still lets 20% of the landing through.
        const string KeyHeavyOff = "psion_tkc_heavy_off";
        internal const int HeavyPass = 20;
        // What makes a body "very heavy": level (default) or weight at least Factor x yours (default 2).
        const string KeyHeavyWeight = "psion_tkc_heavy_weight", KeyHeavyFactor = "psion_tkc_heavy_factor";
        internal static bool HeavyByWeight(Chara c) => !string.IsNullOrEmpty(c?.GetStr(KeyHeavyWeight));
        internal static void SetHeavyByWeight(Chara c, bool on) => c.SetStr(KeyHeavyWeight, on ? "1" : null);
        internal static float HeavyFactor(Chara c) =>
            float.TryParse(c?.GetStr(KeyHeavyFactor), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v) && v > 0 ? v : 2f;
        internal static void SetHeavyFactor(Chara c, float v) =>
            c.SetStr(KeyHeavyFactor, Math.Abs(v - 2f) < 0.0001f ? null : v.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        internal static bool IsHeavy(Chara self, Chara other) => IsHeavy(self, self, other);
        // cfg: whose settings decide the rule (the thrower); flier: who is flying (you or the ally you threw).
        internal static bool IsHeavy(Chara cfg, Chara flier, Chara other) => HeavyByWeight(cfg)
            ? other.SelfWeight >= flier.SelfWeight * (double)HeavyFactor(cfg)
            : other.LV >= flier.LV * (double)HeavyFactor(cfg);
        internal static string HeavyRule(Chara c, string lang)
        {
            string f = "x" + HeavyFactor(c).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
            bool w = HeavyByWeight(c);
            return lang == "JP" ? (w ? "重さ" : "レベル") + "が飛ぶ者(自分か味方)の" + f + "以上"
                 : lang == "CN" ? (w ? "重量" : "等级") + "至少为飞行者(你或友方)的" + f
                 : (w ? "weight" : "level") + " at least " + f + " the flier's (yours or the ally's)";
        }
        internal static bool HeavyMatters(Chara c) => string.IsNullOrEmpty(c.GetStr(KeyHeavyOff));
        internal static void SetHeavyMatters(Chara c, bool on) => c.SetStr(KeyHeavyOff, on ? null : "1");
        const string KeyTkcPath = "psion_tkc_path"; // Telekinetic Control also shields you and allies in your Psi-Hurl's path (default off)
        internal static bool TkControlPath(Chara c) => !string.IsNullOrEmpty(c.GetStr(KeyTkcPath));
        internal static void SetTkControlPath(Chara c, bool on) => c.SetStr(KeyTkcPath, on ? "1" : null);
        const string KeyBonusOff = "psion_ai_bonus_off";
        internal static bool BonusOn(Chara c) => string.IsNullOrEmpty(c.GetStr(KeyBonusOff)); // default on
        internal static void SetBonusOn(Chara c, bool on) => c.SetStr(KeyBonusOff, on ? null : "1");
        internal static void OpenConfig() => SettingsUi.Open();
    }

    // Spell XP scales with the cast's mana cost (cost x 5 + 10). The Psion cost setting is a convenience, not a training
    // penalty: Telekinesis and Psi-Strike XP is always worked out from their natural cost, and Psi-Hurl earns a
    // natural-cost Telekinesis cast's XP (success and fizzle alike).
    [HarmonyPatch(typeof(ElementContainer), nameof(ElementContainer.GetSpellExp))]
    static class TelekinesisXpFromNaturalCost
    {
        static void Postfix(ElementContainer __instance, Chara c, Element e, int costMod, ref int __result)
        {
            if (PsionCombat.RawTkCost || e == null || (e.id != PsionData.Telekinesis && e.id != PsionData.PsiHurl && e.id != PsionData.PsiStrike) || c == null || !c.HasElement(PsionData.FeatPsion)) return;
            PsionCombat.RawTkCost = true;
            try
            {
                // Psi-Hurl earns what a natural-cost Telekinesis cast earns (its XP goes to Telekinesis anyway).
                var tk = e.id != PsionData.PsiHurl ? e : (__instance.GetElement(PsionData.Telekinesis) ?? Element.Create(PsionData.Telekinesis, e.vBase));
                __result = __instance.GetSpellExp(c, tk, costMod);
            }
            catch (Exception ex) { Plugin.Report(ex); }
            finally { PsionCombat.RawTkCost = false; }
        }
    }

    // A free Telekinesis (Psion cost setting 0) still trains: the game only grants spell XP when the cast cost something,
    // so grant what the natural cost would have earned (a free Psi-Hurl earns a natural Telekinesis cast's XP). Psicraft then follows through PsicraftXp below.
    [HarmonyPatch(typeof(Chara), nameof(Chara.UseAbility), new[] { typeof(Act), typeof(Card), typeof(Point), typeof(bool) })]
    static class FreeTelekinesisStillTrains
    {
        static void Postfix(Chara __instance, Act a, bool __result)
        {
            try
            {
                var c = __instance;
                if (!__result || a == null || (a.id != PsionData.Telekinesis && a.id != PsionData.PsiHurl && a.id != PsionData.PsiStrike) || !PsionData.IsPsion(c) || a.source.lvFactor <= 0) return;
                if (a.GetCost(c).cost > 0) return; // paid: the game already granted the XP
                var tk = a.id == PsionData.PsiHurl ? c.elements.GetElement(PsionData.Telekinesis) as Act : a;
                if (tk == null) return;
                Act.Cost natural;
                PsionCombat.RawTkCost = true; try { natural = tk.GetCost(c); } finally { PsionCombat.RawTkCost = false; }
                if (natural.type != Act.CostType.MP || natural.cost <= 0) return;
                c.ModExp(tk.id, c.elements.GetSpellExp(c, tk, 100));
            }
            catch (Exception e) { Plugin.Report(e); }
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
        static bool retargeting;
        internal static Card Aim; // the ranged foe being checked as Telekinesis' target (read by PracticeTkTarget)

        static void Postfix(ActTelekinesis __instance, ref bool __result)
        {
            var cc = Act.CC; var tc = Act.TC;
            if (cc == null || !cc.IsPC || !PsionData.IsPsion(cc) || !(cc.ai is GoalCombat) || tc == null) return;
            // Auto-combat only aims Telekinesis at its combat target (usually the foe in your face). With pulling on, aim
            // it at a ranged foe instead: the combat target if it is one, else the farthest one Telekinesis can reach.
            // Act.Perform re-runs CanPerform and keeps the target set here.
            if (!retargeting && PsionCombat.TkCanPull(cc) && !PsionCombat.PullingAt(cc, tc))
            {
                var foes = EClass._map.charas.Where(c => c != tc && c.IsAliveInCurrentZone && PsionCombat.PullingAt(cc, c) && cc.CanSee(c))
                    .OrderByDescending(c => cc.Dist(c)).ToList();
                foreach (var foe in foes)
                {
                    bool ok;
                    retargeting = true;
                    try { Aim = foe; Act.TC = foe; Act.TP.Set(foe.pos); ok = __instance.CanPerform(); }
                    finally { retargeting = false; Aim = null; }
                    if (ok) { __result = true; return; }
                }
                Act.TC = tc; Act.TP.Set(tc.pos);
            }
            if (!__result) return;
            bool adjacent = tc.Dist(cc) == 1;
            var mode = PsionCombat.Tk;
            bool rangedPull = !adjacent && PsionCombat.TkPullsRanged(cc, tc);
            if (adjacent && PsionCombat.HoldClose(cc, tc)) { __result = false; return; } // keep ranged enemies close
            if (mode == PsionCombat.TkMode.Off || (mode == PsionCombat.TkMode.PullOnly && adjacent) || (mode == PsionCombat.TkMode.PushOnly && !adjacent && !rangedPull))
            { __result = false; return; }
            // ListPath is mode-correct: a pull lists the squares between target and caster (never past the target);
            // a hurl lists the squares beyond the target. Check only the path this cast will travel.
            var path = __instance.ListPath(cc.pos, tc.pos);
            // Damage-only: hurls always deal collision damage; a pull deals none, so it is allowed only as a
            // Pull-and-hurl setup whose landing square leaves room for the follow-up hurl.
            if (PsionCombat.DamageOnly(cc) && !adjacent && !rangedPull &&
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
            if (PsionCombat.RawTkCost || __instance.id != PsionData.Telekinesis || c == null || !c.HasElement(PsionData.FeatPsion) || __result.type != Act.CostType.MP) return;
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
                if (owner.IsPC) __instance.abilities.RemoveAll(a => a.act != null && !PowerConfig.AutoOn(owner, a.act.id));
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

namespace PsionClass
{
    // "You cast telekinesis on a yeek archer (pull)." / "(hurl)." and "You cast psi-hurl on a rock." Vanilla names
    // neither target nor direction. Act.TC is final when the line is said (retargets included); the target's distance
    // then decides pull (farther) or hurl (beside you). Your own tile reads "on yourself".
    [HarmonyLib.HarmonyPatch(typeof(Chara), nameof(Chara.UseAbility), new[] { typeof(Act), typeof(Card), typeof(Point), typeof(bool) })]
    static class TkCastWording
    {
        internal static Chara Caster; internal static bool Tk;
        static void Prefix(Chara __instance, Act a) { if (a is ActTelekinesis || a is ActPsiHurl) { Caster = __instance; Tk = a is ActTelekinesis; } }
        static Exception Finalizer(Exception __exception) { Caster = null; return __exception; }
    }

    [HarmonyLib.HarmonyPatch(typeof(Card), nameof(Card.Say), new[] { typeof(string), typeof(Card), typeof(string), typeof(string) })]
    static class TkCastWordingSay
    {
        static void Prefix(Card c1, ref string ref1)
        {
            try
            {
                var cc = TkCastWording.Caster; var tc = Act.TC;
                if (cc == null || c1 != cc || string.IsNullOrEmpty(ref1) || tc == null) return;
                TkCastWording.Caster = null; // once per cast
                if (tc == cc) { if (cc.IsPC) ref1 += UiText.Pick(" on yourself", "を自分に", "于自己"); return; }
                ref1 += UiText.Pick(" on ", "を", "于") + tc.Name;
                if (TkCastWording.Tk) ref1 += tc.Dist(cc) <= 1 ? UiText.Pick(" (hurl)", "(投げ)", "(抛掷)") : UiText.Pick(" (pull)", "(引き寄せ)", "(拉近)");
            }
            catch (Exception e) { Plugin.Report(e); }
        }
    }
}

namespace PsionClass
{
    // Spell success: for a Psion's own powers, Psicraft stands in for Casting, whichever is higher (never both).
    [HarmonyPatch(typeof(Chara), nameof(Chara.CalcCastingChance))]
    static class PsicraftAsCasting
    {
        [ThreadStatic] static Element current;

        static void Prefix(Element e) => current = e;
        static void Finalizer() => current = null;

        static System.Collections.Generic.IEnumerable<CodeInstruction> Transpiler(System.Collections.Generic.IEnumerable<CodeInstruction> code)
        {
            var evalue = AccessTools.Method(typeof(Card), nameof(Card.Evalue), new[] { typeof(int) });
            var helper = AccessTools.Method(typeof(PsicraftAsCasting), nameof(CastingSkill));
            var list = new System.Collections.Generic.List<CodeInstruction>(code);
            bool done = false;
            for (int i = 1; i < list.Count; i++)
            {
                if (!done && list[i].Calls(evalue) && list[i - 1].LoadsConstant(304))
                {
                    list[i] = new CodeInstruction(System.Reflection.Emit.OpCodes.Call, helper).WithLabels(list[i].labels);
                    done = true;
                }
            }
            if (!done) Plugin.Report(new Exception("PsicraftAsCasting: Casting read not found"));
            return list;
        }

        static int CastingSkill(Card c, int ele)
        {
            int v = c.Evalue(ele);
            try
            {
                if (current != null && c is Chara ch && Array.IndexOf(PsionCombat.PowerIds, current.id) >= 0 && PsionData.IsPsion(ch))
                    v = Math.Max(v, ch.Evalue(PsionData.Psicraft));
            }
            catch (Exception e) { Plugin.Report(e); }
            return v;
        }
    }
}
