using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace OdinChat
{
    internal sealed class OdinApiClient
    {
        private static readonly HttpClient Http = CreateHttpClient();

        private readonly OdinConfig _config;
        private readonly ManualLogSource _log;

        internal OdinApiClient(OdinConfig config, ManualLogSource log)
        {
            _config = config;
            _log = log;
        }

        private static HttpClient CreateHttpClient()
        {
            try
            {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch
            {
                // Mono may not expose every TLS enum; the runtime default is still used.
            }

            return new HttpClient
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
        }

        internal async Task<string> AskAsync(string playerName, string question, CancellationToken cancellationToken)
        {
            string baseUrl = (_config.BaseUrl.Value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException("Api.BaseUrl is empty. Edit the Odin Chat config file.");

            string apiKey = _config.ApiKey.Value ?? string.Empty;
            if (string.IsNullOrWhiteSpace(apiKey) || apiKey == OdinConfig.PlaceholderApiKey)
                throw new InvalidOperationException("Api.ApiKey is not configured. Edit the Odin Chat config file.");

            string model = (_config.Model.Value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(model))
                throw new InvalidOperationException("Api.Model is empty. Edit the Odin Chat config file.");

            string systemPrompt = _config.SystemPrompt.Value;
            if (string.IsNullOrWhiteSpace(systemPrompt))
                systemPrompt = OdinConfig.DefaultSystemPrompt;

            string url = baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
                ? baseUrl
                : baseUrl.TrimEnd('/') + "/chat/completions";

            var payload = new JObject
            {
                ["model"] = model,
                ["messages"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "system",
                        ["content"] = systemPrompt
                    },
                    new JObject
                    {
                        ["role"] = "user",
                        ["content"] = $"Player {playerName} asks: {question}"
                    }
                }
            };

            float temperature = _config.Temperature.Value;
            if (temperature >= 0f)
                payload["temperature"] = Math.Min(2f, temperature);

            int maxTokens = _config.MaxTokens.Value;
            if (maxTokens > 0)
                payload["max_tokens"] = Math.Max(1, Math.Min(8192, maxTokens));

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Headers.TryAddWithoutValidation("Accept", "application/json");
                request.Headers.TryAddWithoutValidation("X-Title", "Odin Chat");
                request.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");

                int timeoutSeconds = Math.Max(5, _config.TimeoutSeconds.Value);
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                    if (_config.VerboseLogging.Value)
                        _log.LogInfo($"POST {url} model={model} timeout={timeoutSeconds}s");

                    using (HttpResponseMessage response = await Http
                        .SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token)
                        .ConfigureAwait(false))
                    {
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                        if (!response.IsSuccessStatusCode)
                            throw new InvalidOperationException(
                                $"API returned HTTP {(int)response.StatusCode}: {Truncate(body, 600)}");

                        JObject json;
                        try
                        {
                            json = JObject.Parse(body);
                        }
                        catch (Exception ex)
                        {
                            throw new InvalidOperationException("API returned invalid JSON: " + Truncate(body, 300), ex);
                        }

                        if (json["error"] != null)
                            throw new InvalidOperationException("API error: " + Truncate(json["error"].ToString(Formatting.None), 600));

                        string content = ExtractContent(json);
                        if (string.IsNullOrWhiteSpace(content))
                            throw new InvalidOperationException("API returned an empty response.");

                        return content.Trim();
                    }
                }
            }
        }

        private static string ExtractContent(JObject json)
        {
            JToken choice = json["choices"]?.FirstOrDefault();
            if (choice == null)
                return null;

            JToken messageContent = choice["message"]?["content"];
            if (messageContent != null)
            {
                if (messageContent.Type == JTokenType.String)
                    return (string)messageContent;

                if (messageContent is JArray parts)
                    return string.Concat(parts
                        .OfType<JObject>()
                        .Select(part => (string)part["text"] ?? string.Empty));
            }

            // Some OpenAI-compatible servers use the legacy completions shape.
            return (string)choice["text"];
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            value = value.Replace("\r", " ").Replace("\n", " ").Trim();
            return value.Length <= maxLength ? value : value.Substring(0, maxLength) + "...";
        }
    }
}
