using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace PsionClass.Support
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using UnityEngine;
    // Shared "Bug reports" helper (same file in each gharkz mod, namespace changed):
    // System > Mod > Bug reports > <mod> opens a preview with Summary / Mods / Log tabs and copy buttons.
    internal static class BugReport
    {
        internal const string IssuesUrl = "https://github.com/gharkz/elin-mods-support/issues/new/choose";
        static string name, id, version, harmonyId; static Func<IEnumerable<string>> settings;
        static readonly List<string> errors = new List<string>();
        static int tab; static UiTable.Window win;

        internal static void Init(string modName, string modId, string modVersion, string harmonyOwner, Func<IEnumerable<string>> settingLines = null)
        {
            name = modName; id = modId; version = modVersion; harmonyId = harmonyOwner; settings = settingLines;
            ModUtil.AddContextMenuEntry(() => Plugin.Guard(Open), "Bug reports/" + modId, modName);
        }

        // Called from the mod's error handler: the last few errors go into the report.
        internal static void Note(Exception e)
        {
            lock (errors)
            {
                errors.Add(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + e.GetType().Name + ": " + e.Message + Environment.NewLine + "    " + (e.StackTrace ?? "").Split('\n').FirstOrDefault()?.Trim());
                if (errors.Count > 10) errors.RemoveAt(0);
            }
        }

        static string GameVersion() { try { return EClass.core.version.GetText(); } catch { return "?"; } }

        static List<string> Summary()
        {
            var l = new List<string> { name + " " + version + "  (" + id + ")", "Elin " + GameVersion(), "Language: " + Lang.langCode };
            try
            {
                // Just the count: game method names stay out of public reports, and some mods skip optional hooks on purpose.
                int owned = HarmonyLib.Harmony.GetAllPatchedMethods().Count(m => HarmonyLib.Harmony.GetPatchInfo(m)?.Owners.Contains(harmonyId) == true);
                l.Add("Hooks applied: " + owned);
            }
            catch (Exception e) { l.Add("Hooks: could not read (" + e.Message + ")"); }
            if (settings != null) try { foreach (var s in settings()) l.Add("Setting: " + s); } catch { }
            lock (errors) { l.Add(errors.Count == 0 ? "Errors caught this session: none" : "Errors caught this session (" + errors.Count + "):"); l.AddRange(errors); }
            return l;
        }

        static List<string> Mods()
        {
            var l = new List<string>();
            try
            {
                foreach (var p in EClass.core.mods.packages.Where(p => p.activated))
                {
                    string where = p.dirInfo == null ? "?" : p.dirInfo.FullName.Replace('/', Path.DirectorySeparatorChar).Contains(Path.DirectorySeparatorChar + "workshop" + Path.DirectorySeparatorChar) ? "workshop" : "local";
                    l.Add(p.title + "  " + p.version + "  (" + p.id + ", " + where + ")");
                }
            }
            catch (Exception e) { l.Add("Could not list mods: " + e.Message); }
            return l;
        }

        static List<string> Log(bool mineOnly)
        {
            try
            {
                string path = Path.Combine(BepInEx.Paths.BepInExRootPath, "LogOutput.log");
                if (!File.Exists(path)) return new List<string> { "(no LogOutput.log found)" };
                string[] all;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var sr = new StreamReader(fs)) all = sr.ReadToEnd().Split('\n').Select(s => s.TrimEnd('\r')).ToArray();
                if (!mineOnly) return all.Skip(Math.Max(0, all.Length - 400)).ToList();
                var mine = new List<string>(); bool inBlock = false;
                foreach (var line in all)
                {
                    bool hit = line.Contains(name) || line.Contains(id) || line.Contains(typeof(BugReport).Namespace + ".");
                    if (hit) { mine.Add(line); inBlock = line.Contains("[Error") || line.Contains("Exception"); }
                    else if (inBlock && line.StartsWith("  at ")) mine.Add(line);
                    else inBlock = false;
                }
                return mine.Count == 0 ? new List<string> { "(nothing from this mod in the log)" } : mine;
            }
            catch (Exception e) { return new List<string> { "Could not read the log: " + e.Message }; }
        }

        static string Section(string title, IEnumerable<string> lines) => "### " + title + Environment.NewLine + string.Join(Environment.NewLine, lines) + Environment.NewLine;
        static string SummaryText() => Section("Summary", Summary());
        static string Everything() => SummaryText() + Environment.NewLine + Section("Mods", Mods()) + Environment.NewLine + Section("Log (" + name + ")", Log(true)) + Environment.NewLine + Section("Log (last 400 lines)", Log(false));

        static void Copy(string text, string what)
        {
            GUIUtility.systemCopyBuffer = text;
            Msg.Say(Plugin.T(what + " copied to the clipboard (" + text.Length + " characters).", what + "をクリップボードにコピーしました(" + text.Length + "文字)。", what + "已复制到剪贴板(" + text.Length + "字符)。"));
            EClass.Sound.Play("click_paper");
        }

        internal static void Open()
        {
            string[] tabs = { Plugin.T("Summary", "概要", "概要"), Plugin.T("Mods List", "Mod一覧", "模组列表"), Plugin.T("Log (this mod)", "ログ(このMod)", "日志(本模组)"), Plugin.T("Log (full)", "ログ(全体)", "日志(全部)") };
            var lines = tab == 0 ? Summary() : tab == 1 ? Mods() : Log(tab == 2);
            var rows = new List<UiRow> { new UiRow { Tabs = tabs, ActiveTab = tab, TabClick = i => { tab = i; Open(); } } };
            foreach (var line in lines) rows.Add(new UiRow { Label = line.Length > 220 ? line.Substring(0, 220) + "..." : line, Note = true, Info = true });
            var corner = new List<(string, Action)>
            {
                (Plugin.T("Open issues page (GitHub)", "報告ページを開く(GitHub)", "打开反馈页面(GitHub)"), () => Application.OpenURL(IssuesUrl)),
                (Plugin.T("Copy this tab", "このタブをコピー", "复制此页"), () => Copy(Section(tabs[tab], lines), tabs[tab])),
                (Plugin.T("Copy everything", "すべてコピー", "全部复制"), () => Copy(Everything(), Plugin.T("Full report", "全文", "完整报告"))),
            };
            string note = Plugin.T("Copy everything (or one tab) and paste it into a new issue on the issues page.",
                "すべて(または一つのタブ)をコピーし、報告ページの新しい投稿に貼り付けてください。",
                "复制全部(或某一页),然后粘贴到反馈页面的新帖中。");
            if (win?.Layer != null && win.Layer.gameObject != null && win.Layer.gameObject.activeInHierarchy) win.Layer.Close();
            win = UiTable.Open(Plugin.T("Bug report", "不具合報告", "问题报告") + ": " + name, rows, new[] { 0f, 1f }, 900, 680, footNote: note, corner: corner);
        }
    }
}
