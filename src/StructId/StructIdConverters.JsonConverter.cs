﻿// <auto-generated/

using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StructId;

public static partial class StructIdConverters
{
    public class SystemTextJsonConverter<TSelf, TValue> : JsonConverter<TSelf>
        where TSelf : IStructId<TValue>, INewable<TSelf, TValue>
        where TValue : struct, IParsable<TValue>
    {
        public override TSelf Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
                return TSelf.New(TValue.Parse(reader.GetString() ?? throw new FormatException("Unsupported null value for struct id."), CultureInfo.InvariantCulture));

            return TSelf.New(JsonSerializer.Deserialize<TValue>(ref reader, options));
        }

        public override void Write(Utf8JsonWriter writer, TSelf value, JsonSerializerOptions options)
        {
            switch (value.Value)
            {
                case Guid guid:
                    writer.WriteStringValue(guid);
                    break;
                case TValue inner:
                    JsonSerializer.Serialize(writer, inner, options);
                    break;
                default:
                    throw new InvalidOperationException("Unsupported value type.");
            }
        }

        public override TSelf ReadAsPropertyName(ref global::System.Text.Json.Utf8JsonReader reader, global::System.Type typeToConvert, global::System.Text.Json.JsonSerializerOptions options)
            => TSelf.New(TValue.Parse(reader.GetString() ?? throw new FormatException("Unsupported null value for struct id."), CultureInfo.InvariantCulture));

        public override void WriteAsPropertyName(global::System.Text.Json.Utf8JsonWriter writer, TSelf value, global::System.Text.Json.JsonSerializerOptions options)
            => writer.WritePropertyName(value.Value.ToString());
    }

    public class SystemTextJsonConverter<TSelf> : JsonConverter<TSelf>
        where TSelf : IStructId, IParsable<TSelf>, INewable<TSelf>
    {
        public override TSelf Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => TSelf.New(reader.GetString() ?? throw new FormatException("Unsupported null value for struct id."));

        public override void Write(Utf8JsonWriter writer, TSelf value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.Value);

        public override TSelf ReadAsPropertyName(ref global::System.Text.Json.Utf8JsonReader reader, global::System.Type typeToConvert, global::System.Text.Json.JsonSerializerOptions options)
           => TSelf.New(reader.GetString() ?? throw new FormatException("Unsupported null value for struct id."));

        public override void WriteAsPropertyName(global::System.Text.Json.Utf8JsonWriter writer, TSelf value, global::System.Text.Json.JsonSerializerOptions options)
            => writer.WritePropertyName(value.Value);
    }
}