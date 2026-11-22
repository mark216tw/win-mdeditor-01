using System.Text.Json;

namespace VibeCode.MdEditor.Core.Services;

public static class TranslationResponseParser
{
    private static readonly string[] TranslationPropertyNames =
    [
        "translations",
        "segments",
        "items",
        "result",
    ];

    public static IReadOnlyList<string> Parse(string responseBody, int expectedCount)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            throw new TranslationException("翻譯服務回應為空。");
        }

        JsonDocument envelope;
        try
        {
            envelope = JsonDocument.Parse(responseBody);
        }
        catch (JsonException ex)
        {
            throw new TranslationException("翻譯服務回應不是有效的 JSON。", ex);
        }

        using (envelope)
        {
            if (TryGetMessageContent(envelope.RootElement, out var content))
            {
                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new TranslationException("翻譯服務回應沒有內容。");
                }

                var payload = ExtractJsonPayload(content);
                JsonDocument contentDocument;
                try
                {
                    contentDocument = JsonDocument.Parse(payload);
                }
                catch (JsonException ex)
                {
                    throw new TranslationException(
                        "翻譯服務未回傳預期的 JSON 格式，請確認模型是否支援結構化輸出。",
                        ex);
                }

                using (contentDocument)
                {
                    return ReadTranslations(contentDocument.RootElement, expectedCount);
                }
            }

            return ReadTranslations(envelope.RootElement, expectedCount);
        }
    }

    private static bool TryGetMessageContent(JsonElement root, out string content)
    {
        content = string.Empty;

        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            return false;
        }

        var first = choices[0];
        if (first.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        if (first.TryGetProperty("message", out var message) &&
            message.ValueKind == JsonValueKind.Object &&
            message.TryGetProperty("content", out var messageContent))
        {
            if (messageContent.ValueKind == JsonValueKind.String)
            {
                content = messageContent.GetString() ?? string.Empty;
                return true;
            }

            if (messageContent.ValueKind == JsonValueKind.Array)
            {
                content = string.Concat(
                    messageContent.EnumerateArray()
                        .Where(part => part.ValueKind == JsonValueKind.Object &&
                                       part.TryGetProperty("text", out _))
                        .Select(part => part.GetProperty("text").GetString()));
                return true;
            }
        }

        if (first.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
        {
            content = text.GetString() ?? string.Empty;
            return true;
        }

        return false;
    }

    private static IReadOnlyList<string> ReadTranslations(JsonElement root, int expectedCount)
    {
        JsonElement array;
        if (root.ValueKind == JsonValueKind.Array)
        {
            array = root;
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            array = default;
            foreach (var propertyName in TranslationPropertyNames)
            {
                if (root.TryGetProperty(propertyName, out var property) &&
                    property.ValueKind == JsonValueKind.Array)
                {
                    array = property;
                    break;
                }
            }

            if (array.ValueKind != JsonValueKind.Array &&
                expectedCount == 1 &&
                root.TryGetProperty("translation", out var single) &&
                single.ValueKind == JsonValueKind.String)
            {
                return [single.GetString() ?? string.Empty];
            }
        }
        else
        {
            throw new TranslationException("翻譯服務回應格式不正確。");
        }

        if (array.ValueKind != JsonValueKind.Array)
        {
            throw new TranslationException("翻譯服務回應缺少 translations 陣列。");
        }

        var results = new List<string>(array.GetArrayLength());
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new TranslationException("翻譯結果陣列包含非文字內容。");
            }

            results.Add(item.GetString() ?? string.Empty);
        }

        if (results.Count != expectedCount)
        {
            throw new TranslationException(
                $"翻譯結果數量不符：預期 {expectedCount} 段，實際 {results.Count} 段。");
        }

        return results;
    }

    private static string ExtractJsonPayload(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            var firstLineEnd = trimmed.IndexOf('\n');
            if (firstLineEnd >= 0)
            {
                trimmed = trimmed[(firstLineEnd + 1)..];
            }

            if (trimmed.EndsWith("```", StringComparison.Ordinal))
            {
                trimmed = trimmed[..^3];
            }

            trimmed = trimmed.Trim();
        }

        var firstObject = trimmed.IndexOf('{');
        var firstArray = trimmed.IndexOf('[');
        var firstBrace = firstObject < 0
            ? firstArray
            : firstArray < 0
                ? firstObject
                : Math.Min(firstObject, firstArray);
        if (firstBrace > 0)
        {
            trimmed = trimmed[firstBrace..];
        }

        var lastObject = trimmed.LastIndexOf('}');
        var lastArray = trimmed.LastIndexOf(']');
        var lastBrace = Math.Max(lastObject, lastArray);
        if (lastBrace >= 0 && lastBrace < trimmed.Length - 1)
        {
            trimmed = trimmed[..(lastBrace + 1)];
        }

        return trimmed.Trim();
    }
}
