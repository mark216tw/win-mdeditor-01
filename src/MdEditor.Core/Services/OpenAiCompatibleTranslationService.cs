using System.Net;
using System.Text;
using System.Text.Json;
using VibeCode.MdEditor.Core.Helpers;
using VibeCode.MdEditor.Core.Models;

namespace VibeCode.MdEditor.Core.Services;

public sealed class OpenAiCompatibleTranslationService : ITranslationService
{
    private const int MaxRetries = 2;

    private readonly HttpClient _httpClient;
    private readonly ITranslationCredentialStore _credentialStore;

    public OpenAiCompatibleTranslationService(
        HttpClient httpClient,
        ITranslationCredentialStore credentialStore)
    {
        _httpClient = httpClient;
        _credentialStore = credentialStore;
    }

    public async Task<bool> IsConfiguredAsync(
        TranslationSettings settings,
        string? apiKeyOverride = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(settings.Model) || !TryCreateRequestUri(settings, out _))
        {
            return false;
        }

        var apiKey = await ResolveApiKeyAsync(apiKeyOverride, cancellationToken).ConfigureAwait(false);
        return !string.IsNullOrWhiteSpace(apiKey);
    }

    public async Task<IReadOnlyList<string>> TranslateAsync(
        IReadOnlyList<string> segments,
        TranslationSettings settings,
        string? apiKeyOverride = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(segments);
        ArgumentNullException.ThrowIfNull(settings);

        if (segments.Count == 0)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(settings.Model))
        {
            throw new TranslationException("尚未設定翻譯模型。");
        }

        if (!TryCreateRequestUri(settings, out var requestUri))
        {
            throw new TranslationException("翻譯服務端點無效，請使用 HTTPS 或本機 HTTP 位址。");
        }

        var apiKey = await ResolveApiKeyAsync(apiKeyOverride, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new TranslationException("尚未設定翻譯 API Key。");
        }

        var payload = BuildRequestPayload(segments, settings);
        var responseBody = await SendWithRetryAsync(
                requestUri,
                apiKey,
                payload,
                settings.RequestTimeoutSeconds,
                cancellationToken)
            .ConfigureAwait(false);

        return TranslationResponseParser.Parse(responseBody, segments.Count);
    }

    private async Task<string?> ResolveApiKeyAsync(string? apiKeyOverride, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(apiKeyOverride))
        {
            return apiKeyOverride.Trim();
        }

        var stored = await _credentialStore.GetApiKeyAsync(cancellationToken).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(stored) ? null : stored.Trim();
    }

    private async Task<string> SendWithRetryAsync(
        Uri requestUri,
        string apiKey,
        string payload,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, requestUri);
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);
                request.Content = new StringContent(payload, Encoding.UTF8, "application/json");

                using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

                using var response = await _httpClient.SendAsync(
                        request,
                        HttpCompletionOption.ResponseHeadersRead,
                        timeoutSource.Token)
                    .ConfigureAwait(false);

                var body = await response.Content.ReadAsStringAsync(timeoutSource.Token).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    return body;
                }

                if (attempt < MaxRetries && IsRetryable(response.StatusCode))
                {
                    await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                throw new TranslationException(BuildErrorMessage(response.StatusCode, body));
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TranslationException("翻譯服務逾時，請稍後再試或縮小翻譯範圍。");
            }
            catch (HttpRequestException) when (attempt < MaxRetries)
            {
                await DelayBeforeRetryAsync(attempt, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                throw new TranslationException("無法連線至翻譯服務：" + ex.Message, ex);
            }
        }
    }

    private static string BuildRequestPayload(IReadOnlyList<string> segments, TranslationSettings settings)
    {
        var targetName = TranslationLanguageCatalog.GetDisplayName(settings.TargetLanguage);
        var targetCode = TranslationLanguageCatalog.ResolveTarget(settings.TargetLanguage);
        var sourceDescription = settings.SourceLanguage == TranslationSettings.AutoDetectSourceLanguage
            ? "自動偵測"
            : TranslationLanguageCatalog.GetDisplayName(settings.SourceLanguage);

        var systemPrompt =
            $"你是一位專業的 Markdown 翻譯人員。請將每一段文字翻譯成 {targetName}（{targetCode}），原文語言為{sourceDescription}。\n" +
            "規則：\n" +
            "1. 只翻譯自然語言內容；不要翻譯程式碼、網址、檔案路徑或 Markdown 標記本身。\n" +
            "2. 必須原樣保留所有形如 ⟦0⟧ 的佔位符，數量、編號與順序都不可改變，也不可翻譯或省略。\n" +
            "3. 保留段落中既有的 Markdown 標記與標點結構，不要自行新增標題或項目符號。\n" +
            "4. 不要加入任何解釋、註解或額外文字。\n" +
            $"5. 只回傳 JSON 物件：{{\"translations\":[\"...\"]}}，陣列長度必須是 {segments.Count}，順序與輸入完全相同。";

        var userPayload = JsonSerializer.Serialize(new
        {
            segments,
        });

        return JsonSerializer.Serialize(new
        {
            model = settings.Model.Trim(),
            temperature = settings.Temperature,
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPayload },
            },
        });
    }

    private static bool TryCreateRequestUri(TranslationSettings settings, out Uri requestUri)
    {
        requestUri = null!;

        if (string.IsNullOrWhiteSpace(settings.Endpoint))
        {
            return false;
        }

        var endpoint = settings.Endpoint.Trim().TrimEnd('/');
        if (!endpoint.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            endpoint += "/chat/completions";
        }

        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme == Uri.UriSchemeHttps ||
            (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback))
        {
            requestUri = uri;
            return true;
        }

        return false;
    }

    private static bool IsRetryable(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.TooManyRequests ||
               (int)statusCode >= 500;
    }

    private static Task DelayBeforeRetryAsync(int attempt, CancellationToken cancellationToken)
    {
        var seconds = Math.Pow(2, Math.Min(attempt, 3));
        return Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
    }

    private static string BuildErrorMessage(HttpStatusCode statusCode, string body)
    {
        var message = body.Trim();
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind == JsonValueKind.Object &&
                document.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("message", out var errorMessage) &&
                errorMessage.ValueKind == JsonValueKind.String)
            {
                message = errorMessage.GetString() ?? message;
            }
        }
        catch (JsonException)
        {
        }

        if (message.Length > 400)
        {
            message = message[..400] + "…";
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = "沒有回應內容。";
        }

        return $"翻譯服務回應錯誤（HTTP {(int)statusCode}）：{message}";
    }
}
