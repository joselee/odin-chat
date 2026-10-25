using System;
using HarmonyLib;

namespace OdinChat
{
    /// <summary>
    /// Reads routed chat packets that vanilla clients send to the synthetic [Odin]
    /// player entry. The server sees these packets because it is the routed-RPC hub;
    /// it then drops them because no real peer owns the fake character id.
    /// </summary>
    [HarmonyPatch(typeof(ZRoutedRpc.RoutedRPCData), nameof(ZRoutedRpc.RoutedRPCData.Deserialize))]
    internal static class OdinChatCapturePatch
    {
        private static readonly int SayHash = StringExtensionMethods.GetStableHashCode("Say");
        private static readonly int ChatMessageHash = StringExtensionMethods.GetStableHashCode("ChatMessage");

        [HarmonyPostfix]
        private static void Postfix(ZRoutedRpc.RoutedRPCData __instance)
        {
            try
            {
                if (ZNet.instance == null || !ZNet.instance.IsServer())
                    return;

                if (__instance == null || __instance.m_targetPeerID != OdinIdentity.CharacterUserId)
                    return;

                if (__instance.m_methodHash != SayHash && __instance.m_methodHash != ChatMessageHash)
                    return;

                ZPackage package = __instance.m_parameters;
                if (package == null)
                    return;

                int position = package.GetPos();
                try
                {
                    Talker.Type type;
                    UserInfo user;
                    string text;

                    if (__instance.m_methodHash == SayHash)
                    {
                        // Talker.RPC_Say(long sender, int type, UserInfo user, string text)
                        type = (Talker.Type)package.ReadInt();
                        user = new UserInfo();
                        user.Deserialize(ref package);
                        text = package.ReadString();
                    }
                    else
                    {
                        // ChatMessage: (Vector3 position, int type, UserInfo user, string text)
                        package.ReadSingle();
                        package.ReadSingle();
                        package.ReadSingle();
                        type = (Talker.Type)package.ReadInt();
                        user = new UserInfo();
                        user.Deserialize(ref package);
                        text = package.ReadString();
                    }

                    if (type != Talker.Type.Normal && type != Talker.Type.Shout)
                        return;

                    OdinChatPlugin.HandleChat(__instance.m_senderPeerID, user, type, text);
                }
                finally
                {
                    package.SetPos(position);
                }
            }
            catch (Exception ex)
            {
                OdinChatPlugin.Log.LogError("Could not read an Odin chat packet: " + ex);
            }
        }
    }
}
