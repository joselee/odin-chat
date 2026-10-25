using System;
using System.Collections.Generic;
using UnityEngine;

namespace OdinChat
{
    internal static class OdinReply
    {
        internal static void Send(long askerPeerId, string text, bool privateReply)
        {
            if (ZRoutedRpc.instance == null || ZNet.instance == null || !ZNet.instance.IsServer())
                return;

            if (string.IsNullOrWhiteSpace(text))
                return;

            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            if (text.Length == 0)
                return;

            int maxLength = Math.Max(50, Math.Min(1000, OdinChatPlugin.Settings.MaxChatChunkLength.Value));
            List<string> chunks = Split(text, maxLength);
            long target = privateReply ? askerPeerId : ZRoutedRpc.Everybody;

            for (int i = 0; i < chunks.Count; i++)
            {
                var sender = new UserInfo
                {
                    Name = OdinIdentity.DisplayName,
                    UserId = OdinIdentity.UserId
                };

                ZRoutedRpc.instance.InvokeRoutedRPC(
                    target,
                    "ChatMessage",
                    new Vector3(0f, 1000f, 0f),
                    (int)Talker.Type.Normal,
                    sender,
                    chunks[i]);
            }

            if (OdinChatPlugin.Settings.VerboseLogging.Value)
                OdinChatPlugin.Log.LogInfo($"Sent Odin reply to {(privateReply ? "asker" : "everyone")} in {chunks.Count} chunk(s).");
        }

        private static List<string> Split(string text, int maxLength)
        {
            var chunks = new List<string>();
            int position = 0;

            while (position < text.Length)
            {
                int remaining = text.Length - position;
                if (remaining <= maxLength)
                {
                    chunks.Add(text.Substring(position).Trim());
                    break;
                }

                int searchEnd = position + maxLength;
                int breakAt = text.LastIndexOf(' ', searchEnd - 1, maxLength);
                if (breakAt <= position)
                    breakAt = searchEnd;

                chunks.Add(text.Substring(position, breakAt - position).Trim());
                position = breakAt;

                while (position < text.Length && text[position] == ' ')
                    position++;
            }

            chunks.RemoveAll(string.IsNullOrWhiteSpace);
            return chunks;
        }
    }
}
