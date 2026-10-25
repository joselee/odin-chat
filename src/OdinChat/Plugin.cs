using System;
using System.Collections.Concurrent;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace OdinChat
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class OdinChatPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.odin.valheim.chat";
        public const string PluginName = "Odin Chat";
        public const string PluginVersion = "0.1.0";

        internal static OdinChatPlugin Instance;
        internal static ManualLogSource Log;
        internal static OdinConfig Settings;
        internal static OdinApiClient Api;
        internal static readonly ConcurrentQueue<Action> MainThreadQueue = new ConcurrentQueue<Action>();

        private Harmony _harmony;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            Settings = new OdinConfig(base.Config);
            Api = new OdinApiClient(Settings, Log);

            _harmony = new Harmony(PluginGuid);
            try
            {
                _harmony.PatchAll(typeof(OdinChatPlugin).Assembly);
                Log.LogInfo("Harmony patches applied.");
            }
            catch (Exception ex)
            {
                Log.LogError("Harmony patch failed: " + ex);
            }

            Log.LogInfo($"{PluginName} {PluginVersion} loaded.");
            Log.LogInfo($"API endpoint: {Settings.BaseUrl.Value}; model: {Settings.Model.Value}");
            if (Settings.ApiKey.Value == OdinConfig.PlaceholderApiKey)
                Log.LogWarning("ApiKey is still the placeholder. Edit BepInEx/config/" + PluginGuid + ".cfg.");
        }

        private void Update()
        {
            while (MainThreadQueue.TryDequeue(out Action action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Log.LogError("Odin main-thread action failed: " + ex);
                }
            }
        }

        private void OnDestroy()
        {
            try
            {
                _harmony?.UnpatchSelf();
            }
            catch (Exception ex)
            {
                Log.LogWarning("Harmony unpatch failed: " + ex.Message);
            }
        }

        internal static void HandleChat(long senderPeerId, UserInfo user, Talker.Type type, string text)
        {
            OdinRequestHandler.HandleChat(senderPeerId, user, type, text);
        }
    }
}
