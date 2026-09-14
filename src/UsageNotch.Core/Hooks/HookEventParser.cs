using System.Text;
using System.Text.Json;
using UsageNotch.Core.Sessions;

namespace UsageNotch.Core.Hooks;

/// <summary>
/// Corps JSON d'un hook Claude Code → HookEvent. Aucun champ manquant n'est une erreur. La lecture se fait vers
/// l'avant avec un <see cref="Utf8JsonReader"/> (isFinalBlock: false) : un corps tronqué — par exemple au-delà de
/// HookListener.MaxBodyBytes — conserve les champs déjà lus avant la coupure plutôt que de tout perdre.
/// </summary>
public static class HookEventParser
{
    public static HookEvent Parse(string kind, int parentPid, string body)
    {
        var sessionId = "";
        var cwd = "";
        var prompt = "";
        var message = "";
        var toolName = "";
        var model = "";
        var toolCommand = "";

        try
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            var reader = new Utf8JsonReader(bytes, isFinalBlock: false, state: default);

            if (reader.Read() && reader.TokenType == JsonTokenType.StartObject)
            {
                while (true)
                {
                    if (!reader.Read()) break; // corps tronqué : on garde ce qui a déjà été lu
                    if (reader.TokenType == JsonTokenType.EndObject) break;
                    if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 1) continue;

                    var name = reader.GetString() ?? "";
                    var ok = name switch
                    {
                        "session_id" => TryReadString(ref reader, out sessionId),
                        "cwd" => TryReadString(ref reader, out cwd),
                        "prompt" => TryReadString(ref reader, out prompt),
                        "message" => TryReadString(ref reader, out message),
                        "tool_name" => TryReadString(ref reader, out toolName),
                        "model" => TryReadString(ref reader, out model),
                        "tool_input" => TryReadToolInput(ref reader, out toolCommand),
                        _ => reader.TrySkip(),
                    };
                    if (!ok) break;
                }
            }
        }
        catch (JsonException)
        {
            // Corps invalide (pas seulement tronqué) : on garde ce qui a déjà été capturé.
        }

        return new HookEvent(
            kind,
            sessionId.Length == 0 ? "unknown" : sessionId,
            parentPid,
            cwd,
            prompt,
            message,
            toolName,
            toolCommand,
            model);
    }

    private static bool TryReadString(ref Utf8JsonReader reader, out string value)
    {
        value = "";
        if (!reader.Read()) return false;
        if (reader.TokenType == JsonTokenType.String)
        {
            value = reader.GetString() ?? "";
            return true;
        }
        if (reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray)
        {
            return reader.TrySkip();
        }
        return true; // valeur scalaire (nombre/bool/null) déjà consommée par Read()
    }

    private static bool TryReadToolInput(ref Utf8JsonReader reader, out string command)
    {
        command = "";
        if (!reader.Read()) return false;

        if (reader.TokenType != JsonTokenType.StartObject)
        {
            return reader.TokenType == JsonTokenType.StartArray ? reader.TrySkip() : true;
        }

        while (true)
        {
            if (!reader.Read()) return false; // tronqué avant la fin de tool_input
            if (reader.TokenType == JsonTokenType.EndObject) return true;
            if (reader.TokenType != JsonTokenType.PropertyName || reader.CurrentDepth != 2) continue;

            var name = reader.GetString() ?? "";
            if (name == "command")
            {
                if (!TryReadString(ref reader, out command)) return false;
            }
            else if (!reader.TrySkip())
            {
                return false;
            }
        }
    }
}
