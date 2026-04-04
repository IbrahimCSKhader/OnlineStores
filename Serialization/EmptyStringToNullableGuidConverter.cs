using System.Text.Json;
using System.Text.Json.Serialization;

namespace onlineStore.Serialization
{
    public sealed class EmptyStringToNullableGuidConverter : JsonConverter<Guid?>
    {
        public override Guid? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            if (reader.TokenType == JsonTokenType.String)
            {
                var value = reader.GetString();

                if (string.IsNullOrWhiteSpace(value))
                    return null;

                if (Guid.TryParse(value, out var guid))
                    return guid;
            }

            throw new JsonException("The value is not a valid GUID.");
        }

        public override void Write(
            Utf8JsonWriter writer,
            Guid? value,
            JsonSerializerOptions options)
        {
            if (value.HasValue)
            {
                writer.WriteStringValue(value.Value);
                return;
            }

            writer.WriteNullValue();
        }
    }
}
