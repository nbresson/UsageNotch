namespace UsageNotch.Core.Hooks;

/// <summary>
/// Une page web ouverte dans un navigateur peut poster sur 127.0.0.1. Les navigateurs y joignent Origin et
/// Sec-Fetch-Site ; le hook natif n'envoie ni l'un ni l'autre. Tout Origin non local, tout doublon d'Origin
/// et tout Sec-Fetch-Site: cross-site sont refusés.
/// </summary>
public static class HookRequestGuard
{
    public static bool IsForbidden(IEnumerable<KeyValuePair<string, string>> headers)
    {
        var originCount = 0;
        foreach (var (name, value) in headers)
        {
            if (name.Equals("Origin", StringComparison.OrdinalIgnoreCase))
            {
                originCount++;
                if (originCount > 1 || !IsAllowedOrigin(value)) return true;
            }
            if (name.Equals("Sec-Fetch-Site", StringComparison.OrdinalIgnoreCase)
                && value.Trim().Equals("cross-site", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public static bool IsAllowedOrigin(string origin)
    {
        var s = origin.Trim();
        if (s.Length == 0 || s.Equals("null", StringComparison.OrdinalIgnoreCase)) return false;

        string rest;
        if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) rest = s[7..];
        else if (s.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) rest = s[8..];
        else if (s.Contains("://", StringComparison.Ordinal)) return false;
        else rest = s;

        if (rest.IndexOfAny(['@', '/', '\\', '?', '#']) >= 0) return false;

        string host;
        string? port;
        if (rest.StartsWith('['))
        {
            var end = rest.IndexOf(']');
            if (end < 0) return false;
            host = rest[..(end + 1)];
            var after = rest[(end + 1)..];
            if (after.Length == 0) port = null;
            else if (after.StartsWith(':')) port = after[1..];
            else return false;
        }
        else
        {
            var colon = rest.IndexOf(':');
            host = colon < 0 ? rest : rest[..colon];
            port = colon < 0 ? null : rest[(colon + 1)..];
        }

        if (port is not null && (!ushort.TryParse(port, out var p) || p == 0)) return false;

        return host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase)
            || host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host == "[::1]";
    }
}
