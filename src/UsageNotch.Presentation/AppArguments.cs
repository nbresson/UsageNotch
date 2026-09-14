namespace UsageNotch.Presentation;

/// <summary><c>doctor</c> affiche le diagnostic et quitte ; <c>--demo</c> utilise des données fixes ; <c>--from-hook</c> signale un lancement par le hook.</summary>
public sealed record AppArguments(bool Doctor, bool Demo, bool FromHook)
{
    public static AppArguments Parse(IReadOnlyList<string> args)
    {
        bool Has(params string[] names) => args.Any(a => names.Any(n => string.Equals(a, n, StringComparison.OrdinalIgnoreCase)));
        return new AppArguments(Has("doctor", "--doctor"), Has("--demo"), Has("--from-hook"));
    }
}
