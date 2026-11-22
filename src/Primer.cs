using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;

namespace PsionClass
{
    // The Psion's Primer reflects the live settings: the awakening list (with powers switched off) and the
    // Telekinesis dice multiplier. Regenerated from the file every time the book is opened.
    [HarmonyPatch(typeof(UIBook), nameof(UIBook.BuildPages))]
    static class PrimerLive
    {
        static void Prefix(UIBook __instance)
        {
            try
            {
                var item = __instance.bookItem;
                if (item?.id != "guide_psion" || item.path == null) return;
                var pc = EClass.pc;
                var raw = IO.LoadTextArray(item.path);
                var lines = new List<string>();
                foreach (var line in raw)
                {
                    if (line.Trim() == "{PSION_AWAKENING}") { lines.AddRange(Awakening(pc)); continue; }
                    var l = line.Replace("{PSION_TKMULT}", TkMult(pc)).Replace("{PSION_TKMP}", TkMp(pc));
                    foreach (var kv in Live(pc)) l = l.Replace(kv.Key, kv.Value);
                    if (line.Trim().StartsWith("{PSION_") && l.Trim().Length == 0) continue; // a live line switched off by settings
                    lines.Add(ClassName.Rename(l));
                }
                item.lines = lines.ToArray();
                BookList.GetItem(item.id, item.cat); // refreshes title/author with the current class name
            }
            catch (Exception e) { Plugin.Report(e); }
        }

        // Live sentences for the Psi-Hurl / Psi-Strike pages, following the current Psion settings.
        static Dictionary<string, string> Live(Chara pc)
        {
            bool live = pc != null && EClass.core.IsGameStarted;
            string X(float v) => "x" + v.ToString("0.##", CultureInfo.InvariantCulture);
            var d = new Dictionary<string, string>();
            bool full = live && PsionCombat.HurlFull(pc);
            d["{PSION_HURL_FLIGHT}"] = full
                ? UiText.Pick("It always flies the full 20 tiles (Psion settings).", "常に最大の20マス飛ぶ(サイオン設定)。", "始终飞行完整的20格(灵能者设置)。")
                : UiText.Pick("It flies 4 tiles, +1 per 3 Telekinesis levels, up to 20.", "飛距離は4マス、テレキネシス3レベルごとに+1、最大20。", "飞行4格,念动术每3级+1,最多20。");
            var mode = live ? PsionCombat.HurlCostMode(pc) : PsionCombat.HurlCost.CurrentTk;
            float m = live ? PsionCombat.HurlMult(pc) : 1.5f;
            d["{PSION_HURL_COST}"] = mode == PsionCombat.HurlCost.Fixed
                ? UiText.Pick("It costs a fixed " + PsionCombat.HurlFixed(pc) + " mana.", "消費は固定" + PsionCombat.HurlFixed(pc) + "マナ。", "固定消耗" + PsionCombat.HurlFixed(pc) + "点魔力。")
                : m <= 0 ? UiText.Pick("It costs no mana.", "マナを消費しない。", "不消耗魔力。")
                : mode == PsionCombat.HurlCost.NaturalTk
                    ? UiText.Pick("It costs " + X(m) + " Telekinesis's natural mana cost.", "消費はテレキネシス本来の消費の" + X(m) + "。", "消耗为念动术原始消耗的" + X(m) + "。")
                    : UiText.Pick("It costs " + X(m) + " Telekinesis's mana.", "消費はテレキネシスの" + X(m) + "。", "消耗为念动术的" + X(m) + "。");
            bool allies = !live || PsionCombat.TkControlAllies(pc);
            d["{PSION_HURL_DANGER}"] = allies
                ? UiText.Pick("Hurling yourself or allies is dangerous. You and they take the landing hit like anyone else, unless you have learned Telekinetic Control, which protects you both.",
                    "自分や味方を投げるのは危険だ。着地の衝撃は誰とも同じく受ける。念動制御を身につければ自分も味方も守れる。",
                    "投掷自己或友方很危险。你和他们都会像其他人一样承受落地冲击,除非你已习得念动控制:它同时保护你和他们。")
                : UiText.Pick("Hurling yourself or allies is dangerous. You take the landing hit like anyone else unless you have learned Telekinetic Control; your allies always do.",
                    "自分や味方を投げるのは危険だ。念動制御を身につけるまで着地の衝撃は誰とも同じく受ける。味方は常に受ける。",
                    "投掷自己或友方很危险。在习得念动控制前你会像其他人一样承受落地冲击;友方则总是承受。");
            bool path = live && PsionCombat.TkControlPath(pc);
            string a1 = allies ? UiText.Pick("It shields allies you throw the same way.", "投げた味方も同じく守る。", "也以同样方式保护你投掷的友方。") : "";
            string a2 = path ? UiText.Pick(" It also shields you and allies standing in the path of your projectile.", "投擲物の経路上にいる自分と味方も守る。", "也保护投掷物路径上的你和友方。") : "";
            d["{PSION_TKC_ALLIES}"] = (a1 + a2).Trim();
            bool heavy = !live || PsionCombat.HeavyMatters(pc);
            d["{PSION_TKC_RANK2}"] = heavy
                ? UiText.Pick("* Rank 2 (Psicraft 10): your self-hurl collisions don't hurt, except that passing through a creature whose " + PsionCombat.HeavyRule(pc, "EN") + " still lets 20% of the landing damage through; when others hurl you, 20% less.",
                    "* ランク2(サイクラフト10): 自分を投げた時の衝突ダメージなし。ただし" + PsionCombat.HeavyRule(pc, "JP") + "の生物を通り抜けると、着地ダメージの20%は受ける。他者に投げられた時は20%減。",
                    "* 等级2(灵能术10): 自我投掷不受碰撞伤害,但穿过" + PsionCombat.HeavyRule(pc, "CN") + "的生物时仍受20%的落地伤害;被他人投掷时减少20%。")
                : UiText.Pick("* Rank 2 (Psicraft 10): your self-hurl collisions don't hurt at all; when others hurl you, 20% less.",
                    "* ランク2(サイクラフト10): 自分を投げた時の衝突ダメージは一切なし。他者に投げられた時は20%減。",
                    "* 等级2(灵能术10): 自我投掷完全不受碰撞伤害;被他人投掷时减少20%。");
            int sc = live ? PsionCombat.StrikeCost(pc) : 12;
            d["{PSION_STRIKE_COST}"] = UiText.Pick("It costs a fixed " + sc + " mana.", "消費は固定" + sc + "マナ。", "固定消耗" + sc + "点魔力。");
            return d;
        }

        static string TkMult(Chara pc)
        {
            float m = pc != null && EClass.core.IsGameStarted ? PsionCombat.TkDiceMult(pc) : 1.5f;
            return "x" + m.ToString("0.##", CultureInfo.InvariantCulture);
        }

        static string TkMp(Chara pc)
        {
            bool live = pc != null && EClass.core.IsGameStarted;
            if (live && PsionCombat.TkFixed(pc) is int f) return UiText.Pick("a fixed " + f + " MP", "固定" + f + "MP", "固定" + f + "点");
            float m = live ? PsionCombat.TkMpMult(pc) : 0.5f;
            string x = "x" + m.ToString("0.##", CultureInfo.InvariantCulture);
            return UiText.Pick(x + " the mana", x, x);
        }

        static IEnumerable<string> Awakening(Chara pc)
        {
            bool live = pc != null && EClass.core.IsGameStarted;
            var levels = PsionData.Unlocks.GroupBy(u => u.id).Select(x => (lv: live ? PowerConfig.Level(pc, x.Key) : x.First().lv, id: x.Key));
            foreach (var g in levels.GroupBy(u => u.lv).OrderBy(x => x.Key))
            {
                var names = g.Select(u =>
                {
                    string n = EClass.sources.elements.map[u.id].GetName().ToTitleCase();
                    if (live && !PowerConfig.Enabled(pc, u.id)) n += "<color=#a02818>" + UiText.Pick(" (off: spellbook only)", "(オフ: 魔法書のみ)", "(关闭: 仅魔法书)") + "</color>";
                    return n;
                });
                yield return "* " + UiText.Pick("Psicraft ", "サイクラフト", "灵能术") + g.Key + ": " + string.Join(UiText.Pick(", ", "、", "、"), names);
            }
        }
    }
}

namespace PsionClass
{
    // Every lookup of the primer (inventory name, tooltips, the open book) gets the current class name.
    // Originals are remembered per catalogue entry so later renames start from the default text, not a stale one.
    [HarmonyLib.HarmonyPatch(typeof(BookList), nameof(BookList.GetItem))]
    static class PrimerTitleLive
    {
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<BookList.Item, string[]> originals =
            new System.Runtime.CompilerServices.ConditionalWeakTable<BookList.Item, string[]>();
        static void Postfix(BookList.Item __result)
        {
            if (__result?.id != "guide_psion") return;
            var o = originals.GetValue(__result, it => new[] { it.title, it.author });
            __result.title = ClassName.Rename(o[0]);
            __result.author = ClassName.Rename(o[1]);
        }
    }
}
