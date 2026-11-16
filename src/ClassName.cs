using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BepInEx;
using Newtonsoft.Json.Linq;

namespace PsionClass
{
    // The class's display name, per language, global (mod config file). Defaults: Psion / サイオン / 灵能者.
    // Only the current language is edited from the settings window; other languages keep their own names.
    internal static class ClassName
    {
        static string FilePath => Path.Combine(Paths.ConfigPath, Plugin.Id + ".json");
        static Dictionary<string, string> names;

        internal static string LangKey => Lang.langCode == "JP" ? "JP" : Lang.langCode == "CN" ? "CN" : "EN";
        internal static string Default(string lang) => lang == "JP" ? "サイオン" : lang == "CN" ? "灵能者" : "Psion";

        static Dictionary<string, string> Names
        {
            get
            {
                if (names != null) return names;
                names = new Dictionary<string, string>();
                try
                {
                    if (File.Exists(FilePath) && JObject.Parse(File.ReadAllText(FilePath))["ClassName"] is JObject o)
                        foreach (var p in o.Properties()) if (p.Value.Type == JTokenType.String) names[p.Name] = (string)p.Value;
                }
                catch (Exception e) { Plugin.Report(e); }
                return names;
            }
        }

        internal static string Get(string lang) => Names.TryGetValue(lang, out var n) && !string.IsNullOrWhiteSpace(n) ? n : Default(lang);
        internal static string Current => Get(LangKey);
        internal static bool IsCustom(string lang) => Get(lang) != Default(lang);

        // Empty (or the default) restores the default for that language.
        internal static void Set(string lang, string value)
        {
            value = (value ?? "").Trim();
            if (value.Length == 0 || value == Default(lang)) Names.Remove(lang); else Names[lang] = value;
            try
            {
                var root = File.Exists(FilePath) ? JObject.Parse(File.ReadAllText(FilePath)) : new JObject();
                var o = new JObject(); foreach (var kv in Names) o[kv.Key] = kv.Value;
                root["ClassName"] = o;
                var tmp = FilePath + ".tmp"; File.WriteAllText(tmp, root.ToString());
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null); else File.Move(tmp, FilePath);
            }
            catch (Exception e) { Plugin.Report(e); }
            Apply();
        }

        // Push the names into the job and tactics rows (character creation, character sheet, NPC tactics).
        internal static void Apply()
        {
            foreach (var row in new object[] { EClass.sources?.jobs?.map?.TryGetValue(PsionData.JobId), EClass.sources?.tactics?.map?.TryGetValue(PsionData.JobId) })
            {
                if (row == null) continue;
                var t = row.GetType();
                t.GetField("name")?.SetValue(row, Get("EN"));
                t.GetField("name_JP")?.SetValue(row, Get("JP"));
                t.GetField("name_L")?.SetValue(row, Current);
            }
        }

        // Replace the default name for the current language inside text (whole words for English, so "Psionic" is untouched).
        internal static string Rename(string text)
        {
            if (string.IsNullOrEmpty(text) || !IsCustom(LangKey)) return text;
            if (LangKey == "EN") return Regex.Replace(text, @"\bPsion(?=s\b|'|\b)", Current);
            return text.Replace(Default(LangKey), Current);
        }
    }
}
