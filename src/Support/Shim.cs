using System;

namespace PsionClass.Support
{
    // Local helpers for the shared bug-report files, so they don't depend on each mod's own Plugin class.
    internal static class Plugin
    {
        internal static void Guard(Action a) { try { a(); } catch (Exception e) { BugReport.Note(e); UnityEngine.Debug.LogException(e); } }
        internal static string T(string en, string jp, string cn) => Lang.langCode == "JP" ? jp : Lang.langCode == "CN" ? cn : en;
    }
}
