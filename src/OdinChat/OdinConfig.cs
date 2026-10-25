using BepInEx.Configuration;

namespace OdinChat
{
    internal enum ReplyVisibility
    {
        Public,
        Private
    }

    internal sealed class OdinConfig
    {
        internal const string PlaceholderApiKey = "sk-REPLACE_ME";

        internal const string DefaultSystemPrompt =
            "You are Odin, the Allfather, speaking to Vikings in Valheim. " +
            "Answer concisely in one to four short sentences. Be practical and in-character, " +
            "but do not overdo the theatrics. Do not use markdown, bullet points, URLs, or modern references. " +
            "If you do not know something, say so plainly. Reply in the same language as the question.";

        internal readonly ConfigEntry<string> BaseUrl;
        internal readonly ConfigEntry<string> ApiKey;
        internal readonly ConfigEntry<string> Model;
        internal readonly ConfigEntry<string> SystemPrompt;
        internal readonly ConfigEntry<float> Temperature;
        internal readonly ConfigEntry<int> MaxTokens;
        internal readonly ConfigEntry<int> TimeoutSeconds;

        internal readonly ConfigEntry<string> Trigger;
        internal readonly ConfigEntry<ReplyVisibility> ReplyVisibilityMode;
        internal readonly ConfigEntry<int> CooldownSeconds;
        internal readonly ConfigEntry<int> MaxQuestionLength;
        internal readonly ConfigEntry<int> MaxChatChunkLength;
        internal readonly ConfigEntry<int> MaxConcurrentRequests;
        internal readonly ConfigEntry<bool> ShowErrors;

        internal readonly ConfigEntry<bool> VerboseLogging;

        internal OdinConfig(ConfigFile config)
        {
            BaseUrl = config.Bind(
                "Api",
                "BaseUrl",
                "https://api.openai.com/v1",
                "OpenAI-compatible base URL. The plugin POSTs to <BaseUrl>/chat/completions. " +
                "Examples: https://api.openai.com/v1, https://openrouter.ai/api/v1, http://host.docker.internal:11434/v1");

            ApiKey = config.Bind(
                "Api",
                "ApiKey",
                PlaceholderApiKey,
                "API key for the endpoint. Keep this file private. Use a non-empty dummy value for endpoints that do not require auth.");

            Model = config.Bind(
                "Api",
                "Model",
                "gpt-4o-mini",
                "Model name understood by your endpoint.");

            SystemPrompt = config.Bind(
                "Api",
                "SystemPrompt",
                DefaultSystemPrompt,
                "System prompt sent with every request.");

            Temperature = config.Bind(
                "Api",
                "Temperature",
                0.7f,
                "Sampling temperature, normally 0 to 2.");

            MaxTokens = config.Bind(
                "Api",
                "MaxTokens",
                300,
                "Maximum tokens in the reply. Set to 0 to omit the field.");

            TimeoutSeconds = config.Bind(
                "Api",
                "TimeoutSeconds",
                60,
                "HTTP request timeout in seconds.");

            Trigger = config.Bind(
                "Chat",
                "Trigger",
                "odin",
                "Case-insensitive message prefix. The default accepts 'Odin, ...', 'odin! ...' and 'odin ...'. " +
                "A word boundary is required after the trigger.");

            ReplyVisibilityMode = config.Bind(
                "Chat",
                "ReplyVisibility",
                ReplyVisibility.Public,
                "Public: everyone sees Odin's answer. Private: only the asker sees it.");

            CooldownSeconds = config.Bind(
                "Chat",
                "CooldownSeconds",
                10,
                "Minimum seconds between AI requests from the same player.");

            MaxQuestionLength = config.Bind(
                "Chat",
                "MaxQuestionLength",
                500,
                "Maximum question length in characters.");

            MaxChatChunkLength = config.Bind(
                "Chat",
                "MaxChatChunkLength",
                400,
                "Long replies are split into chat lines of at most this many characters.");

            MaxConcurrentRequests = config.Bind(
                "Chat",
                "MaxConcurrentRequests",
                1,
                "Maximum number of AI requests in flight at once. Extra requests are rejected with a short message.");

            ShowErrors = config.Bind(
                "Chat",
                "ShowErrors",
                true,
                "Send a short private chat message to the asker when the API call fails.");

            VerboseLogging = config.Bind(
                "Debug",
                "VerboseLogging",
                false,
                "Log extra details such as trigger matches and reply chunks.");
        }
    }
}
