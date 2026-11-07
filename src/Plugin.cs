using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using REPOLib.Modules;
using UnityEngine;

namespace CartCompressor
{
    [BepInPlugin("Local.CartCompressor", "Cart Compressor", "0.2.1")]
    [BepInDependency("REPOLib", "4.3.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string UpgradeId = "CartCompressor";
        internal static PlayerUpgrade Upgrade;
        internal static CargoRuntime Cargo;
        internal static ConfigEntry<bool> Enabled;
        private Harmony harmony;
        private float nextError;

        private void Awake()
        {
            Log.Source = Logger;
            Enabled = Config.Bind("Основное", "Включить улучшение", true, "Три уровня персонального сжатия груза.");
            Cargo = new CargoRuntime();
            harmony = new Harmony("Local.CartCompressor");
            harmony.PatchAll(typeof(Plugin).Assembly);
            Log.Info("Cart Compressor 0.2.1 standalone loaded. Dependency: REPOLib only. Tiers: 76.7% / 70.8% / 65%.");
        }
        private void Update()
        {
            try
            {
                CompressorShop.Tick();
                Cargo.Tick();
            }
            catch (Exception ex)
            {
                if (Time.unscaledTime >= nextError) { nextError = Time.unscaledTime + 5; Log.Error(ex.ToString()); }
            }
        }
        private void OnDestroy()
        {
            Cargo?.Dispose();
            Cargo = null;
            harmony?.UnpatchSelf();
        }
    }
    internal static class Log
    {
        internal static ManualLogSource Source;
        internal static void Info(string message) => Source?.LogInfo(message);
        internal static void Warn(string message) => Source?.LogWarning(message);
        internal static void Error(string message) => Source?.LogError(message);
    }
    [HarmonyPatch(typeof(RunManager), "ChangeLevel")]
    internal static class LevelResetPatch
    {
        private static void Prefix() => Plugin.Cargo?.Reset();
    }
}
