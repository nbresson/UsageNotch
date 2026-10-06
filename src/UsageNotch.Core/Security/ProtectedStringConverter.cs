using System.Text.Json;
using System.Text.Json.Serialization;

namespace UsageNotch.Core.Security;

/// <summary>
/// Convertisseur JSON transparent pour les champs contenant des données sensibles.
/// Déchiffre automatiquement lors de la désérialisation et chiffre lors de la sérialisation.
/// </summary>
public sealed class ProtectedStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var raw = reader.GetString();
        return SecretProtector.Unprotect(raw);
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        var cipher = SecretProtector.Protect(value);
        writer.WriteStringValue(cipher);
    }
}
