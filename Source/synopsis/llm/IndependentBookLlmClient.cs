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
using RimTalk_LiteratureExpansion.settings;
using Verse;

namespace RimTalk_LiteratureExpansion.synopsis.llm
{
    public static class IndependentBookLlmClient
    {
        private const int TimeoutMs = 120000;
        private const int MinOutputTokens = 512;
        private const int MaxOutputTokensCap = 2048;
        private const int OutputTokenOverhead = 120;
        private const string Player2LocalBaseUrl = "http://localhost:4315/v1";
        private const string Player2LocalHealthUrl = "http://localhost:4315/v1/health";
        private const string Player2LocalLoginUrl = "http://localhost:4315/v1/login/web/019a8368-b00b-72bc-b367-2825079dc6fb";
        private const int Player2LocalHealthTimeoutMs = 2000;
        private const int Player2LocalLoginTimeoutMs = 3000;
        private static string _player2LocalKey;
        private static DateTime _player2LocalKeyCheckedAt = DateTime.MinValue;
        private static readonly TimeSpan Player2LocalCheckInterval = TimeSpan.FromSeconds(30);
        private static int _requestId;
        private static readonly Regex OpenAIResponseRegex = new Regex(
            @"""content""\s*:\s*""((?:\\.|[^""])*)""",
            RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex GoogleResponseRegex = new Regex(
            @"""text""\s*:\s*""((?:\\.|[^""])*)""",
            RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex OpenAIFinishReasonRegex = new Regex(
            @"""finish_reason""\s*:\s*""([^""]*)""",
            RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex GoogleFinishReasonRegex = new Regex(
            @"""finishReason""\s*:\s*""([^""]*)""",
            RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex OpenAIUsageRegex = new Regex(
            @"""usage""\s*:\s*\{[^}]*?""prompt_tokens""\s*:\s*(\d+)[^}]*?""completion_tokens""\s*:\s*(\d+)[^}]*?""total_tokens""\s*:\s*(\d+)",
            RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly Regex GoogleUsageRegex = new Regex(
            @"""usageMetadata""\s*:\s*\{[^}]*?""promptTokenCount""\s*:\s*(\d+)[^}]*?""candidatesTokenCount""\s*:\s*(\d+)[^}]*?""totalTokenCount""\s*:\s*(\d+)",
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

            string apiKey = config.ApiKey;
            bool usePlayer2Local = false;

            if (config.Provider == AIProvider.Player2 && string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = await TryResolvePlayer2LocalKeyAsync(requestId);
                usePlayer2Local = !string.IsNullOrWhiteSpace(apiKey);
                if (!usePlayer2Local)
                {
                    Log.Warning($"[RimTalk LE] [Req {requestId}] Player2 API key is empty and no local app detected.");
                    return null;
                }
            }

            string endpoint = ResolveEndpoint(config, model);
            if (config.Provider == AIProvider.Player2 && usePlayer2Local)
                endpoint = $"{Player2LocalBaseUrl}/chat/completions";
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
                Log.Message($"[RimTalk LE] [Req {requestId}] API key: {SanitizeApiKey(apiKey)}");
                if (string.IsNullOrWhiteSpace(apiKey) && config.Provider != AIProvider.Google)
                    Log.Warning($"[RimTalk LE] [Req {requestId}] API key is empty.");

                int maxTokens = ResolveMaxOutputTokens();
                Log.Message($"[RimTalk LE] [Req {requestId}] Request max tokens: {maxTokens}");
                string json = BuildRequestJson(config.Provider, model, request.Context, request.Prompt, maxTokens);
                Log.Message($"[RimTalk LE] [Req {requestId}] Request payload length: {json?.Length ?? 0}");

                string responseText = await PostJsonAsync(requestId, endpoint, json, apiKey, config.Provider == AIProvider.Google);
                if (string.IsNullOrWhiteSpace(responseText))
                {
                    Log.Warning($"[RimTalk LE] [Req {requestId}] Empty response from independent book request.");
                    return null;
                }

                Log.Message($"[RimTalk LE] [Req {requestId}] Response length: {responseText.Length}");
                LogResponseMeta(config.Provider, responseText, requestId);
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

        private static string BuildRequestJson(AIProvider provider, string model, string context, string prompt, int maxTokens)
        {
            if (provider == AIProvider.Google)
            {
                var combined = string.IsNullOrWhiteSpace(context)
                    ? prompt ?? string.Empty
                    : $"{context}\n{prompt}";

                var request = new GeminiRequest
                {
                    Contents = new[] { new GeminiContent { Parts = new[] { new GeminiPart { Text = combined } } } },
                    GenerationConfig = new GeminiGenerationConfig
                    {
                        Temperature = 0.7f,
                        MaxOutputTokens = maxTokens
                    }
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
                MaxTokens = maxTokens
            };

            if (provider == AIProvider.Custom)
            {
                openAiRequest.MaxOutputTokens = maxTokens;
                openAiRequest.MaxCompletionTokens = maxTokens;
            }

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

        private static async Task<string> TryResolvePlayer2LocalKeyAsync(int requestId)
        {
            if (!string.IsNullOrWhiteSpace(_player2LocalKey))
                return _player2LocalKey;

            var now = DateTime.UtcNow;
            if (now - _player2LocalKeyCheckedAt < Player2LocalCheckInterval)
                return null;

            _player2LocalKeyCheckedAt = now;

            if (!await IsPlayer2LocalHealthyAsync(requestId))
                return null;

            var localKey = await RequestPlayer2LocalKeyAsync(requestId);
            if (!string.IsNullOrWhiteSpace(localKey))
            {
                _player2LocalKey = localKey;
                Log.Message($"[RimTalk LE] [Req {requestId}] Player2 local app authenticated.");
            }

            return _player2LocalKey;
        }

        private static async Task<bool> IsPlayer2LocalHealthyAsync(int requestId)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(Player2LocalHealthUrl);
                request.Method = "GET";
                request.Timeout = Player2LocalHealthTimeoutMs;

                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                    return response.StatusCode == HttpStatusCode.OK;
            }
            catch (Exception ex)
            {
                Log.Message($"[RimTalk LE] [Req {requestId}] Player2 local health check failed: {ex.Message}");
                return false;
            }
        }

        private static async Task<string> RequestPlayer2LocalKeyAsync(int requestId)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(Player2LocalLoginUrl);
                request.Method = "POST";
                request.ContentType = "application/json";
                request.Timeout = Player2LocalLoginTimeoutMs;

                byte[] bodyRaw = Encoding.UTF8.GetBytes("{}");
                request.ContentLength = bodyRaw.Length;

                using (var stream = await request.GetRequestStreamAsync())
                {
                    await stream.WriteAsync(bodyRaw, 0, bodyRaw.Length);
                }

                using (var response = (HttpWebResponse)await request.GetResponseAsync())
                using (var reader = new StreamReader(response.GetResponseStream()))
                {
                    string text = await reader.ReadToEndAsync();
                    var auth = JsonUtil.DeserializeFromJson<Player2LocalAuthResponse>(text);
                    return auth?.ApiKey ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                Log.Message($"[RimTalk LE] [Req {requestId}] Player2 local login failed: {ex.Message}");
                return null;
            }
        }

        [DataContract]
        private sealed class OpenAIRequest
        {
            [DataMember(Name = "model")] public string Model;
            [DataMember(Name = "messages")] public OpenAIMessage[] Messages;
            [DataMember(Name = "temperature", EmitDefaultValue = false)] public float Temperature;
            [DataMember(Name = "max_tokens", EmitDefaultValue = false)] public int? MaxTokens;
            [DataMember(Name = "max_output_tokens", EmitDefaultValue = false)] public int? MaxOutputTokens;
            [DataMember(Name = "max_completion_tokens", EmitDefaultValue = false)] public int? MaxCompletionTokens;
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

        [DataContract]
        private sealed class Player2LocalAuthResponse
        {
            [DataMember(Name = "p2Key")] public string ApiKey;
        }


        private static int ResolveMaxOutputTokens()
        {
            var settings = LiteratureMod.Settings;
            int target = settings?.synopsisTokenTarget ?? LiteratureSettingsDef.DefaultSynopsisTokenTarget;
            int maxTokens = target + LiteratureSettingsDef.StoryTokenBonus + OutputTokenOverhead;
            if (maxTokens < MinOutputTokens) maxTokens = MinOutputTokens;
            if (maxTokens > MaxOutputTokensCap) maxTokens = MaxOutputTokensCap;
            return maxTokens;
        }

        private static void LogResponseMeta(AIProvider provider, string responseText, int requestId)
        {
            if (string.IsNullOrWhiteSpace(responseText)) return;

            string finishReason = null;
            if (provider == AIProvider.Google)
            {
                var match = GoogleFinishReasonRegex.Match(responseText);
                if (match.Success) finishReason = match.Groups[1].Value;
            }
            else
            {
                var match = OpenAIFinishReasonRegex.Match(responseText);
                if (match.Success) finishReason = match.Groups[1].Value;
            }

            if (!string.IsNullOrWhiteSpace(finishReason))
                Log.Message($"[RimTalk LE] [Req {requestId}] finish_reason: {finishReason}");

            if (provider == AIProvider.Google)
            {
                if (TryParseUsage(GoogleUsageRegex, responseText, out var prompt, out var completion, out var total))
                    Log.Message($"[RimTalk LE] [Req {requestId}] token usage: prompt={prompt}, completion={completion}, total={total}");
            }
            else
            {
                if (TryParseUsage(OpenAIUsageRegex, responseText, out var prompt, out var completion, out var total))
                    Log.Message($"[RimTalk LE] [Req {requestId}] token usage: prompt={prompt}, completion={completion}, total={total}");
            }
        }

        private static bool TryParseUsage(Regex regex, string text, out int prompt, out int completion, out int total)
        {
            prompt = 0;
            completion = 0;
            total = 0;

            if (regex == null || string.IsNullOrWhiteSpace(text)) return false;

            var match = regex.Match(text);
            if (!match.Success || match.Groups.Count < 4) return false;

            return int.TryParse(match.Groups[1].Value, out prompt) &&
                   int.TryParse(match.Groups[2].Value, out completion) &&
                   int.TryParse(match.Groups[3].Value, out total);
        }
    }
}
