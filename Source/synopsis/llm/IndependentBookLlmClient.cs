using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RimTalk;
using RimTalk.Data;
using RimTalk.Util;
using Verse;

namespace RimTalk_LiteratureExpansion.synopsis.llm
{
    public static class IndependentBookLlmClient
    {
        private const int TimeoutMs = 120000;
        private static int _requestId;
        private static readonly Regex OpenAIResponseRegex = new Regex(
            @"""content""\s*:\s*""((?:\\.|[^""])*)""",
            RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex GoogleResponseRegex = new Regex(
            @"""text""\s*:\s*""((?:\\.|[^""])*)""",
            RegexOptions.Compiled | RegexOptions.Singleline);

        public static async Task<T> QueryJsonAsync<T>(TalkRequest request) where T : class, IJsonData
        {
            if (request == null) return null;

            int requestId = Interlocked.Increment(ref _requestId);

            if (!TryGetActiveConfig(out var config))
            {
                Log.Warning($"[RimTalk LE] [Req {requestId}] No active RimTalk API config for independent book request.");
                return null;
            }

            string model = ResolveModel(config);
            if (string.IsNullOrWhiteSpace(model))
            {
                Log.Warning($"[RimTalk LE] [Req {requestId}] Missing model for independent book request.");
                return null;
            }

            string endpoint = ResolveEndpoint(config, model);
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                Log.Warning($"[RimTalk LE] [Req {requestId}] Missing endpoint for independent book request.");
                return null;
            }

            try
            {
                Log.Message($"[RimTalk LE] [Req {requestId}] Independent LLM request start.");
                Log.Message($"[RimTalk LE] [Req {requestId}] Provider: {config.Provider}, Model: {model}");
                Log.Message($"[RimTalk LE] [Req {requestId}] Endpoint: {SanitizeEndpoint(config.Provider, endpoint)}");
                Log.Message($"[RimTalk LE] [Req {requestId}] API key: {SanitizeApiKey(config.ApiKey)}");
                if (string.IsNullOrWhiteSpace(config.ApiKey) && config.Provider != AIProvider.Google)
                    Log.Warning($"[RimTalk LE] [Req {requestId}] API key is empty.");

                string json = BuildRequestJson(config.Provider, model, request.Context, request.Prompt);
                Log.Message($"[RimTalk LE] [Req {requestId}] Request payload length: {json?.Length ?? 0}");

                string responseText = await PostJsonAsync(requestId, endpoint, json, config.ApiKey, config.Provider == AIProvider.Google);
                if (string.IsNullOrWhiteSpace(responseText))
                {
                    Log.Warning($"[RimTalk LE] [Req {requestId}] Empty response from independent book request.");
                    return null;
                }

                Log.Message($"[RimTalk LE] [Req {requestId}] Response length: {responseText.Length}");
                string content = ExtractContent(config.Provider, responseText, requestId);
                if (string.IsNullOrWhiteSpace(content))
                {
                    Log.Warning($"[RimTalk LE] [Req {requestId}] Failed to parse response content from independent book request.");
                    return null;
                }

                var jsonPayload = ExtractJsonPayload(content);
                if (string.IsNullOrWhiteSpace(jsonPayload))
                {
                    var trimmed = content.Trim();
                    int startIndex = trimmed.IndexOf('{');
                    int endIndex = trimmed.LastIndexOf('}');
                    bool hasFence = trimmed.StartsWith("```", StringComparison.Ordinal);
                    Log.Warning($"[RimTalk LE] [Req {requestId}] JSON detect: fence={hasFence}, start={startIndex}, end={endIndex}, len={trimmed.Length}");
                    Log.Warning($"[RimTalk LE] [Req {requestId}] Response content was not JSON; abort deserialize.");
                    Log.Warning($"[RimTalk LE] [Req {requestId}] Content preview: {TrimPreview(content)}");
                    return null;
                }

            Log.Message($"[RimTalk LE] [Req {requestId}] Parsed JSON payload length: {jsonPayload.Length}");
            var result = JsonUtil.DeserializeFromJson<T>(jsonPayload);
            Log.Message($"[RimTalk LE] [Req {requestId}] JSON deserialization {(result == null ? "failed" : "succeeded")}.");
            return result;
        }
            catch (Exception ex)
            {
                Log.Warning($"[RimTalk LE] [Req {requestId}] Independent book request failed: {ex.GetType().Name} - {ex.Message}");
                return null;
            }
        }

        private static bool TryGetActiveConfig(out ApiConfig config)
        {
            config = null;
            var settings = Settings.Get();
            if (settings == null) return false;
            config = settings.GetActiveConfig();
            return config != null;
        }

        private static string ResolveModel(ApiConfig config)
        {
            if (config == null) return string.Empty;

            if ((config.Provider == AIProvider.Local || config.Provider == AIProvider.Custom) &&
                !string.IsNullOrWhiteSpace(config.CustomModelName))
                return config.CustomModelName;

            if (string.Equals(config.SelectedModel, "Custom", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(config.CustomModelName))
                return config.CustomModelName;

            if (!string.IsNullOrWhiteSpace(config.SelectedModel) &&
                !string.Equals(config.SelectedModel, Constant.ChooseModel, StringComparison.OrdinalIgnoreCase))
                return config.SelectedModel;

            return Constant.DefaultCloudModel;
        }

        private static string ResolveEndpoint(ApiConfig config, string model)
        {
            if (config == null) return string.Empty;

            switch (config.Provider)
            {
                case AIProvider.Google:
                    return $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={config.ApiKey}";
                case AIProvider.OpenAI:
                    return "https://api.openai.com/v1/chat/completions";
                case AIProvider.DeepSeek:
                    return "https://api.deepseek.com/v1/chat/completions";
                case AIProvider.Grok:
                    return "https://api.x.ai/v1/chat/completions";
                case AIProvider.GLM:
                    return "https://api.z.ai/api/paas/v4/chat/completions";
                case AIProvider.OpenRouter:
                    return "https://openrouter.ai/api/v1/chat/completions";
                case AIProvider.Player2:
                    return "https://api.player2.game/v1/chat/completions";
                case AIProvider.Local:
                case AIProvider.Custom:
                    return NormalizeEndpoint(config.BaseUrl);
                default:
                    return string.Empty;
            }
        }

        private static string NormalizeEndpoint(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) return string.Empty;

            var trimmed = baseUrl.Trim().TrimEnd('/');
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
                return trimmed;

            if (string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/")
                return trimmed + "/v1/chat/completions";

            return trimmed;
        }

        private static string BuildRequestJson(AIProvider provider, string model, string context, string prompt)
        {
            if (provider == AIProvider.Google)
            {
                var combined = string.IsNullOrWhiteSpace(context)
                    ? prompt ?? string.Empty
                    : $"{context}\n{prompt}";

                var request = new GeminiRequest
                {
                    Contents = new[] { new GeminiContent { Parts = new[] { new GeminiPart { Text = combined } } } },
                    GenerationConfig = new GeminiGenerationConfig { Temperature = 0.7f, MaxOutputTokens = 512 }
                };

                return JsonUtil.SerializeToJson(request);
            }

            var messages = new[]
            {
                new OpenAIMessage { Role = "system", Content = context ?? string.Empty },
                new OpenAIMessage { Role = "user", Content = prompt ?? string.Empty }
            };

            var openAiRequest = new OpenAIRequest
            {
                Model = model,
                Messages = messages,
                Temperature = 0.7f,
                MaxTokens = 512
            };

            return JsonUtil.SerializeToJson(openAiRequest);
        }

        private static async Task<string> PostJsonAsync(int requestId, string url, string json, string apiKey, bool isGoogle)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Timeout = TimeoutMs;

            if (!isGoogle && !string.IsNullOrWhiteSpace(apiKey))
                request.Headers["Authorization"] = $"Bearer {apiKey}";

            byte[] bodyRaw = Encoding.UTF8.GetBytes(json ?? string.Empty);
            request.ContentLength = bodyRaw.Length;

            var sw = Stopwatch.StartNew();

            try
            {
                using (var stream = await request.GetRequestStreamAsync())
                {
                    await stream.WriteAsync(bodyRaw, 0, bodyRaw.Length);
                }

                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                using (var streamReader = new StreamReader(response.GetResponseStream()))
                {
                    string text = await streamReader.ReadToEndAsync();
                    Log.Message($"[RimTalk LE] [Req {requestId}] Response status: {(int)response.StatusCode} in {sw.ElapsedMilliseconds} ms.");
                    return text;
                }
            }
            catch (WebException ex)
            {
                string detail = ex.Message;
                if (ex.Response is HttpWebResponse errorResponse)
                {
                    using (var reader = new StreamReader(errorResponse.GetResponseStream()))
                    {
                        string errorText = reader.ReadToEnd();
                        detail = errorText.Length > 300 ? errorText.Substring(0, 300) : errorText;
                        Log.Warning($"[RimTalk LE] [Req {requestId}] HTTP {(int)errorResponse.StatusCode}: {detail}");
                    }
                }
                else
                {
                    Log.Warning($"[RimTalk LE] [Req {requestId}] WebException: {detail}");
                }
                throw;
            }
        }

        private static string ExtractContent(AIProvider provider, string responseText, int requestId)
        {
            var regex = provider == AIProvider.Google ? GoogleResponseRegex : OpenAIResponseRegex;
            var matches = regex.Matches(responseText);
            if (matches.Count == 0)
            {
                Log.Warning($"[RimTalk LE] [Req {requestId}] No content fragments matched in response.");
                return null;
            }

            var sb = new StringBuilder();
            foreach (Match match in matches)
                sb.Append(match.Groups[1].Value);

            Log.Message($"[RimTalk LE] [Req {requestId}] Content fragments: {matches.Count}.");
            return Regex.Unescape(sb.ToString());
        }

        private static string ExtractJsonPayload(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) return null;

            var trimmed = content.Trim();
            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                int firstLine = trimmed.IndexOf('\n');
                int lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
                if (firstLine >= 0 && lastFence > firstLine)
                    trimmed = trimmed.Substring(firstLine + 1, lastFence - firstLine - 1).Trim();
            }

            int start = trimmed.IndexOf('{');
            int end = trimmed.LastIndexOf('}');
            if (start >= 0 && end > start)
                return trimmed.Substring(start, end - start + 1).Trim();

            if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
                return trimmed;

            return null;
        }

        private static string TrimPreview(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "(empty)";
            const int maxLen = 160;
            var trimmed = text.Trim();
            return trimmed.Length <= maxLen ? trimmed : trimmed.Substring(0, maxLen) + "...";
        }

        private static string SanitizeApiKey(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return "(empty)";
            if (apiKey.Length <= 8) return $"{apiKey.Substring(0, 2)}...";
            return $"{apiKey.Substring(0, 4)}...{apiKey.Substring(apiKey.Length - 4)}";
        }

        private static string SanitizeEndpoint(AIProvider provider, string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint)) return "(empty)";
            if (provider != AIProvider.Google) return endpoint;
            int keyIndex = endpoint.IndexOf("key=", StringComparison.OrdinalIgnoreCase);
            if (keyIndex < 0) return endpoint;
            return endpoint.Substring(0, keyIndex + 4) + "***";
        }

        [DataContract]
        private sealed class OpenAIRequest
        {
            [DataMember(Name = "model")] public string Model;
            [DataMember(Name = "messages")] public OpenAIMessage[] Messages;
            [DataMember(Name = "temperature", EmitDefaultValue = false)] public float Temperature;
            [DataMember(Name = "max_tokens", EmitDefaultValue = false)] public int MaxTokens;
        }

        [DataContract]
        private sealed class OpenAIMessage
        {
            [DataMember(Name = "role")] public string Role;
            [DataMember(Name = "content")] public string Content;
        }

        [DataContract]
        private sealed class GeminiRequest
        {
            [DataMember(Name = "contents")] public GeminiContent[] Contents;
            [DataMember(Name = "generationConfig")] public GeminiGenerationConfig GenerationConfig;
        }

        [DataContract]
        private sealed class GeminiContent
        {
            [DataMember(Name = "parts")] public GeminiPart[] Parts;
        }

        [DataContract]
        private sealed class GeminiPart
        {
            [DataMember(Name = "text")] public string Text;
        }

        [DataContract]
        private sealed class GeminiGenerationConfig
        {
            [DataMember(Name = "temperature")] public float Temperature;
            [DataMember(Name = "maxOutputTokens")] public int MaxOutputTokens;
        }
    }
}
