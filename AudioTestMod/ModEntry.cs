using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using AudioTestMod.Config;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using AudioTestMod.Helpers;
using Microsoft.Xna.Framework.Audio;

namespace AudioTestMod
{
    internal sealed class ModEntry : Mod
    {
        internal static IModHelper ModHelper { get; set; } = null!;
        internal static IMonitor ModMonitor { get; set; } = null!;
        internal static ModConfig Config { get; set; } = null!;
        internal static Harmony Harmony { get; set; } = null!;

        public override void Entry(IModHelper helper)
        {
            ModHelper = helper;
            ModMonitor = Monitor;
            Config = helper.ReadConfig<ModConfig>();
            Harmony = new Harmony(ModManifest.UniqueID);

            Harmony.PatchAll();

            Helper.Events.Input.ButtonPressed += this.OnButtonPressed;
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (e.Button is not SButton.F5) return;

            var bank = Game1.soundBank as SoundBankWrapper;
            var realBank = bank?.soundBank;
            Cue audioCue = realBank.GetCue("wavy");
            if (audioCue is null)
            {
                Log.Info("no cue");
                return;
            }
            Log.Info($"cue: {audioCue.Name}");
            var rpcVars = audioCue?._variables;
            if (rpcVars == null)
            {
                Log.Debug("rpcVars is null");
                return;
            }
            foreach (var rpcVar in rpcVars)
            {
                Log.Debug($"rpcVar: {rpcVar.Name} {rpcVar.Value}");
            }
        }
    }
}