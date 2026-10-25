using System;
using HarmonyLib;
using UnityEngine;

namespace OdinChat
{
    /// <summary>
    /// Maintains the synthetic [Odin] player entry while at least one real peer is connected.
    /// The entry is a chat recipient for vanilla clients and also the sender identity for replies.
    /// </summary>
    [HarmonyPatch(typeof(ZNet), "UpdatePlayerList")]
    internal static class OdinFakePlayerPatch
    {
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ZNet __instance)
        {
            try
            {
                if (__instance == null || !__instance.IsServer())
                    return;

                // The fake is only needed while somebody can chat. Vanilla clears the
                // player list at the start of UpdatePlayerList, so this also removes it
                // automatically when the last real player disconnects.
                if (__instance.GetPeerConnections() <= 0)
                    return;

                var players = __instance.GetPlayerList();
                if (players == null)
                    return;

                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].m_userInfo.m_id == OdinIdentity.UserId)
                        return;
                }

                var entry = new ZNet.PlayerInfo
                {
                    m_name = OdinIdentity.DisplayName,
                    m_characterID = new ZDOID(OdinIdentity.CharacterUserId, OdinIdentity.CharacterId),
                    m_publicPosition = false,
                    m_position = Vector3.zero,
                    m_userInfo = new ZNet.CrossNetworkUserInfo
                    {
                        m_id = OdinIdentity.UserId,
                        m_displayName = OdinIdentity.DisplayName,
                        m_serverAssignedDisplayName = OdinIdentity.DisplayName,
                        m_playfabId = string.Empty
                    }
                };

                players.Add(entry);

                if (OdinChatPlugin.Settings.VerboseLogging.Value)
                    OdinChatPlugin.Log.LogInfo("Injected synthetic [Odin] player entry.");
            }
            catch (Exception ex)
            {
                OdinChatPlugin.Log.LogError("Could not inject the [Odin] player entry: " + ex);
            }
        }
    }

    /// <summary>
    /// Keeps GetNrOfPlayers based on real players only. This matters for the world
    /// clock, the 10-player capacity check, and one-per-player loot drops.
    /// </summary>
    [HarmonyPatch(typeof(ZNet), nameof(ZNet.GetNrOfPlayers))]
    internal static class OdinPlayerCountPatch
    {
        [HarmonyPostfix]
        private static void Postfix(ZNet __instance, ref int __result)
        {
            try
            {
                if (__instance == null)
                    return;

                var players = __instance.GetPlayerList();
                if (players == null)
                    return;

                int fakeCount = 0;
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i].m_userInfo.m_id == OdinIdentity.UserId)
                        fakeCount++;
                }

                if (fakeCount > 0)
                    __result = Math.Max(0, __result - fakeCount);
            }
            catch
            {
                // Never let a cosmetic player entry break the vanilla count.
            }
        }
    }
}
