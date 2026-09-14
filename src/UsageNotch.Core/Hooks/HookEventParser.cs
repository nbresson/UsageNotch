using System.Text.Json;
using UsageNotch.Core.Sessions;

namespace UsageNotch.Core.Hooks;

/// <summary>Corps JSON d'un hook Claude Code → HookEvent. Aucun champ manquant n'est une erreur.</summary>
public static class HookEventParser
{
    public static HookEvent Parse(string kind, int parentPid, string body)
    {
        JsonDocument? doc = null;
        try
        {
            if (body.Length > 0) doc = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            doc = null;
        }

        using (doc)
        {
            var root = doc is not null && doc.RootElement.ValueKind == JsonValueKind.Object ? doc.RootElement : default;
            var sessionId = Str(root, "session_id");
            var toolCommand = root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("tool_input", out var input)
                && input.ValueKind == JsonValueKind.Object
                ? Str(input, "command")
                : "";

            return new HookEvent(
                kind,
                sessionId.Length == 0 ? "unknown" : sessionId,
                parentPid,
                Str(root, "cwd"),
                Str(root, "prompt"),
                Str(root, "message"),
                Str(root, "tool_name"),
                toolCommand,
                Str(root, "model"));
        }
    }

    private static string Str(JsonElement obj, string name) =>
        obj.ValueKind == JsonValueKind.Object
        && obj.TryGetProperty(name, out var p)
        && p.ValueKind == JsonValueKind.String
            ? p.GetString() ?? ""
            : "";
}
