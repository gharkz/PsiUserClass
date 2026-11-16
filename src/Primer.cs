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
                    lines.Add(line.Replace("{PSION_TKMULT}", TkMult(pc)).Replace("{PSION_TKMP}", TkMp(pc)));
                }
                item.lines = lines.ToArray();
            }
            catch (Exception e) { Plugin.Report(e); }
        }

        static string TkMult(Chara pc)
        {
            float m = pc != null && EClass.core.IsGameStarted ? PsionCombat.TkDiceMult(pc) : 2f;
            return "x" + m.ToString("0.##", CultureInfo.InvariantCulture);
        }

        static string TkMp(Chara pc)
        {
            float m = pc != null && EClass.core.IsGameStarted ? PsionCombat.TkMpMult(pc) : 0.5f;
            return "x" + m.ToString("0.##", CultureInfo.InvariantCulture);
        }

        static IEnumerable<string> Awakening(Chara pc)
        {
            bool live = pc != null && EClass.core.IsGameStarted;
            foreach (var g in PsionData.Unlocks.GroupBy(u => u.lv).OrderBy(g => g.Key))
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
