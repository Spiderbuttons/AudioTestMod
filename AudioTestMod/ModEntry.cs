using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using AudioTestMod.Config;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using AudioTestMod.Helpers;
using Microsoft.Xna.Framework.Audio;
using MonoGame.OpenAL;
using StardewValley.GameData;
using System.Reflection;
using Microsoft.Xna.Framework.Graphics;
using StardewValley.GameData.Powers;

namespace AudioTestMod
{
    internal sealed class ModEntry : Mod
    {
        internal static IModHelper ModHelper { get; set; } = null!;
        internal static IMonitor ModMonitor { get; set; } = null!;
        internal static ModConfig Config { get; set; } = null!;
        internal static Harmony Harmony { get; set; } = null!;
        
        internal static Cue? currentMusic = null!;
        internal static EfxFilterType TargetFilterType = EfxFilterType.None;
        internal static float TargetFilterQ = 0f;
        internal static float TargetFrequency = 0f;
        internal static float TargetPitch = 0f;
        internal static float TargetVolume = 1f;

        internal static bool LoFiActive = false;

        public override void Entry(IModHelper helper)
        {
            ModHelper = helper;
            ModMonitor = Monitor;
            Config = helper.ReadConfig<ModConfig>();
            Harmony = new Harmony(ModManifest.UniqueID);

            Harmony.Patch(original: AccessTools.PropertySetter(typeof(Game1), nameof(Game1.currentSong)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ModEntry), nameof(currentSong_Prefix))));

            Harmony.Patch(original: AccessTools.Method(typeof(Cue), nameof(Cue.PlaySoundInstance)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ModEntry), nameof(PlaySoundInstance_Prefix))));
            
            Harmony.Patch(original: AccessTools.Method(typeof(Cue), nameof(Cue.Update)),
                prefix: new HarmonyMethod(AccessTools.Method(typeof(ModEntry), nameof(Update_Postfix))));

            Helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            
            Helper.Events.Content.AssetRequested += this.OnAssetRequested;
        }

        private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
        {
            //
        }

        private static void PlaySoundInstance_Prefix(Cue __instance, ref SoundEffectInstance sound_instance)
        {
            Log.Warn(__instance.Name);
            if (__instance != currentMusic || !LoFiActive) return;
            
            Log.Debug($"FilterEnabled: {sound_instance._filterEnabled}");
            Log.Debug($"FilterType: {sound_instance.filterType}");
            Log.Debug($"FilterQ: {sound_instance.filterQ}");
            Log.Debug($"Frequency: {sound_instance.frequency}");
            Log.Debug($"Pitch: {sound_instance.Pitch}");
            Log.Debug($"Volume: {sound_instance.Volume}");
            
            sound_instance._filterEnabled = true;
            sound_instance.filterType = TargetFilterType;
            sound_instance.filterQ = TargetFilterQ;
            sound_instance.frequency = TargetFrequency;

            __instance.UpdateRpcCurves();
            __instance._UpdateSoundParameters();
        }

        private static void Update_Postfix(Cue __instance, float dt)
        {
            if (__instance != currentMusic) return;
            bool isFreq = true;
            bool isQ = true;
            if (Math.Abs(__instance._soundEffect.frequency - TargetFrequency) > 10)
            {
                isFreq = false;
                __instance._soundEffect.frequency = MathHelper.Lerp(__instance._soundEffect.frequency, TargetFrequency, dt);
            }
            if (Math.Abs(__instance._soundEffect.filterQ - TargetFilterQ) > 0.1)
            {
                isQ = false;
                __instance._soundEffect.filterQ = MathHelper.Lerp(__instance._soundEffect.filterQ, TargetFilterQ, dt);
            }
            
            __instance._soundEffect.filterType = TargetFilterType;
            // if close enough to zero, disable the filter, or else it takes ages to get there bc of the lerp
            if (Math.Abs(0 - __instance._soundEffect.frequency) < 0.1) __instance._soundEffect._filterEnabled = false;
            else __instance._soundEffect._filterEnabled = true;
            
            if (isFreq && isQ)
            {
                __instance._soundEffect.filterType = TargetFilterType;
                __instance._soundEffect.filterQ = TargetFilterQ;
                __instance._soundEffect.frequency = TargetFrequency;
            }
            
            __instance._UpdateSoundParameters();
        }

        private static void ToggleLoFi()
        {
            if (LoFiActive)
            {
                DeactivateLoFi();
            }
            else
            {
                ActivateLoFi();
            }
        }

        private static void ToggleHighLow()
        {
            if (TargetFilterType == EfxFilterType.Highpass)
            {
                TargetFrequency = 500f;
                TargetFilterQ = 1f;
                TargetFilterType = EfxFilterType.Lowpass;
            }
            else
            {
                TargetFrequency = 3000f;
                TargetFilterQ = 3f;
                TargetFilterType = EfxFilterType.Highpass;
            }
        }

        private static void RotateFilters()
        {
            if (TargetFilterType == EfxFilterType.Highpass)
            {
                ActivateLowPass();
            }
            else if (TargetFilterType == EfxFilterType.Lowpass)
            {
                DeactivateLoFi();
            }
            else
            {
                ActivateHighPass();
            }
        }

        private static void ActivateLoFi()
        {
            LoFiActive = true;
            TargetFrequency = 3000;
            TargetFilterQ = 3f;
            TargetPitch = (float)Game1.random.NextDouble() * 0.01f - 0.005f;
            TargetVolume = (Game1.options.musicVolumeLevel * 100 + 20) / 100f;
            TargetFilterType = EfxFilterType.Highpass;
        }

        private static void DeactivateLoFi()
        {
            LoFiActive = false;
            TargetFrequency = 0;
            TargetFilterQ = 0;
            TargetPitch = 0;
            TargetVolume = Game1.options.musicVolumeLevel;
            TargetFilterType = EfxFilterType.None;
        }

        private static void ActivateHighPass()
        {
            TargetFrequency = 3000f;
            TargetFilterQ = 3f;
            TargetFilterType = EfxFilterType.Highpass;
        }

        private static void ActivateLowPass()
        {
            TargetFrequency = 200f;
            TargetFilterQ = 1f;
            TargetFilterType = EfxFilterType.Lowpass;
        }

        private static void currentSong_Prefix(ref CueWrapper? value)
        {
            if (value is null)
            {
                currentMusic = null;
                return;
            }
            currentMusic = value.cue;
        }

        private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            if (e.Button is SButton.F2)
            {
                ToggleLoFi();
                Log.Debug(LoFiActive);
            }

            if (e.Button is SButton.RightShift)
            {
                RotateFilters();
            }

            if (e.Button is SButton.F6)
            {
                if (Game1.currentSong is not null)
                {
                    Game1.stopMusicTrack(Game1._activeMusicContext);
                    return;
                }
                Game1.changeMusicTrack("junimoKart");
                // Game1.stopMusicTrack(MusicContext.Default);
                // if (cue is null) return;
                // if (cue.IsPlaying)
                // {
                //     cue._soundEffect.Stop();
                //     return;
                // }
                // cue.Play();
            }

            if (e.Button is SButton.F7)
            {
                if (currentMusic is null) return;
                currentMusic._soundEffect._filterEnabled = true;
                currentMusic._soundEffect.filterType = EfxFilterType.Highpass;
                currentMusic._soundEffect.filterQ = 3f;
                currentMusic._soundEffect.frequency = 3000;
                currentMusic.Volume++;
                
                // cue._soundEffect.filterType = EfxFilterType.Lowpass;
                // cue._soundEffect.filterQ = 1f;
                // cue._soundEffect.frequency = 100;
                // cue._soundEffect.Volume = 0.5f;
                
                currentMusic._soundEffect.Pitch += (float)Game1.random.NextDouble() * 0.01f - 0.005f;
                currentMusic._UpdateSoundParameters();
                
                Log.Info(currentMusic._playingCategory.Name);
            }

            if (e.Button is SButton.F8)
            {
                if (currentMusic is null) return;
                foreach (var rpcVar in currentMusic._variables)
                {
                    Log.Debug($"rpcVar: {rpcVar.Name} {rpcVar.Value}");
                }
            }

            if (e.Button is SButton.F5)
            {
                if (currentMusic is not null && currentMusic.IsPlaying) currentMusic.Stop(AudioStopOptions.Immediate);
                var bank = Game1.soundBank as SoundBankWrapper;
                var realBank = bank?.soundBank;
                currentMusic = realBank?.GetCue("woodsTheme");
                if (currentMusic is null)
                {
                    Log.Info("no cue");
                    return;
                }

                Log.Info($"cue: {currentMusic.Name}");
                var rpcVars = currentMusic._variables;
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
}