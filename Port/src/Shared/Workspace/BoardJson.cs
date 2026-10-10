#nullable enable
using System;
using System.Collections;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Prisma.Workspace
{
    /// <summary>
    /// How board.json is read and written. Reading is tolerant: unknown fields go to the
    /// <c>Extra</c> bags, and an unknown kind or state is kept as text. Writing leaves out empty
    /// lists and nulls, so a card that uses no scheduling fields looks as it did before.
    /// </summary>
    public static class BoardJson
    {
        public static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters = { new JsonStringEnumConverter() },
            TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { SkipEmptyCollections } },
        };

        static void SkipEmptyCollections(JsonTypeInfo info)
        {
            foreach (var p in info.Properties)
                if (typeof(ICollection).IsAssignableFrom(p.PropertyType) && p.Get is { } get)
                    p.ShouldSerialize = (obj, _) => get(obj) is not ICollection { Count: 0 };
        }

        public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

        /// <summary>Parses a board; false (with the reason) when the text is not one.</summary>
        public static bool TryParse(string text, out PrismaBoard? board, out string? error)
        {
            try
            {
                board = JsonSerializer.Deserialize<PrismaBoard>(text, Options);
                if (board == null) { error = "the file holds no board (null)"; return false; }
                board.Items = board.Items?.Where(i => i != null).ToList() ?? new();
                BoardMerge.Backfill(board);
                error = null;
                return true;
            }
            catch (Exception e) when (e is JsonException or NotSupportedException or InvalidOperationException or ArgumentException or FormatException)
            {
                board = null;
                error = e.Message;
                return false;
            }
        }

        /// <summary>The enum value a stored name stands for, or <paramref name="fallback"/> when this build does not know it.</summary>
        public static T ParseEnum<T>(string? name, T fallback) where T : struct, Enum =>
            name != null && Enum.TryParse<T>(name.Trim(), true, out var v) && Enum.IsDefined(v) ? v : fallback;

        public static bool IsKnown<T>(string? name) where T : struct, Enum =>
            name != null && Enum.TryParse<T>(name.Trim(), true, out var v) && Enum.IsDefined(v);
    }

    /// <summary>Reads a string, number or boolean as text, so an enum written any way (or one this build lacks) survives.</summary>
    public sealed class LenientStringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString() ?? "",
            JsonTokenType.Number => reader.TryGetInt64(out var n) ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : reader.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.True => "true",
            JsonTokenType.False => "false",
            JsonTokenType.Null => "",
            _ => throw new JsonException($"expected a string, found {reader.TokenType}"),
        };

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
    }
}
