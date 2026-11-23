using System;
using BepInEx;
using HarmonyLib;

namespace PsionClass
{
    [BepInPlugin(Id, "Psion", "0.2.4")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "gharkz.elin.psion";
        internal static Plugin Instance;
        Harmony harmony;
        bool failed;

        void Awake()
        {
            Instance = this;
            harmony = new Harmony(Id);
            // Let Elin's ClassCache resolve our global-namespace classes (ActPsiTeleport, stance, ...).
            var asm = typeof(Plugin).Assembly;
            ClassCache.typeLoaders.Add(name => asm.GetType(name));
            harmony.PatchAll(asm);
            try { Icons.Load(System.IO.Path.GetDirectoryName(Info.Location)); } catch (Exception e) { Logger.LogError(e); }
            TryRegister();
        }
        void Update() { if (!failed && !PsionData.Registered) TryRegister(); }

        internal bool TryRegister()
        {
            if (PsionData.Registered) return true;
            if (failed || EClass.sources?.elements?.alias == null || !EClass.sources.elements.alias.ContainsKey("casting")
                || EClass.sources.jobs?.map == null || !EClass.sources.jobs.map.ContainsKey("wizard")) return false;
            try { PsionData.Register(); Logger.LogInfo("Psion registered."); }
            catch (Exception error) { failed = true; Logger.LogError(error); }
            return PsionData.Registered;
        }
        internal static void Report(Exception error) { Instance?.Logger.LogError(error); }
        internal static void Guard(Action action) { try { action(); } catch (Exception error) { Report(error); } }
        void OnDestroy() { harmony?.UnpatchSelf(); }
    }

    // Rows must exist before a save with Psion elements deserializes.
    [HarmonyPatch(typeof(Game), nameof(Game.Load))]
    static class RegisterBeforeLoading
    {
        [HarmonyPriority(Priority.First)]
        static void Prefix()
        {
            if (Plugin.Instance == null || !Plugin.Instance.TryRegister())
                throw new InvalidOperationException("Psion could not register before loading a world.");
        }
    }

    internal static class UiText
    {
        internal static string Pick(string en, string jp, string cn) => Lang.langCode == "JP" ? jp : Lang.langCode == "CN" ? cn : en;
    }
}
