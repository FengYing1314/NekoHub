using System.Text.Json;
using System.Text.Json.Serialization;
using NekoHub.Application.Common.Models;

namespace NekoHub.Api.Serialization;

public sealed class OptionalValueJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType
               && typeToConvert.GetGenericTypeDefinition() == typeof(OptionalValue<>);
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var valueType = typeToConvert.GetGenericArguments()[0];
        var converterType = typeof(OptionalValueJsonConverter<>).MakeGenericType(valueType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }

    private sealed class OptionalValueJsonConverter<TValue> : JsonConverter<OptionalValue<TValue>>
    {
        public override OptionalValue<TValue> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            var value = JsonSerializer.Deserialize<TValue>(ref reader, options);
            return OptionalValue<TValue>.From(value);
        }

        public override void Write(Utf8JsonWriter writer, OptionalValue<TValue> value, JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(writer, value.Value, options);
        }
    }
}
