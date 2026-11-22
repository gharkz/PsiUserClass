using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using PsionClass;
using UnityEngine;

// Psi-Hurl: Telekinesis's push, aimed. Any object or creature in sight (the caster too) is flung along a line of the
// caster's choosing. Collisions work like a Telekinesis hurl: everyone on the path is knocked aside and takes half
// damage, the flung target takes full damage where it stops. Level and XP are Telekinesis's (see PsiHurlShared).
public class ActPsiHurl : Spell
{
    internal const int Range = 20, MaxFlight = 20;
    // Flight distance grows with Telekinesis level: 4 tiles, +1 per 3 levels, up to 20 (vanilla Telekinesis is unchanged).
    internal static int Flight(Chara cc) => cc != null && cc.IsPC && PsionCombat.HurlFull(cc) ? MaxFlight : Mathf.Clamp(4 + (cc?.elements.ValueWithoutLink(PsionData.Telekinesis) ?? 0) / 3, 4, MaxFlight);
    static Plan pending; // auto-combat choice made in CanPerform, consumed by Perform

    internal class Plan { public Card target; public Point aim; public int score; public int max = -1; }

    public override int PerformDistance => Range;
    public override bool ShowMapHighlight => true;

    // Target pick: your own tile is always marked (you can hurl yourself), plus the hovered target if it can be hurled.
    public override void OnMarkMapHighlights()
    {
        var pc = EClass.pc;
        if (pc == null) return;
        pc.pos.SetHighlight(8);
        var t = EClass.scene.mouseTarget.card;
        if (t != null && t != pc && Hurlable(pc, t)) t.pos.SetHighlight(2);
    }
    public override bool IsHostileAct => true;

    public override bool CanPerform()
    {
        var cc = Act.CC;
        if (cc == null) return false;
        if (cc.IsPC && !(cc.ai is GoalCombat))
        {
            var t = EClass.scene.mouseTarget.card;
            var mp = EClass.scene.mouseTarget.pos;
            if ((t == null || t.pos.Equals(cc.pos)) && mp != null && mp.Equals(cc.pos)) t = cc; // your own tile: hurl yourself
            if (t == null || !Hurlable(cc, t)) return false;
            Act.TC = t; Act.TP.Set(t.pos);
            return true;
        }
        // Auto-combat / NPCs: choose ammo and direction now.
        pending = Choose(cc, Act.TC);
        if (pending == null) return false;
        Act.TC = pending.target; Act.TP.Set(pending.target.pos);
        return true;
    }

    public override bool Perform()
    {
        var cc = Act.CC; var tc = Act.TC;
        if (cc == null || tc == null) return false;
        if (cc.IsPC && !(cc.ai is GoalCombat))
        {
            AM_PsiHurl.Begin(cc, tc, cc.IsPC ? GetCost(cc).cost : 0);
            return true;
        }
        var plan = pending; pending = null;
        if (plan == null || plan.target != tc) return false;
        Hurl(cc, tc, plan.aim, plan.max);
        return true;
    }

    internal static bool Hurlable(Chara cc, Card t)
    {
        if (t == null || !t.ExistsOnMap || t.IsMultisize) return false;
        if (t == cc.ride || t == cc.parasite) return false; // your mount or the parasite you host travel with you
        if (!t.trait.CanBeHeld || !t.trait.CanBeTeleported) return false;
        if (t.isThing && t.Thing.IsInstalled && t.Thing.trait.CanBeHeld == false) return false;
        if (t != cc && (cc.Dist(t) > Range || !cc.CanSee(t))) return false;
        return true;
    }

    // ---- flight path: from the target along the line toward the aim point, until blocked ----
    internal static List<Point> Path(Card t, Point aim, int max)
    {
        var list = new List<Point>();
        if (aim == null || aim.Equals(t.pos)) return list;
        // Extend the line well past the aim so a short aim still throws a full distance.
        int dx = aim.x - t.pos.x, dz = aim.z - t.pos.z;
        int m = Math.Max(Math.Abs(dx), Math.Abs(dz));
        var far = new Point(t.pos.x + dx * MaxFlight / m, t.pos.z + dz * MaxFlight / m);
        foreach (var p in EClass._map.ListPointsInLine(t.pos, far, MaxFlight + 1, returnOnBlocked: false))
        {
            if (p.Equals(t.pos)) continue;
            if (!p.IsValid || !p.IsInBounds) break;
            if (p.IsBlocked || (p.HasChara && p.FirstChara.IsMultisize)) break;
            list.Add(p.Copy());
            if (list.Count >= max) break;
        }
        return list;
    }

    // ---- cost and difficulty ----
    // No extra mana: Psi-Hurl costs a flat 1.5x Telekinesis wherever the target is.
    internal static int ExtraCost(Chara cc, Card t, int baseCost) => 0; // flat 1.5x Telekinesis; distance only affects difficulty

    // Telekinesis difficulty (weight vs. power) raised by distance, and by level for creatures that aren't you.
    internal int Difficulty(Chara cc, Card t)
    {
        long power = GetPower(cc);
        long limit = Math.Max(1, Math.Min(int.MaxValue, power * power));
        long n = t.SelfWeight / limit;
        if (t != cc) n = n * (100 + 15 * Math.Max(0, cc.Dist(t) - 1)) / 100 + Math.Max(0, cc.Dist(t) - 1) * 3;
        if (t is Chara ch && ch != cc) n += Math.Max(0, ch.LV - cc.LV) / 2;
        var at = t.GetCondition<ConATField>(); if (at != null) n += at.value;
        return Mathf.Clamp((int)(n / 33), 0, 3);
    }

    internal static readonly string[] DiffText = { "trivial", "easy", "hard", "very hard" };

    public override string GetText(string str = "")
    {
        var t = Act.TC;
        if (t == null || Act.CC == null) return base.GetText(str);
        return base.GetText(str) + " (" + Lang.GetList("skillDiff")[Difficulty(Act.CC, t)] + ")";
    }

    // ---- the hurl ----
    internal void Hurl(Chara cc, Card tc, Point aim, int max = -1)
    {
        Act.CC = cc; Act.TC = tc;
        PsionCombat.SoundAct = PsionData.PsiHurl;
        try { DoHurl(cc, tc, aim, max); } finally { PsionCombat.SoundAct = 0; }
    }

    void DoHurl(Chara cc, Card tc, Point aim, int max)
    {
        var list = Path(tc, aim, max > 0 ? Math.Min(max, Flight(cc)) : Flight(cc));
        if (list.Count == 0) { cc.Say("tooHeavy", tc); return; }
        int extra = ExtraCost(cc, tc, GetCost(cc).cost);
        if (extra > 0) { cc.mana.Mod(-extra); if (cc.isDead) return; }
        if (Difficulty(cc, tc) * 33 > EClass.rnd(100))
        {
            tc.PlayEffect("Element/ball_Gravity"); tc.PlaySound("gravity");
            cc.Say("tooHeavy", tc);
            return;
        }
        cc.PlaySound("telekinesis"); cc.PlayEffect("telekinesis");
        if (EClass._zone.IsCrime(cc, this) && tc.isChara) cc.pos.TryWitnessCrime(cc, tc.Chara, 5, c => EClass.rnd(3) == 0);
        bool self = tc == cc;
        bool heavyHit = false;
        PsiHurlContext.Flung = tc; PsiHurlContext.Thrower = cc;
        try
        {
            for (int i = 0; i < list.Count; i++)
            {
                var p = list[i];
                Effect.Get("telekinesis2").Play(0.1f * i, p);
                if (!p.HasChara) continue;
                foreach (var ch in p.ListCharas().ToList())
                {
                    if (ch == tc) continue;
                    if (tc is Chara flier && (self || cc.IsPC) && (!cc.IsPC || PsionCombat.HeavyMatters(cc)) && PsionCombat.IsHeavy(cc, flier, ch)) heavyHit = true; // flier vs. what it passes
                    tc.Kick(ch, ignoreSelf: true, karmaLoss: false);
                    tc.pos.PlayEffect("vanish");
                    Collide(cc, tc, ch, i + 1, 50);
                }
            }
            var last = list[list.Count - 1];
            if (last.HasChara) last.FirstChara.MoveImmediate(tc.pos);
            tc.MoveImmediate(last, focus: true, cancelAI: false);
            tc.pos.PlayEffect("telekinesis"); tc.pos.PlayEffect("vanish"); tc.pos.PlaySound("rush");
            PsiHurlContext.HeavyHit = heavyHit;
            if (tc.isChara || tc.trait.CanBeAttacked) Collide(cc, tc, tc, list.Count, 100);
            if (tc.isChara && tc != cc && tc.IsAliveInCurrentZone) tc.Chara.AddCondition<ConATField>();
        }
        finally { PsiHurlContext.Flung = null; PsiHurlContext.Thrower = null; PsiHurlContext.HeavyHit = false; }
    }

    // Telekinesis collision damage (same dice, distance and weight scaling).
    void Collide(Chara cc, Card flung, Card victim, int dist, int mtp)
    {
        if (!victim.isChara && !victim.trait.CanBeAttacked) return;
        long num = Dice.Create("SpTelekinesis", GetPower(cc), cc, this).Roll();
        num = num * (50 + dist * 25) / 100;
        if (flung.SelfWeight > 1000) num = num * (100 + (int)Mathf.Sqrt(flung.SelfWeight / 10) / 2) / 100;
        num = num * mtp / 100;
        // An ally your Telekinetic Control fully protects takes no harm, so it isn't treated as an attack on them.
        if (victim != cc && !FullyProtectedAlly(cc, victim)) cc.DoHostileAction(victim);
        victim.DamageHP(num, 925, 100, AttackSource.Throw, cc);
    }

    internal static bool FullyProtectedAlly(Chara cc, Card victim)
    {
        if (!cc.IsPC || !(victim is Chara v) || !(v.IsPCParty || v.IsPCFactionOrMinion) || cc.elements.ValueWithoutLink(PsionData.FeatTkControl) < 2) return false;
        if (v == PsiHurlContext.Flung) return PsionCombat.TkControlAllies(cc) && !PsiHurlContext.HeavyHit; // the ally you threw
        return PsionCombat.TkControlPath(cc);                                                              // an ally in the path
    }

    // ---- auto-aim: ammo x direction, hits enemies only ----
    internal static Plan Choose(Chara cc, Card primary = null)
    {
        var act = cc.elements.GetElement(PsionData.PsiHurl)?.act as ActPsiHurl ?? ACT.Create(PsionData.PsiHurl) as ActPsiHurl;
        if (act == null) return null;
        int flight = Flight(cc);
        var enemies = EClass._map.charas.Where(c => c != cc && c.IsAliveInCurrentZone && c.IsHostile(cc) && cc.Dist(c) <= Range + flight && cc.CanSee(c)).ToList();
        if (enemies.Count == 0) return null;
        if (cc.mana.value < act.GetCost(cc).cost) return null;
        if (cc.IsPC && PsionCombat.PullRanged(cc) && !PsionCombat.TkCanPull(cc) && PullPlan(cc, act, enemies, primary, flight) is Plan pull) return pull;
        var mode = cc.IsPC ? PsionCombat.HurlDir(cc) : PsionCombat.HurlDirection.Random;
        var dirPlan = DirectionPlan(cc, act, enemies, primary, mode, flight);
        if (mode != PsionCombat.HurlDirection.Random && PsionCombat.HurlDirFirst(cc) && dirPlan != null) return dirPlan;
        return CollisionPlan(cc, act, enemies, flight) ?? dirPlan;
    }

    // A non-hostile (or the caster) on the path rules a throw out; hostiles on it are hits.
    static bool Clean(Chara cc, Card ammo, List<Point> path, out int hits)
    {
        hits = 0;
        foreach (var p in path)
            foreach (var ch in p.ListCharas())
            {
                if (ch == ammo) continue;
                if (ch == cc) return false;
                if (ch.IsHostile(cc)) { hits++; continue; }
                if (!cc.IsPC || PsionCombat.Kinds(cc, ch).Any(k => PsionCombat.Blocks(cc, k))) return false;
            }
        return true;
    }

    // Throw ammo (loose objects, hostiles, optionally allies) into enemies.
    static Plan CollisionPlan(Chara cc, ActPsiHurl act, List<Chara> enemies, int flight)
    {
        var ammo = new List<Card>();
        foreach (var c in enemies) if (cc.Dist(c) <= Range && Hurlable(cc, c)) ammo.Add(c);
        if (cc.IsPC && PsionCombat.AllyAmmoAllowed(cc))
            foreach (var c in EClass._map.charas)
                if (c != cc && c.IsAliveInCurrentZone && (c.IsPCParty || c.IsPCFactionOrMinion) && cc.Dist(c) <= Range && Hurlable(cc, c)) ammo.Add(c);
        foreach (var t in EClass._map.things)
            if (!t.isNPCProperty && !t.IsInstalled && cc.Dist(t) <= Range && Hurlable(cc, t)) ammo.Add(t);
        Plan best = null;
        foreach (var a in ammo)
        {
            if (act.Difficulty(cc, a) >= 2 || HoldClose(cc, a)) continue; // 66%+ fail: not worth it; or kept close
            foreach (var e in enemies)
            {
                if (e == a || a.Dist(e) > flight) continue;
                var path = Path(a, e.pos, flight);
                if (!Clean(cc, a, path, out int hits) || hits == 0) continue;
                if (LeaveToTk(cc, a) && hits <= TkPushHits(cc, a)) continue; // Telekinesis does as well, cheaper
                int score = hits * 100 + (a is Chara ac && ac.IsHostile(cc) ? 100 : 0) + Math.Min(50, a.SelfWeight / 1000) - cc.Dist(a);
                if (best == null || score > best.score) best = new Plan { target = a, aim = e.pos.Copy(), score = score };
            }
        }
        return best;
    }

    // Hurl an enemy (the combat target first, else the nearest) away from you, toward you, or in a random direction.
    static Plan DirectionPlan(Chara cc, ActPsiHurl act, List<Chara> enemies, Card primary, PsionCombat.HurlDirection mode, int flight)
    {
        var order = enemies.Where(e => cc.Dist(e) <= Range && Hurlable(cc, e) && act.Difficulty(cc, e) < 2 && !LeaveToTk(cc, e)
            && (mode == PsionCombat.HurlDirection.Toward || !HoldClose(cc, e))).OrderBy(e => cc.Dist(e)).ToList();
        if (primary is Chara pt && order.Remove(pt)) order.Insert(0, pt);
        foreach (var e in order)
        {
            var aims = new List<(Point aim, int max)>();
            int dx = e.pos.x - cc.pos.x, dz = e.pos.z - cc.pos.z;
            if (mode == PsionCombat.HurlDirection.Away && (dx != 0 || dz != 0)) aims.Add((new Point(e.pos.x + dx, e.pos.z + dz), flight));
            else if (mode == PsionCombat.HurlDirection.Toward && TowardAim(cc, e, flight) is int stop) aims.Add((cc.pos.Copy(), stop)); // lands beside you
            else if (mode == PsionCombat.HurlDirection.Random)
                foreach (var d in Dirs.OrderBy(_ => EClass.rnd(100))) aims.Add((new Point(e.pos.x + d.x, e.pos.z + d.z), flight));
            foreach (var (aim, max) in aims)
            {
                var path = Path(e, aim, max);
                if (path.Count == 0 || !Clean(cc, e, path, out _)) continue;
                return new Plan { target = e, aim = aim, max = max };
            }
        }
        return null;
    }

    // Flight that stops beside the caster, or null when the caster isn't on the line.
    static int? TowardAim(Chara cc, Card e, int flight)
    {
        int stop = Path(e, cc.pos, flight).FindIndex(q => q.Equals(cc.pos));
        return stop > 0 ? stop : (int?)null;
    }

    // Pull a ranged / keep-away enemy (3+ tiles off) in beside you: the combat target first, else the farthest.
    static Plan PullPlan(Chara cc, ActPsiHurl act, List<Chara> enemies, Card primary, int flight)
    {
        var order = enemies.Where(e => cc.Dist(e) >= 3 && cc.Dist(e) <= Range && PsionCombat.KeepsAway(e) && Hurlable(cc, e) && act.Difficulty(cc, e) < 2)
            .OrderByDescending(e => cc.Dist(e)).ToList();
        if (primary is Chara pt && order.Remove(pt)) order.Insert(0, pt);
        foreach (var e in order)
        {
            if (!(TowardAim(cc, e, flight) is int stop)) continue;
            var path = Path(e, cc.pos, stop);
            if (path.Count == 0 || !Clean(cc, e, path, out _)) continue;
            return new Plan { target = e, aim = cc.pos.Copy(), max = stop };
        }
        return null;
    }

    static bool HoldClose(Chara cc, Card a) => PsionCombat.HoldClose(cc, a);

    // An enemy right beside a Psion who has Telekinesis: a plain Telekinesis hurl does the job for less mana.
    static bool LeaveToTk(Chara cc, Card a) =>
        cc.IsPC && PsionCombat.PreferTk(cc) && a is Chara ch && ch.IsHostile(cc) && cc.Dist(a) == 1 && cc.ability.Has(PsionData.Telekinesis);

    // Enemies a Telekinesis hurl of an adjacent target would hit (straight away from the caster).
    static int TkPushHits(Chara cc, Card a)
    {
        var away = new Point(a.pos.x + (a.pos.x - cc.pos.x), a.pos.z + (a.pos.z - cc.pos.z));
        Clean(cc, a, Path(a, away, MaxFlight), out int hits);
        return hits;
    }

    static readonly (int x, int z)[] Dirs = { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };
}

namespace PsionClass
{
    internal static class PsiHurlContext
    {
        [ThreadStatic] internal static Card Flung;
        [ThreadStatic] internal static Chara Thrower;
        [ThreadStatic] internal static bool HeavyHit;
        [ThreadStatic] internal static bool VanillaTk;
    }

    // Telekinetic Control: less collision damage for the flung character (self-hurl 50/100%, hurled by others 10/20%).
    [HarmonyPatch(typeof(Card), nameof(Card.DamageHP), new[] { typeof(long), typeof(int), typeof(int), typeof(AttackSource), typeof(Card), typeof(bool), typeof(Thing), typeof(Chara), typeof(int) })]
    static class TkControlDamage
    {
        static void Prefix(Card __instance, ref long dmg, int ele, AttackSource attackSource)
        {
            try
            {
                // Zero-damage hits still pass through (a low roll near the start of the path rounds to 0), so the shield message shows.
                if (ele != 925 || attackSource != AttackSource.Throw || dmg < 0 || !(__instance is Chara c)) return;
                var thrower = PsiHurlContext.Thrower;
                // You or an ally standing in the path of your own Psi-Hurl (optional): your control shields them from the projectile.
                if (thrower != null && thrower.IsPC && PsiHurlContext.Flung != null && c != PsiHurlContext.Flung
                    && (c == thrower || c.IsPCParty || c.IsPCFactionOrMinion) && PsionCombat.TkControlPath(thrower))
                {
                    int pr = thrower.elements.ValueWithoutLink(PsionData.FeatTkControl);
                    if (pr > 0)
                    {
                        dmg = dmg * (100 - (pr >= 2 ? 100 : 50)) / 100;
                        if (dmg <= 0) Msg.Say(c == thrower
                            ? UiText.Pick("Your mind shields you from your projectile.", "精神の力で自分の投擲物から身を守った。", "你以念力护住自己免受投掷物伤害。")
                            : UiText.Pick("Your mind shields " + c.Name + " from your projectile.", "精神の力で" + c.Name + "を投擲物から守った。", "你以念力护住" + c.Name + "免受投掷物伤害。"));
                        return;
                    }
                }
                // Allies you fling get your own self-hurl protection.
                if (thrower != null && thrower != c && PsiHurlContext.Flung == c && thrower.IsPC && (c.IsPCParty || c.IsPCFactionOrMinion)
                    && PsionCombat.TkControlAllies(thrower))
                {
                    int tr = thrower.elements.ValueWithoutLink(PsionData.FeatTkControl);
                    if (tr > 0)
                    {
                        dmg = dmg * (100 - (tr >= 2 ? (PsiHurlContext.HeavyHit ? 100 - PsionCombat.HeavyPass : 100) : 50)) / 100;
                        if (dmg <= 0 && c == PsiHurlContext.Flung) Msg.Say(UiText.Pick("Your mind steadies their landing.", "精神の力で味方の着地を整えた。", "你以念力稳住了友方的落地。"));
                        return;
                    }
                }
                int rank = c.elements.ValueWithoutLink(PsionData.FeatTkControl);
                if (rank <= 0) return;
                bool flungByHurl = PsiHurlContext.Flung == c;
                bool flungByTk = PsiHurlContext.VanillaTk && Act.TC == c;
                if (!flungByHurl && !flungByTk) return;
                int cut;
                if (flungByHurl && PsiHurlContext.Thrower == c) cut = rank >= 2 ? (PsiHurlContext.HeavyHit ? 100 - PsionCombat.HeavyPass : 100) : 50;
                else cut = rank >= 2 ? 20 : 10;
                dmg = dmg * (100 - cut) / 100;
                if (dmg <= 0 && c.IsPC) Msg.Say(UiText.Pick("Your mind steadies your landing.", "精神の力で着地を整えた。", "你以念力稳住了落地。"));
            }
            catch (Exception e) { Plugin.Report(e); }
        }
    }

    // The Telekinesis sound, muted per the Psion settings while Telekinesis, Psi-Hurl or Psi-Strike plays it.
    [HarmonyPatch(typeof(Card), nameof(Card.PlaySound))]
    static class MuteTkSound
    {
        static bool Prefix(ref string id, ref SoundSource __result)
        {
            if (id != "telekinesis" || PsionCombat.SoundAct == 0) return true;
            var swap = PsionCombat.TkSoundFor(PsionCombat.SoundAct);
            if (swap == null) { __result = null; return false; } // muted
            id = swap;                                            // default or a substitute sound
            return true;
        }
    }

    [HarmonyPatch(typeof(ActTelekinesis), nameof(ActTelekinesis.Perform))]
    static class VanillaTkContext
    {
        // A Telekinesis hurl goes through the same Telekinetic Control rules as Psi-Hurl (thrown ally, path shield).
        static void Prefix()
        {
            PsiHurlContext.VanillaTk = true; PsionCombat.SoundAct = PsionData.Telekinesis;
            if (Act.CC != null && Act.TC != null && Act.TC.Dist(Act.CC) == 1) { PsiHurlContext.Flung = Act.TC; PsiHurlContext.Thrower = Act.CC; }
        }
        static Exception Finalizer(Exception __exception)
        {
            PsiHurlContext.VanillaTk = false; PsionCombat.SoundAct = 0;
            PsiHurlContext.Flung = null; PsiHurlContext.Thrower = null; PsiHurlContext.HeavyHit = false;
            return __exception;
        }
    }

    // Telekinesis's own collision damage marks the target as attacked; skip that for allies your control fully protects.
    [HarmonyPatch(typeof(Chara), nameof(Chara.DoHostileAction))]
    static class TkProtectedAllyNoGrudge
    {
        static bool Prefix(Chara __instance, Card _tg)
        {
            try { return !(PsiHurlContext.VanillaTk && PsiHurlContext.Thrower == __instance && ActPsiHurl.FullyProtectedAlly(__instance, _tg)); }
            catch (Exception e) { Plugin.Report(e); return true; }
        }
    }

    // Psi-Hurl shares Telekinesis's level: its XP goes to Telekinesis and its level mirrors Telekinesis's.
    [HarmonyPatch(typeof(ElementContainer), nameof(ElementContainer.ModExp))]
    static class PsiHurlShared
    {
        static void Prefix(ref int ele) { if (ele == PsionData.PsiHurl) ele = PsionData.Telekinesis; }
        static void Postfix(ElementContainer __instance, int ele) { if (ele == PsionData.Telekinesis) Sync(__instance); }
        internal static void Sync(ElementContainer ec)
        {
            try
            {
                var hurl = ec.GetElement(PsionData.PsiHurl); var tk = ec.GetElement(PsionData.Telekinesis);
                if (hurl == null || tk == null) return;
                hurl.vBase = tk.vBase; hurl.vExp = tk.vExp; hurl.vPotential = 0;
            }
            catch (Exception e) { Plugin.Report(e); }
        }
    }

    // Tooltip dice: Psi-Hurl shows Telekinesis's collision dice (same roll it uses).
    [HarmonyPatch(typeof(Dice), nameof(Dice.Create), new[] { typeof(Element), typeof(Card) })]
    static class PsiHurlDice
    {
        static void Postfix(Element ele, Card c, ref Dice __result)
        {
            if (ele?.id != PsionData.PsiHurl || !(c is Chara ch)) return;
            try
            {
                var tk = ch.elements.GetElement(PsionData.Telekinesis) ?? Element.Create(PsionData.Telekinesis, ele.vBase);
                __result = Dice.Create(tk, c);
            }
            catch (Exception e) { Plugin.Report(e); }
        }
    }

    // Psi-Hurl cost: x multiplier of the current or natural Telekinesis cost, or a fixed cost (default 1.5x current).
    [HarmonyPatch(typeof(Element), nameof(Element.GetCost))]
    static class PsiHurlCost
    {
        static void Postfix(Element __instance, Chara c, ref Act.Cost __result)
        {
            if (__instance.id != PsionData.PsiHurl || c == null) return;
            try
            {
                __result.type = Act.CostType.MP;
                var mode = PsionCombat.HurlCostMode(c);
                if (mode == PsionCombat.HurlCost.Fixed) { __result.cost = PsionCombat.HurlFixed(c); return; }
                var tk = c.elements.GetElement(PsionData.Telekinesis) ?? c.elements.GetOrCreateElement(PsionData.Telekinesis);
                int tkCost;
                if (mode == PsionCombat.HurlCost.NaturalTk) { PsionCombat.RawTkCost = true; try { tkCost = tk.GetCost(c).cost; } finally { PsionCombat.RawTkCost = false; } }
                else tkCost = tk.GetCost(c).cost;
                float m = PsionCombat.HurlMult(c);
                __result.cost = m <= 0 ? 0 : Math.Max(1, (int)Math.Ceiling(tkCost * m));
            }
            catch (Exception e) { Plugin.Report(e); }
        }
    }
}

// Choose the flight direction: click any tile; the target flies along that line until something stops it.
// Choose the flight direction: right-click a tile to hurl along that line (like casting); left or middle click cancels.
public class AM_PsiHurl : AM_ViewMap
{
    static AM_PsiHurl instance;
    Chara cc; Card tc; int refund; bool done; int startFrame;

    internal static void Begin(Chara cc, Card tc, int cost)
    {
        instance = instance ?? new AM_PsiHurl();
        instance.cc = cc; instance.tc = tc; instance.refund = cost; instance.done = false;
        instance.startFrame = Time.frameCount;
        instance.Activate();
        Msg.Say(UiText.Pick("Right-click where to hurl (left or middle click cancels).", "放り飛ばす先を右クリック(左・中クリックで中止)。", "右键点击抛出方向(左键或中键取消)。"));
    }

    public override HitResult HitTest(Point point, Point start)
    {
        if (point == null || !point.IsValid || tc == null || point.Equals(tc.pos)) return HitResult.Invalid;
        return ActPsiHurl.Path(tc, point, ActPsiHurl.Flight(cc)).Count > 0 ? HitResult.Valid : HitResult.Warning;
    }

    public override void OnUpdateInput()
    {
        // No base call: its left click would pick the tile and its middle click toggles roofs.
        if (done) return;
        var hp = EClass.scene.mouseTarget.pos;
        bool fresh = Time.frameCount <= startFrame + 1; // ignore the click that started the cast
        if (!fresh && (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(2)))
        {
            EInput.leftMouse.Consume(); EInput.Consume();
            Cancel();
            return;
        }
        var path = (tc == null || hp == null || !hp.IsValid || hp.Equals(tc.pos)) ? null : ActPsiHurl.Path(tc, hp, ActPsiHurl.Flight(cc));
        if (!fresh && Input.GetMouseButtonDown(1))
        {
            EInput.rightMouse.Consume();
            if (path != null && path.Count > 0) Fire(hp.Copy());
            return;
        }
        if (path == null) return;
        // Highlights are hidden in this view mode, so draw the bolt line straight into the floor guide pass.
        var pass = EClass.screen.guide.passGuideFloor;
        pass.Add(tc.pos, (float)HitResult.Warning, 0.6f);
        for (int i = 0; i < path.Count; i++) pass.Add(path[i], (float)(i == path.Count - 1 ? HitResult.Invalid : HitResult.Valid), 0.7f);
    }

    public override void OnProcessTiles(Point point, int dir) { } // left click cancels instead (handled above)

    void Fire(Point point)
    {
        done = true;
        Deactivate();
        if (tc == null || !tc.ExistsOnMap) { if (refund > 0) cc.mana.Mod(refund); return; }
        var act = cc.elements.GetElement(PsionData.PsiHurl)?.act as ActPsiHurl ?? (ActPsiHurl)ACT.Create(PsionData.PsiHurl);
        act.Hurl(cc, tc, point);
    }

    void Cancel()
    {
        if (!done && refund > 0) cc.mana.Mod(refund);
        done = true;
        Deactivate();
        Msg.Say(UiText.Pick("You let it go.", "念動を解いた。", "你松开了念力。"));
    }

    // The game's own cancel (right click / Escape): right click is our fire button, so only Escape cancels here.
    public override void OnCancel()
    {
        if (done) return;
        if (Input.GetMouseButton(1) && !Input.GetKey(KeyCode.Escape)) return;
        Cancel();
    }
}
