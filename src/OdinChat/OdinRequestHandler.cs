using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace OdinChat
{
    internal static class OdinRequestHandler
    {
        private static readonly ConcurrentDictionary<long, DateTime> LastRequest = new ConcurrentDictionary<long, DateTime>();
        private static int _inFlight;

        internal static void HandleChat(long senderPeerId, UserInfo user, Talker.Type type, string text)
        {
            if (senderPeerId == 0L)
                return;

            OdinConfig settings = OdinChatPlugin.Settings;
            if (settings == null)
                return;

            if (!OdinTrigger.TryExtractQuestion(text, settings.Trigger.Value, out string question))
                return;

            if (string.IsNullOrWhiteSpace(question))
            {
                OdinReply.Send(senderPeerId, "Ask me something, Viking. Try: Odin, how do I tame a boar?", privateReply: true);
                return;
            }

            int maxQuestionLength = Math.Max(20, settings.MaxQuestionLength.Value);
            if (question.Length > maxQuestionLength)
            {
                OdinReply.Send(senderPeerId,
                    $"Your question is too long. Keep it under {maxQuestionLength} characters.",
                    privateReply: true);
                return;
            }

            DateTime now = DateTime.UtcNow;
            if (LastRequest.TryGetValue(senderPeerId, out DateTime last) &&
                (now - last).TotalSeconds < Math.Max(0, settings.CooldownSeconds.Value))
            {
                if (settings.VerboseLogging.Value)
                    OdinChatPlugin.Log.LogInfo($"Ignoring Odin request from peer {senderPeerId} during cooldown.");
                return;
            }
            LastRequest[senderPeerId] = now;

            int maxConcurrent = Math.Max(1, settings.MaxConcurrentRequests.Value);
            if (Interlocked.Increment(ref _inFlight) > maxConcurrent)
            {
                Interlocked.Decrement(ref _inFlight);
                OdinReply.Send(senderPeerId,
                    "Odin is already pondering another question. Wait a moment.",
                    privateReply: true);
                return;
            }

            string playerName = GetPlayerName(senderPeerId, user);
            string logQuestion = question.Length <= 200 ? question : question.Substring(0, 200) + "...";
            OdinChatPlugin.Log.LogInfo($"Odin request from {playerName} ({type}): {logQuestion}");

            Task.Run(async () =>
            {
                string reply = null;
                try
                {
                    reply = await OdinChatPlugin.Api
                        .AskAsync(playerName, question, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    OdinChatPlugin.Log.LogWarning("Odin API request failed: " + ex.Message);
                }
                finally
                {
                    Interlocked.Decrement(ref _inFlight);
                }

                OdinChatPlugin.MainThreadQueue.Enqueue(() =>
                {
                    if (string.IsNullOrWhiteSpace(reply))
                    {
                        if (settings.ShowErrors.Value)
                        {
                            OdinReply.Send(senderPeerId,
                                "Odin could not answer right now. Check the server log for details.",
                                privateReply: true);
                        }
                        return;
                    }

                    bool privateReply = settings.ReplyVisibilityMode.Value == ReplyVisibility.Private;
                    OdinReply.Send(senderPeerId, reply, privateReply);
                });
            });
        }

        private static string GetPlayerName(long senderPeerId, UserInfo user)
        {
            try
            {
                ZNetPeer peer = ZNet.instance?.GetPeer(senderPeerId);
                if (peer != null && !string.IsNullOrWhiteSpace(peer.m_playerName))
                    return peer.m_playerName;
            }
            catch
            {
                // Fall through to the packet-provided name.
            }

            if (user != null && !string.IsNullOrWhiteSpace(user.Name))
                return user.Name;

            return "Viking";
        }
    }
}
