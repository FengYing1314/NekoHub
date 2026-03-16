using System.Text.Json;
using NekoHub.Application.Common.Exceptions;

namespace NekoHub.Application.Storage.Validation;

internal static class StorageProviderProfileJson
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static T DeserializeRequiredObject<T>(string json, string code, string message)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                throw new ValidationException(code, message);
            }

            var model = JsonSerializer.Deserialize<T>(document.RootElement.GetRawText(), SerializerOptions);
            return model ?? throw new ValidationException(code, message);
        }
        catch (JsonException)
        {
            throw new ValidationException(code, message);
        }
    }

    public static string Serialize<T>(T model)
    {
        return JsonSerializer.Serialize(model, SerializerOptions);
    }

    public static bool IsNullOrEmptyObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                   && !document.RootElement.EnumerateObject().Any();
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string NormalizeJsonElement(JsonElement? element, string code, string message, bool allowNull = false)
    {
        if (!element.HasValue || element.Value.ValueKind == JsonValueKind.Undefined)
        {
            if (allowNull)
            {
                return string.Empty;
            }

            throw new ValidationException(code, message);
        }

        if (element.Value.ValueKind == JsonValueKind.Null)
        {
            if (allowNull)
            {
                return string.Empty;
            }

            throw new ValidationException(code, message);
        }

        if (element.Value.ValueKind is not JsonValueKind.Object)
        {
            throw new ValidationException(code, message);
        }

        return element.Value.GetRawText();
    }

    public static bool IsAbsoluteHttpUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}
