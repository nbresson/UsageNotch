# UsageNotch — Plan 1 : Core et Hook

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Construire toute la logique non visuelle de UsageNotch (modèles, fournisseur Claude, magasin et planificateur d'usage, machine à états des sessions, récepteur et installeur de hooks, réglages, calcul de placement) et l'exécutable hook, entièrement couverts par des tests xUnit.

**Architecture:** Une bibliothèque `UsageNotch.Core` sans dépendance UI, où chaque décision est une classe testable qui reçoit un `TimeProvider` et des chemins de fichiers injectés. Un exécutable `UsageNotch.Hook` en Native AOT, sans dépendance, qui relaie les événements Claude Code au récepteur HTTP local. Le projet WPF fait l'objet du Plan 2, qui consomme les interfaces produites ici.

**Tech Stack:** .NET 9 (SDK 9.0.205 installé), C# 13, xUnit, FluentAssertions, Microsoft.Extensions.TimeProvider.Testing, Microsoft.Extensions.Hosting.Abstractions, System.Text.Json, HttpListener.

**Spec:** `docs/superpowers/specs/2026-09-14-usagenotch-design.md`

**Raffinement par rapport à la spec :** `IUsageProvider.FetchAsync` retourne un `FetchResult` (Success / NeedsAuth / RateLimited / Failed) plutôt qu'un `UsageSnapshot`. C'est `UsageStore` qui compose le snapshot à partir du résultat et de la lecture précédente, ce qui garde la règle « conserver la dernière bonne lecture » en un seul endroit. Le contrat visible par l'UI (un `UsageSnapshot` publié par le magasin) est inchangé.

## Global Constraints

- Cible : `net9.0` pour Core, Tests et Hook. `global.json` épingle le SDK `9.0.205` avec `rollForward: latestFeature`.
- `Nullable` et `ImplicitUsings` activés, `TreatWarningsAsErrors` à `true`, dans `Directory.Build.props`.
- Aucune dépendance NuGet dans `UsageNotch.Hook`. Il est publié en Native AOT.
- Aucun jeton, aucun contenu de prompt dans les journaux ni dans les messages d'exception.
- Tous les textes destinés à l'utilisateur sont en français.
- Toute écriture de fichier de données est atomique : fichier temporaire puis `File.Move(temp, cible, overwrite: true)`.
- Toute classe qui lit l'heure reçoit un `TimeProvider` par constructeur ; jamais `DateTime.Now` ni `DateTimeOffset.UtcNow` en direct.
- Chemins de données par défaut : `%APPDATA%\UsageNotch\` (settings.json, usage.json, logs\). Chaque classe reçoit son chemin par constructeur pour que les tests utilisent un dossier temporaire.
- Port par défaut du récepteur de hooks : `48666`.
- Commits fréquents, un par tâche au minimum, messages en anglais au format `type: description`, terminés par les lignes d'attribution :
  ```
  Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01DhmM4QvqWKwELXSCEZcweW
  ```
  Pour un message multi-lignes, écrire le message dans un fichier et utiliser `git commit -F <fichier>`.

## Structure des fichiers

```
UsageNotch.sln
global.json
Directory.Build.props
.editorconfig
src/UsageNotch.Core/
  UsageNotch.Core.csproj
  Usage/SnapshotStatus.cs          enum des statuts
  Usage/LimitWindow.cs             record d'une fenêtre de limite
  Usage/UsageSnapshot.cs           record du snapshot publié
  Usage/FetchResult.cs             résultat d'un appel fournisseur
  Usage/IUsageProvider.cs          interface fournisseur
  Usage/ClaudeUsageParser.cs       JSON → fenêtres
  Usage/ClaudeCredentialReader.cs  .credentials.json → jeton
  Usage/BackoffPolicy.cs           progression 60 s × 2^n plafonnée
  Usage/ClaudeUsageProvider.cs     appel HTTP, 401 retry, 429, erreurs
  Usage/UsageStore.cs              dernière bonne lecture, Stale, persistance, Changed
  Usage/UsagePoller.cs             BackgroundService : cadence, rafraîchissement forcé
  Sessions/SessionState.cs         enum
  Sessions/HookEvent.cs            record d'un événement reçu
  Sessions/Session.cs              record d'une session
  Sessions/SessionStore.cs         machine à états, balayage, Changed
  Sessions/SessionSweeper.cs       BackgroundService : Sweep toutes les 30 s
  Hooks/HookRequestGuard.cs        filtrage Origin / Sec-Fetch-Site
  Hooks/HookEventParser.cs         requête + corps JSON → HookEvent
  Hooks/HookListener.cs            BackgroundService HttpListener
  Hooks/HookInstaller.cs           fusion dans ~/.claude/settings.json
  Settings/ScreenEdge.cs           enum
  Settings/VisibilityMode.cs       enum
  Settings/CellContent.cs          enum
  Settings/ThemePreset.cs          enum
  Settings/Theme.cs                record des couleurs (hex string) et seuils
  Settings/Settings.cs             record immuable, valeurs par défaut, Clamp()
  Settings/SettingsStore.cs        lecture tolérante, écriture atomique, Changed
  Placement/PixelRect.cs           rectangle en pixels physiques
  Placement/MonitorInfo.cs         record d'un écran
  Placement/PillPlacement.cs       calcul pur de position
src/UsageNotch.Hook/
  UsageNotch.Hook.csproj           Native AOT, InvariantGlobalization
  Program.cs                       point d'entrée
  PortReader.cs                    lecture textuelle de "port" dans settings.json
  ParentProcess.cs                 PID parent via NtQueryInformationProcess
tests/UsageNotch.Core.Tests/
  UsageNotch.Core.Tests.csproj     référence Core + lien source PortReader.cs
  TempDir.cs                       dossier temporaire jetable
  Usage/ClaudeUsageParserTests.cs
  Usage/ClaudeCredentialReaderTests.cs
  Usage/BackoffPolicyTests.cs
  Usage/ClaudeUsageProviderTests.cs
  Usage/UsageStoreTests.cs
  Usage/UsagePollerTests.cs
  Sessions/SessionStoreTests.cs
  Hooks/HookRequestGuardTests.cs
  Hooks/HookEventParserTests.cs
  Hooks/HookListenerTests.cs
  Hooks/HookInstallerTests.cs
  Settings/SettingsStoreTests.cs
  Placement/PillPlacementTests.cs
  Hook/PortReaderTests.cs
```

---

### Task 1 : Squelette de la solution

**Files:**
- Create: `global.json`, `Directory.Build.props`, `.editorconfig`, `UsageNotch.sln`
- Create: `src/UsageNotch.Core/UsageNotch.Core.csproj`
- Create: `src/UsageNotch.Hook/UsageNotch.Hook.csproj`, `src/UsageNotch.Hook/Program.cs`
- Create: `tests/UsageNotch.Core.Tests/UsageNotch.Core.Tests.csproj`, `tests/UsageNotch.Core.Tests/TempDir.cs`, `tests/UsageNotch.Core.Tests/SmokeTests.cs`

**Interfaces:**
- Produces: la classe utilitaire `TempDir` (`IDisposable`, propriété `Path`) utilisée par tous les tests de fichiers.

- [ ] **Step 1 : Créer les fichiers racine**

`global.json` :
```json
{
  "sdk": {
    "version": "9.0.205",
    "rollForward": "latestFeature"
  }
}
```

`Directory.Build.props` :
```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>13</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest</AnalysisLevel>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
```

`.editorconfig` :
```ini
root = true

[*]
charset = utf-8
end_of_line = crlf
insert_final_newline = true
indent_style = space
indent_size = 4
trim_trailing_whitespace = true

[*.{json,xml,csproj,props,targets,yml}]
indent_size = 2

[*.cs]
csharp_style_namespace_declarations = file_scoped:warning
csharp_style_var_for_built_in_types = true:suggestion
csharp_style_var_when_type_is_apparent = true:suggestion
csharp_style_var_elsewhere = true:suggestion
dotnet_style_qualification_for_field = false:warning
dotnet_diagnostic.CA1848.severity = none
dotnet_diagnostic.CA2007.severity = none
dotnet_diagnostic.CA1031.severity = none
dotnet_diagnostic.CA1062.severity = none
```

- [ ] **Step 2 : Créer la solution et les projets**

Depuis la racine du dépôt :
```powershell
dotnet new sln -n UsageNotch
dotnet new classlib -n UsageNotch.Core -o src/UsageNotch.Core -f net9.0
dotnet new console  -n UsageNotch.Hook -o src/UsageNotch.Hook -f net9.0
dotnet new xunit    -n UsageNotch.Core.Tests -o tests/UsageNotch.Core.Tests -f net9.0
dotnet sln add src/UsageNotch.Core src/UsageNotch.Hook tests/UsageNotch.Core.Tests
dotnet add tests/UsageNotch.Core.Tests reference src/UsageNotch.Core
dotnet add src/UsageNotch.Core package Microsoft.Extensions.Hosting.Abstractions
dotnet add src/UsageNotch.Core package Microsoft.Extensions.Logging.Abstractions
dotnet add tests/UsageNotch.Core.Tests package FluentAssertions --version 7.2.0
dotnet add tests/UsageNotch.Core.Tests package Microsoft.Extensions.TimeProvider.Testing
Remove-Item src/UsageNotch.Core/Class1.cs, tests/UsageNotch.Core.Tests/UnitTest1.cs
```

- [ ] **Step 3 : Ajuster les csproj**

`src/UsageNotch.Core/UsageNotch.Core.csproj` — s'assurer qu'il contient :
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <RootNamespace>UsageNotch.Core</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting.Abstractions" Version="9.0.*" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="9.0.*" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="UsageNotch.Core.Tests" />
  </ItemGroup>
</Project>
```

`src/UsageNotch.Hook/UsageNotch.Hook.csproj` :
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <RootNamespace>UsageNotch.Hook</RootNamespace>
    <PublishAot>true</PublishAot>
    <InvariantGlobalization>true</InvariantGlobalization>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <AssemblyName>UsageNotch.Hook</AssemblyName>
  </PropertyGroup>
</Project>
```

`src/UsageNotch.Hook/Program.cs` (provisoire, remplacé en Task 15) :
```csharp
return 0;
```

`tests/UsageNotch.Core.Tests/UsageNotch.Core.Tests.csproj` — ajouter dans un `ItemGroup` le lien source qui permettra de tester le lecteur de port du hook sans référencer un exécutable :
```xml
<ItemGroup>
  <Compile Include="..\..\src\UsageNotch.Hook\PortReader.cs" Link="Hook\PortReader.cs" Condition="Exists('..\..\src\UsageNotch.Hook\PortReader.cs')" />
</ItemGroup>
```

- [ ] **Step 4 : Écrire l'utilitaire de dossier temporaire et un test de fumée**

`tests/UsageNotch.Core.Tests/TempDir.cs` :
```csharp
namespace UsageNotch.Core.Tests;

public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "usagenotch-tests", Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
    }
}
```

`tests/UsageNotch.Core.Tests/SmokeTests.cs` :
```csharp
using FluentAssertions;

namespace UsageNotch.Core.Tests;

public class SmokeTests
{
    [Fact]
    public void TempDir_creates_and_deletes_a_directory()
    {
        string path;
        using (var dir = new TempDir())
        {
            path = dir.Path;
            Directory.Exists(path).Should().BeTrue();
        }
        Directory.Exists(path).Should().BeFalse();
    }
}
```

- [ ] **Step 5 : Construire et tester**

Run: `dotnet test`
Expected: `Passed! - Failed: 0, Passed: 1`

- [ ] **Step 6 : Commit**

```powershell
git add -A
git commit -m "chore: solution skeleton (Core, Hook, Tests)"
```
(avec les lignes d'attribution des contraintes globales)

---

### Task 2 : Modèles d'usage et parseur Claude

**Files:**
- Create: `src/UsageNotch.Core/Usage/SnapshotStatus.cs`, `LimitWindow.cs`, `UsageSnapshot.cs`, `FetchResult.cs`, `IUsageProvider.cs`, `ClaudeUsageParser.cs`
- Test: `tests/UsageNotch.Core.Tests/Usage/ClaudeUsageParserTests.cs`

**Interfaces:**
- Produces:
  - `enum SnapshotStatus { Ok, Stale, NeedsAuth, Backoff, Error, Absent }`
  - `record LimitWindow(string Id, string Label, double UsedFraction, DateTimeOffset ResetsAt)`
  - `record UsageSnapshot(SnapshotStatus Status, IReadOnlyList<LimitWindow> Windows, DateTimeOffset FetchedAt, string Note, DateTimeOffset? BackoffUntil)` avec `static UsageSnapshot Empty`
  - `abstract record FetchResult` et ses cas `Success(IReadOnlyList<LimitWindow> Windows)`, `NeedsAuth(string Note)`, `RateLimited(TimeSpan RetryAfter)`, `Failed(string Note)`
  - `interface IUsageProvider { string Id; string DisplayName; string HeadlineWindowId; Task<FetchResult> FetchAsync(CancellationToken ct); }`
  - `static IReadOnlyList<LimitWindow> ClaudeUsageParser.Parse(string json)` — lève `JsonException` si le JSON est illisible.

- [ ] **Step 1 : Écrire les tests du parseur**

`tests/UsageNotch.Core.Tests/Usage/ClaudeUsageParserTests.cs` :
```csharp
using System.Text.Json;
using FluentAssertions;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class ClaudeUsageParserTests
{
    private const string Reset1 = "2026-09-14T20:00:00Z";
    private const string Reset2 = "2026-09-18T00:00:00Z";

    [Fact]
    public void Reads_limits_array_and_puts_session_first()
    {
        var json = $$"""
        { "limits": [
            { "kind": "weekly_all", "percent": 7,  "resets_at": "{{Reset2}}" },
            { "kind": "session",    "percent": 73, "resets_at": "{{Reset1}}" }
        ] }
        """;

        var windows = ClaudeUsageParser.Parse(json);

        windows.Should().HaveCount(2);
        windows[0].Id.Should().Be("session");
        windows[0].Label.Should().Be("Session en cours");
        windows[0].UsedFraction.Should().BeApproximately(0.73, 1e-9);
        windows[0].ResetsAt.Should().Be(DateTimeOffset.Parse(Reset1));
        windows[1].Id.Should().Be("weekly_all");
        windows[1].Label.Should().Be("Hebdomadaire (tous modèles)");
    }

    [Fact]
    public void Skips_a_limit_without_reset_time()
    {
        var json = """{ "limits": [ { "kind": "session", "percent": 10 } ] }""";
        ClaudeUsageParser.Parse(json).Should().BeEmpty();
    }

    [Fact]
    public void Clamps_percent_into_0_1()
    {
        var json = $$"""{ "limits": [ { "kind": "session", "percent": 140, "resets_at": "{{Reset1}}" } ] }""";
        ClaudeUsageParser.Parse(json)[0].UsedFraction.Should().Be(1.0);
    }

    [Fact]
    public void Falls_back_to_five_hour_and_seven_day_when_limits_is_absent()
    {
        var json = $$"""
        { "five_hour": { "utilization": 42, "resets_at": "{{Reset1}}" },
          "seven_day": { "utilization": 9,  "resets_at": "{{Reset2}}" } }
        """;

        var windows = ClaudeUsageParser.Parse(json);

        windows.Select(w => w.Id).Should().Equal("session", "weekly_all");
        windows[0].UsedFraction.Should().BeApproximately(0.42, 1e-9);
    }

    [Fact]
    public void Does_not_duplicate_a_fallback_that_matches_a_limit_by_alias()
    {
        var json = $$"""
        { "limits": [ { "kind": "session", "percent": 73, "resets_at": "{{Reset1}}" } ],
          "five_hour": { "utilization": 73, "resets_at": "{{Reset1}}" } }
        """;
        ClaudeUsageParser.Parse(json).Should().HaveCount(1);
    }

    [Fact]
    public void Does_not_duplicate_a_fallback_that_matches_by_reset_and_value()
    {
        // weekly_scoped n'est pas un alias de seven_day, mais même reset et même valeur : doublon
        var json = $$"""
        { "limits": [ { "kind": "weekly_scoped", "percent": 9, "resets_at": "{{Reset2}}" } ],
          "seven_day": { "utilization": 9.2, "resets_at": "{{Reset2}}" } }
        """;
        ClaudeUsageParser.Parse(json).Should().HaveCount(1);
    }

    [Fact]
    public void Adds_the_fallback_when_it_is_genuinely_different()
    {
        var json = $$"""
        { "limits": [ { "kind": "weekly_opus", "percent": 30, "resets_at": "{{Reset2}}" } ],
          "seven_day": { "utilization": 9, "resets_at": "{{Reset2}}" } }
        """;
        ClaudeUsageParser.Parse(json).Select(w => w.Id).Should().Equal("weekly_opus", "weekly_all");
    }

    [Theory]
    [InlineData("seven_day_opus", "Hebdomadaire (Opus)")]
    [InlineData("weekly_opus", "Hebdomadaire (Opus)")]
    [InlineData("weekly_scoped", "Hebdomadaire (par modèle)")]
    [InlineData("some_new_kind", "Some new kind")]
    public void Labels_known_and_unknown_kinds(string kind, string label)
    {
        var json = $$"""{ "limits": [ { "kind": "{{kind}}", "percent": 1, "resets_at": "{{Reset1}}" } ] }""";
        ClaudeUsageParser.Parse(json)[0].Label.Should().Be(label);
    }

    [Fact]
    public void Throws_on_invalid_json()
    {
        var act = () => ClaudeUsageParser.Parse("{ not json");
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Returns_empty_on_an_object_without_usage_fields()
    {
        ClaudeUsageParser.Parse("{}").Should().BeEmpty();
    }
}
```

- [ ] **Step 2 : Lancer les tests, vérifier l'échec de compilation**

Run: `dotnet test --filter ClaudeUsageParserTests`
Expected: erreur de compilation, `UsageNotch.Core.Usage` inconnu.

- [ ] **Step 3 : Écrire les modèles**

`src/UsageNotch.Core/Usage/SnapshotStatus.cs` :
```csharp
namespace UsageNotch.Core.Usage;

public enum SnapshotStatus
{
    Ok,
    Stale,
    NeedsAuth,
    Backoff,
    Error,
    Absent,
}
```

`src/UsageNotch.Core/Usage/LimitWindow.cs` :
```csharp
namespace UsageNotch.Core.Usage;

/// <summary>Une fenêtre de limite : « session », « weekly_all »… avec sa fraction utilisée (0 à 1).</summary>
public sealed record LimitWindow(string Id, string Label, double UsedFraction, DateTimeOffset ResetsAt);
```

`src/UsageNotch.Core/Usage/UsageSnapshot.cs` :
```csharp
namespace UsageNotch.Core.Usage;

public sealed record UsageSnapshot(
    SnapshotStatus Status,
    IReadOnlyList<LimitWindow> Windows,
    DateTimeOffset FetchedAt,
    string Note,
    DateTimeOffset? BackoffUntil)
{
    public static UsageSnapshot Empty { get; } = new(SnapshotStatus.Error, [], DateTimeOffset.MinValue, "", null);

    public LimitWindow? Window(string id) => Windows.FirstOrDefault(w => w.Id == id);
}
```

`src/UsageNotch.Core/Usage/FetchResult.cs` :
```csharp
namespace UsageNotch.Core.Usage;

/// <summary>Résultat brut d'un appel fournisseur. Le magasin en fait un snapshot.</summary>
public abstract record FetchResult
{
    private FetchResult() { }

    public sealed record Success(IReadOnlyList<LimitWindow> Windows) : FetchResult;
    public sealed record NeedsAuth(string Note) : FetchResult;
    public sealed record RateLimited(TimeSpan RetryAfter) : FetchResult;
    public sealed record Failed(string Note) : FetchResult;
}
```

`src/UsageNotch.Core/Usage/IUsageProvider.cs` :
```csharp
namespace UsageNotch.Core.Usage;

public interface IUsageProvider
{
    /// <summary>Identifiant stable, ex. « claude ».</summary>
    string Id { get; }

    string DisplayName { get; }

    /// <summary>La fenêtre que l'anneau dessine. Absente de la réponse : la cellule montre un tiret.</summary>
    string HeadlineWindowId { get; }

    Task<FetchResult> FetchAsync(CancellationToken ct);
}
```

- [ ] **Step 4 : Écrire le parseur**

`src/UsageNotch.Core/Usage/ClaudeUsageParser.cs` :
```csharp
using System.Globalization;
using System.Text.Json;

namespace UsageNotch.Core.Usage;

public static class ClaudeUsageParser
{
    private static readonly (string Field, string Id, string[] Aliases)[] Fallbacks =
    [
        ("five_hour", "session", ["session", "five_hour"]),
        ("seven_day", "weekly_all", ["seven_day", "weekly_all", "weekly"]),
    ];

    /// <summary>Lit <c>limits[]</c>, puis fusionne <c>five_hour</c> / <c>seven_day</c> en secours. Lève <see cref="JsonException"/> si le JSON est illisible.</summary>
    public static IReadOnlyList<LimitWindow> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var list = new List<LimitWindow>();

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("limits", out var limits)
            && limits.ValueKind == JsonValueKind.Array)
        {
            foreach (var l in limits.EnumerateArray())
            {
                if (!TryString(l, "kind", out var kind)) continue;
                if (!TryDouble(l, "percent", out var pct)) continue;
                if (!TryReset(l, out var resets)) continue;
                list.Add(new LimitWindow(kind, LabelFor(kind), Fraction(pct), resets));
            }
        }

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var (field, id, aliases) in Fallbacks)
            {
                MergeFallback(root, field, id, aliases, list);
            }
        }

        return list.OrderBy(w => w.Id == "session" ? 0 : 1).ToList();
    }

    public static string LabelFor(string kind) => kind switch
    {
        "session" => "Session en cours",
        "seven_day" or "weekly_all" => "Hebdomadaire (tous modèles)",
        "seven_day_opus" or "weekly_opus" => "Hebdomadaire (Opus)",
        "weekly_scoped" => "Hebdomadaire (par modèle)",
        _ => Humanize(kind),
    };

    private static void MergeFallback(JsonElement root, string field, string id, string[] aliases, List<LimitWindow> list)
    {
        if (!root.TryGetProperty(field, out var w) || w.ValueKind != JsonValueKind.Object) return;
        if (!TryDouble(w, "utilization", out var u)) return;
        if (!TryReset(w, out var resets)) return;

        var used = Fraction(u);
        var label = LabelFor(id);
        var duplicate = list.Any(x =>
            aliases.Contains(x.Id)
            || x.Label == label
            || (x.ResetsAt.ToUnixTimeSeconds() == resets.ToUnixTimeSeconds()
                && Math.Abs(x.UsedFraction - used) < 0.005));
        if (!duplicate)
        {
            list.Add(new LimitWindow(id, label, used, resets));
        }
    }

    private static double Fraction(double percent) => Math.Clamp(percent / 100.0, 0.0, 1.0);

    private static string Humanize(string kind)
    {
        var s = kind.Replace('_', ' ');
        return s.Length == 0 ? s : char.ToUpper(s[0], CultureInfo.InvariantCulture) + s[1..];
    }

    private static bool TryString(JsonElement e, string name, out string value)
    {
        value = "";
        if (e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String)
        {
            value = p.GetString() ?? "";
            return value.Length > 0;
        }
        return false;
    }

    private static bool TryDouble(JsonElement e, string name, out double value)
    {
        value = 0;
        return e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetDouble(out value);
    }

    private static bool TryReset(JsonElement e, out DateTimeOffset value)
    {
        value = default;
        return e.TryGetProperty("resets_at", out var p)
            && p.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(p.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out value);
    }
}
```

- [ ] **Step 5 : Lancer les tests**

Run: `dotnet test --filter ClaudeUsageParserTests`
Expected: 13 tests passés (9 Facts + 4 Theory cases).

- [ ] **Step 6 : Commit**

```powershell
git add src/UsageNotch.Core/Usage tests/UsageNotch.Core.Tests/Usage
git commit -m "feat(core): usage models and Claude usage parser"
```

---

### Task 3 : Lecture du jeton Claude Code

**Files:**
- Create: `src/UsageNotch.Core/Usage/ClaudeCredentialReader.cs`
- Test: `tests/UsageNotch.Core.Tests/Usage/ClaudeCredentialReaderTests.cs`

**Interfaces:**
- Produces:
  - `record ClaudeCredential(string AccessToken, bool IsExpired)`
  - `class ClaudeCredentialReader(string claudeDirectory, TimeProvider time)` avec `ClaudeCredential? Read()` et `static string DefaultDirectory`.

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Core.Tests/Usage/ClaudeCredentialReaderTests.cs` :
```csharp
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class ClaudeCredentialReaderTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private static ClaudeCredentialReader Reader(TempDir dir) =>
        new(dir.Path, new FakeTimeProvider(Now));

    [Fact]
    public void Returns_null_when_no_file_exists()
    {
        using var dir = new TempDir();
        Reader(dir).Read().Should().BeNull();
    }

    [Fact]
    public void Returns_null_on_invalid_json()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File(".credentials.json"), "{ broken");
        Reader(dir).Read().Should().BeNull();
    }

    [Fact]
    public void Returns_null_when_access_token_is_missing()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File(".credentials.json"), """{ "claudeAiOauth": { "refreshToken": "r" } }""");
        Reader(dir).Read().Should().BeNull();
    }

    [Fact]
    public void Reads_nested_claudeAiOauth_shape_and_expiry()
    {
        using var dir = new TempDir();
        var expiresAt = Now.AddHours(1).ToUnixTimeMilliseconds();
        File.WriteAllText(dir.File(".credentials.json"),
            $$"""{ "claudeAiOauth": { "accessToken": "sk-ant-abc", "expiresAt": {{expiresAt}} } }""");

        var cred = Reader(dir).Read();

        cred.Should().NotBeNull();
        cred!.AccessToken.Should().Be("sk-ant-abc");
        cred.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void Flags_an_expired_token()
    {
        using var dir = new TempDir();
        var expiresAt = Now.AddMinutes(-1).ToUnixTimeMilliseconds();
        File.WriteAllText(dir.File(".credentials.json"),
            $$"""{ "claudeAiOauth": { "accessToken": "sk-ant-abc", "expiresAt": {{expiresAt}} } }""");

        Reader(dir).Read()!.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void Reads_flat_shape_without_wrapper()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File(".credentials.json"), """{ "accessToken": "flat-token" }""");
        Reader(dir).Read()!.AccessToken.Should().Be("flat-token");
    }

    [Fact]
    public void Falls_back_to_credentials_json_without_dot()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("credentials.json"), """{ "claudeAiOauth": { "accessToken": "second" } }""");
        Reader(dir).Read()!.AccessToken.Should().Be("second");
    }

    [Fact]
    public void Default_directory_is_dot_claude_under_the_user_profile()
    {
        ClaudeCredentialReader.DefaultDirectory.Should().EndWith(".claude");
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test --filter ClaudeCredentialReaderTests`
Expected: erreur de compilation, `ClaudeCredentialReader` inconnu.

- [ ] **Step 3 : Implémenter**

`src/UsageNotch.Core/Usage/ClaudeCredentialReader.cs` :
```csharp
using System.Text.Json;

namespace UsageNotch.Core.Usage;

public sealed record ClaudeCredential(string AccessToken, bool IsExpired);

/// <summary>Lit le jeton OAuth que Claude Code conserve dans <c>~/.claude/.credentials.json</c>. Lecture seule, jamais de rafraîchissement.</summary>
public sealed class ClaudeCredentialReader(string claudeDirectory, TimeProvider time)
{
    private static readonly string[] FileNames = [".credentials.json", "credentials.json"];

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

    public string Directory { get; } = claudeDirectory;

    public ClaudeCredential? Read()
    {
        foreach (var name in FileNames)
        {
            var path = Path.Combine(Directory, name);
            string text;
            try
            {
                if (!File.Exists(path)) continue;
                text = File.ReadAllText(path);
            }
            catch (IOException) { continue; }
            catch (UnauthorizedAccessException) { continue; }

            var cred = ParseCredential(text);
            if (cred is not null) return cred;
        }
        return null;
    }

    private ClaudeCredential? ParseCredential(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            var oauth = root.TryGetProperty("claudeAiOauth", out var nested) && nested.ValueKind == JsonValueKind.Object
                ? nested
                : root;

            if (!oauth.TryGetProperty("accessToken", out var tokenEl) || tokenEl.ValueKind != JsonValueKind.String) return null;
            var token = tokenEl.GetString();
            if (string.IsNullOrEmpty(token)) return null;

            var expired = false;
            if (oauth.TryGetProperty("expiresAt", out var expEl) && expEl.ValueKind == JsonValueKind.Number && expEl.TryGetDouble(out var ms))
            {
                expired = (long)ms <= time.GetUtcNow().ToUnixTimeMilliseconds();
            }
            return new ClaudeCredential(token, expired);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
```

- [ ] **Step 4 : Lancer les tests**

Run: `dotnet test --filter ClaudeCredentialReaderTests`
Expected: 8 tests passés.

- [ ] **Step 5 : Commit**

```powershell
git add src/UsageNotch.Core/Usage/ClaudeCredentialReader.cs tests/UsageNotch.Core.Tests/Usage/ClaudeCredentialReaderTests.cs
git commit -m "feat(core): read Claude Code OAuth credential file"
```

---

### Task 4 : Politique de backoff

**Files:**
- Create: `src/UsageNotch.Core/Usage/BackoffPolicy.cs`
- Test: `tests/UsageNotch.Core.Tests/Usage/BackoffPolicyTests.cs`

**Interfaces:**
- Produces: `class BackoffPolicy` avec `TimeSpan Next(TimeSpan retryAfterFloor)`, `void Reset()`, `int ConsecutiveFailures`.

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Core.Tests/Usage/BackoffPolicyTests.cs` :
```csharp
using FluentAssertions;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class BackoffPolicyTests
{
    [Fact]
    public void Doubles_from_60s_and_caps_at_15_minutes()
    {
        var policy = new BackoffPolicy();
        var waits = Enumerable.Range(0, 7).Select(_ => policy.Next(TimeSpan.Zero).TotalSeconds).ToList();
        waits.Should().Equal(60, 120, 240, 480, 900, 900, 900);
        policy.ConsecutiveFailures.Should().Be(7);
    }

    [Fact]
    public void Retry_after_only_raises_the_wait()
    {
        var policy = new BackoffPolicy();
        policy.Next(TimeSpan.FromSeconds(10)).TotalSeconds.Should().Be(60);
        policy.Next(TimeSpan.FromSeconds(1000)).TotalSeconds.Should().Be(1000);
    }

    [Fact]
    public void Reset_starts_over_at_60s()
    {
        var policy = new BackoffPolicy();
        policy.Next(TimeSpan.Zero);
        policy.Next(TimeSpan.Zero);
        policy.Reset();
        policy.ConsecutiveFailures.Should().Be(0);
        policy.Next(TimeSpan.Zero).TotalSeconds.Should().Be(60);
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test --filter BackoffPolicyTests`
Expected: erreur de compilation.

- [ ] **Step 3 : Implémenter**

`src/UsageNotch.Core/Usage/BackoffPolicy.cs` :
```csharp
namespace UsageNotch.Core.Usage;

/// <summary>60 s × 2^n, plafonné à 15 min ; un Retry-After ne fait que relever le plancher.</summary>
public sealed class BackoffPolicy
{
    public static readonly TimeSpan Base = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan Cap = TimeSpan.FromMinutes(15);

    public int ConsecutiveFailures { get; private set; }

    public TimeSpan Next(TimeSpan retryAfterFloor)
    {
        var exponent = Math.Min(ConsecutiveFailures, 4);
        ConsecutiveFailures++;
        var wait = TimeSpan.FromTicks(Base.Ticks * (1L << exponent));
        if (wait > Cap) wait = Cap;
        return wait > retryAfterFloor ? wait : retryAfterFloor;
    }

    public void Reset() => ConsecutiveFailures = 0;
}
```

- [ ] **Step 4 : Lancer les tests**

Run: `dotnet test --filter BackoffPolicyTests`
Expected: 3 tests passés.

- [ ] **Step 5 : Commit**

```powershell
git add src/UsageNotch.Core/Usage/BackoffPolicy.cs tests/UsageNotch.Core.Tests/Usage/BackoffPolicyTests.cs
git commit -m "feat(core): exponential backoff policy"
```

---

### Task 5 : Fournisseur Claude (appel HTTP)

**Files:**
- Create: `src/UsageNotch.Core/Usage/ClaudeUsageProvider.cs`
- Test: `tests/UsageNotch.Core.Tests/Usage/ClaudeUsageProviderTests.cs`

**Interfaces:**
- Consumes: `ClaudeCredentialReader.Read()`, `ClaudeUsageParser.Parse(string)`, `FetchResult`.
- Produces: `class ClaudeUsageProvider(HttpClient http, ClaudeCredentialReader credentials, ILogger<ClaudeUsageProvider> logger) : IUsageProvider`, `const string Endpoint`, `const string BetaHeader`, `static readonly TimeSpan Timeout` (15 s, à appliquer sur le HttpClient à l'enregistrement DI dans le Plan 2).

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Core.Tests/Usage/ClaudeUsageProviderTests.cs` :
```csharp
using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class ClaudeUsageProviderTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(respond(request));
        }
    }

    private const string Body = """{ "limits": [ { "kind": "session", "percent": 73, "resets_at": "2026-09-14T20:00:00Z" } ] }""";

    private static void WriteToken(TempDir dir, string token) =>
        File.WriteAllText(dir.File(".credentials.json"), $$"""{ "claudeAiOauth": { "accessToken": "{{token}}", "expiresAt": 9999999999999 } }""");

    private static (ClaudeUsageProvider Provider, StubHandler Handler) Build(TempDir dir, Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var reader = new ClaudeCredentialReader(dir.Path, new FakeTimeProvider(new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero)));
        var provider = new ClaudeUsageProvider(new HttpClient(handler), reader, NullLogger<ClaudeUsageProvider>.Instance);
        return (provider, handler);
    }

    private static HttpResponseMessage Json(HttpStatusCode code, string body) =>
        new(code) { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Returns_NeedsAuth_without_calling_the_network_when_no_credential()
    {
        using var dir = new TempDir();
        var (provider, handler) = Build(dir, _ => Json(HttpStatusCode.OK, Body));

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.NeedsAuth>().Which.Note.Should().Contain("Aucun identifiant");
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Sends_bearer_and_beta_header_and_parses_success()
    {
        using var dir = new TempDir();
        WriteToken(dir, "tok-1");
        var (provider, handler) = Build(dir, _ => Json(HttpStatusCode.OK, Body));

        var result = await provider.FetchAsync(CancellationToken.None);

        var success = result.Should().BeOfType<FetchResult.Success>().Subject;
        success.Windows.Should().ContainSingle(w => w.Id == "session");
        var req = handler.Requests.Single();
        req.RequestUri!.ToString().Should().Be(ClaudeUsageProvider.Endpoint);
        req.Headers.Authorization.Should().Be(new AuthenticationHeaderValue("Bearer", "tok-1"));
        req.Headers.GetValues("anthropic-beta").Should().Equal(ClaudeUsageProvider.BetaHeader);
    }

    [Fact]
    public async Task Rereads_the_credential_once_after_401_and_retries_if_it_changed()
    {
        using var dir = new TempDir();
        WriteToken(dir, "old");
        var (provider, handler) = Build(dir, req =>
        {
            if (req.Headers.Authorization!.Parameter == "old")
            {
                WriteToken(dir, "new"); // Claude Code vient de renouveler le jeton
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }
            return Json(HttpStatusCode.OK, Body);
        });

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.Success>();
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task Returns_NeedsAuth_when_401_and_the_credential_did_not_change()
    {
        using var dir = new TempDir();
        WriteToken(dir, "same");
        var (provider, handler) = Build(dir, _ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.NeedsAuth>().Which.Note.Should().Contain("refusé");
        handler.Requests.Should().HaveCount(1);
    }

    [Fact]
    public async Task Mentions_expiry_in_the_NeedsAuth_note_when_the_token_is_expired()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File(".credentials.json"), """{ "claudeAiOauth": { "accessToken": "exp", "expiresAt": 1 } }""");
        var (provider, _) = Build(dir, _ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.NeedsAuth>().Which.Note.Should().Contain("expiré");
    }

    [Fact]
    public async Task Returns_RateLimited_with_retry_after_on_429()
    {
        using var dir = new TempDir();
        WriteToken(dir, "t");
        var (provider, _) = Build(dir, _ =>
        {
            var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(120));
            return r;
        });

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.RateLimited>().Which.RetryAfter.Should().Be(TimeSpan.FromSeconds(120));
    }

    [Fact]
    public async Task Returns_RateLimited_zero_when_429_has_no_retry_after()
    {
        using var dir = new TempDir();
        WriteToken(dir, "t");
        var (provider, _) = Build(dir, _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.RateLimited>().Which.RetryAfter.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task Returns_Failed_with_status_code_on_other_http_errors()
    {
        using var dir = new TempDir();
        WriteToken(dir, "t");
        var (provider, _) = Build(dir, _ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.Failed>().Which.Note.Should().Be("HTTP 500");
    }

    [Fact]
    public async Task Returns_Failed_on_unreadable_body()
    {
        using var dir = new TempDir();
        WriteToken(dir, "t");
        var (provider, _) = Build(dir, _ => Json(HttpStatusCode.OK, "{ nope"));

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.Failed>().Which.Note.Should().StartWith("Réponse illisible");
    }

    [Fact]
    public async Task Returns_Failed_on_network_exception()
    {
        using var dir = new TempDir();
        WriteToken(dir, "t");
        var (provider, _) = Build(dir, _ => throw new HttpRequestException("connexion refusée"));

        var result = await provider.FetchAsync(CancellationToken.None);

        result.Should().BeOfType<FetchResult.Failed>().Which.Note.Should().Contain("connexion refusée");
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test --filter ClaudeUsageProviderTests`
Expected: erreur de compilation.

- [ ] **Step 3 : Implémenter**

`src/UsageNotch.Core/Usage/ClaudeUsageProvider.cs` :
```csharp
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace UsageNotch.Core.Usage;

/// <summary>Appelle l'endpoint d'usage OAuth d'Anthropic avec le jeton de Claude Code. Le délai de 15 s est réglé sur le HttpClient injecté.</summary>
public sealed class ClaudeUsageProvider(HttpClient http, ClaudeCredentialReader credentials, ILogger<ClaudeUsageProvider> logger) : IUsageProvider
{
    public const string Endpoint = "https://api.anthropic.com/api/oauth/usage";
    public const string BetaHeader = "oauth-2025-04-20";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public string Id => "claude";
    public string DisplayName => "Claude";
    public string HeadlineWindowId => "session";

    public async Task<FetchResult> FetchAsync(CancellationToken ct)
    {
        var cred = credentials.Read();
        if (cred is null)
        {
            return new FetchResult.NeedsAuth("Aucun identifiant Claude Code trouvé — connectez-vous une fois avec la CLI claude.");
        }

        var result = await FetchOnceAsync(cred.AccessToken, ct);

        if (result is FetchResult.NeedsAuth)
        {
            // Claude Code a peut-être renouvelé le jeton entre-temps : une relecture, un seul nouvel essai.
            var again = credentials.Read();
            if (again is not null && again.AccessToken != cred.AccessToken)
            {
                logger.LogInformation("Jeton Claude changé après un refus, nouvel essai");
                cred = again;
                result = await FetchOnceAsync(again.AccessToken, ct);
            }
        }

        if (result is FetchResult.NeedsAuth)
        {
            return new FetchResult.NeedsAuth(cred.IsExpired
                ? "Identifiant expiré — lancez une commande claude pour le renouveler."
                : "Identifiant refusé (changement de compte ?).");
        }

        return result;
    }

    private async Task<FetchResult> FetchOnceAsync(string token, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.TryAddWithoutValidation("anthropic-beta", BetaHeader);

            using var response = await http.SendAsync(request, ct);
            var code = (int)response.StatusCode;
            switch (code)
            {
                case 200:
                    var json = await response.Content.ReadAsStringAsync(ct);
                    return new FetchResult.Success(ClaudeUsageParser.Parse(json));
                case 401:
                case 403:
                    return new FetchResult.NeedsAuth("");
                case 429:
                    var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.Zero;
                    logger.LogWarning("Usage Claude : 429, Retry-After {Seconds}s", retryAfter.TotalSeconds);
                    return new FetchResult.RateLimited(retryAfter < TimeSpan.Zero ? TimeSpan.Zero : retryAfter);
                default:
                    logger.LogWarning("Usage Claude : HTTP {Code}", code);
                    return new FetchResult.Failed($"HTTP {code}");
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new FetchResult.Failed("Délai dépassé (15 s)");
        }
        catch (HttpRequestException e)
        {
            return new FetchResult.Failed(e.Message);
        }
        catch (JsonException e)
        {
            return new FetchResult.Failed("Réponse illisible : " + e.Message);
        }
    }
}
```

- [ ] **Step 4 : Lancer les tests**

Run: `dotnet test --filter ClaudeUsageProviderTests`
Expected: 10 tests passés.

- [ ] **Step 5 : Commit**

```powershell
git add src/UsageNotch.Core/Usage/ClaudeUsageProvider.cs tests/UsageNotch.Core.Tests/Usage/ClaudeUsageProviderTests.cs
git commit -m "feat(core): Claude usage provider with 401 retry and 429 handling"
```

---

### Task 6 : Magasin d'usage (dernière bonne lecture, Stale, persistance)

**Files:**
- Create: `src/UsageNotch.Core/Usage/UsageStore.cs`
- Test: `tests/UsageNotch.Core.Tests/Usage/UsageStoreTests.cs`

**Interfaces:**
- Consumes: `UsageSnapshot`, `FetchResult`, `SnapshotStatus`.
- Produces: `class UsageStore(string filePath, TimeProvider time, ILogger<UsageStore> logger)` avec `UsageSnapshot Current`, `event Action<UsageSnapshot>? Changed`, `void Load()`, `void Apply(FetchResult result, TimeSpan backoffWait = default)`, `bool IsInBackoff`, `void ClearBackoff()`.

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Core.Tests/Usage/UsageStoreTests.cs` :
```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class UsageStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly LimitWindow Session = new("session", "Session en cours", 0.73, Now.AddHours(2));

    private static (UsageStore Store, FakeTimeProvider Time) Build(TempDir dir)
    {
        var time = new FakeTimeProvider(Now);
        return (new UsageStore(dir.File("usage.json"), time, NullLogger<UsageStore>.Instance), time);
    }

    [Fact]
    public void Starts_empty()
    {
        using var dir = new TempDir();
        var (store, _) = Build(dir);
        store.Current.Should().Be(UsageSnapshot.Empty);
        store.IsInBackoff.Should().BeFalse();
    }

    [Fact]
    public void Success_becomes_Ok_and_is_persisted()
    {
        using var dir = new TempDir();
        var (store, _) = Build(dir);
        UsageSnapshot? published = null;
        store.Changed += s => published = s;

        store.Apply(new FetchResult.Success([Session]));

        store.Current.Status.Should().Be(SnapshotStatus.Ok);
        store.Current.Windows.Should().ContainSingle();
        store.Current.FetchedAt.Should().Be(Now);
        store.Current.Note.Should().BeEmpty();
        published.Should().Be(store.Current);
        File.Exists(dir.File("usage.json")).Should().BeTrue();
    }

    [Fact]
    public void Failure_after_a_reading_keeps_the_windows_and_marks_Stale()
    {
        using var dir = new TempDir();
        var (store, time) = Build(dir);
        store.Apply(new FetchResult.Success([Session]));
        time.Advance(TimeSpan.FromMinutes(3));

        store.Apply(new FetchResult.Failed("HTTP 500"));

        store.Current.Status.Should().Be(SnapshotStatus.Stale);
        store.Current.Windows.Should().ContainSingle();
        store.Current.FetchedAt.Should().Be(Now);
        store.Current.Note.Should().Be("HTTP 500");
    }

    [Fact]
    public void Failure_without_any_reading_is_Error()
    {
        using var dir = new TempDir();
        var (store, _) = Build(dir);
        store.Apply(new FetchResult.Failed("panne"));
        store.Current.Status.Should().Be(SnapshotStatus.Error);
    }

    [Fact]
    public void RateLimited_sets_backoff_deadline_and_Stale_when_a_reading_exists()
    {
        using var dir = new TempDir();
        var (store, time) = Build(dir);
        store.Apply(new FetchResult.Success([Session]));

        store.Apply(new FetchResult.RateLimited(TimeSpan.Zero), backoffWait: TimeSpan.FromSeconds(60));

        store.Current.Status.Should().Be(SnapshotStatus.Stale);
        store.Current.BackoffUntil.Should().Be(Now.AddSeconds(60));
        store.Current.Note.Should().Contain("60 s");
        store.IsInBackoff.Should().BeTrue();
        time.Advance(TimeSpan.FromSeconds(61));
        store.IsInBackoff.Should().BeFalse();
    }

    [Fact]
    public void RateLimited_without_reading_is_Backoff()
    {
        using var dir = new TempDir();
        var (store, _) = Build(dir);
        store.Apply(new FetchResult.RateLimited(TimeSpan.Zero), backoffWait: TimeSpan.FromSeconds(60));
        store.Current.Status.Should().Be(SnapshotStatus.Backoff);
    }

    [Fact]
    public void NeedsAuth_keeps_windows_and_clears_backoff()
    {
        using var dir = new TempDir();
        var (store, _) = Build(dir);
        store.Apply(new FetchResult.Success([Session]));
        store.Apply(new FetchResult.RateLimited(TimeSpan.Zero), backoffWait: TimeSpan.FromSeconds(60));

        store.Apply(new FetchResult.NeedsAuth("Identifiant refusé"));

        store.Current.Status.Should().Be(SnapshotStatus.NeedsAuth);
        store.Current.Windows.Should().ContainSingle();
        store.Current.BackoffUntil.Should().BeNull();
    }

    [Fact]
    public void Success_clears_a_previous_backoff()
    {
        using var dir = new TempDir();
        var (store, _) = Build(dir);
        store.Apply(new FetchResult.RateLimited(TimeSpan.Zero), backoffWait: TimeSpan.FromSeconds(60));
        store.Apply(new FetchResult.Success([Session]));
        store.Current.BackoffUntil.Should().BeNull();
        store.IsInBackoff.Should().BeFalse();
    }

    [Fact]
    public void ClearBackoff_removes_the_deadline_and_publishes()
    {
        using var dir = new TempDir();
        var (store, _) = Build(dir);
        store.Apply(new FetchResult.RateLimited(TimeSpan.Zero), backoffWait: TimeSpan.FromSeconds(60));
        var published = 0;
        store.Changed += _ => published++;

        store.ClearBackoff();

        store.Current.BackoffUntil.Should().BeNull();
        store.Current.Status.Should().Be(SnapshotStatus.Error);
        published.Should().Be(1);
        store.ClearBackoff();
        published.Should().Be(1, "rien à effacer, rien à publier");
    }

    [Fact]
    public void Load_restores_a_persisted_reading_as_Stale_with_its_backoff()
    {
        using var dir = new TempDir();
        var (first, _) = Build(dir);
        first.Apply(new FetchResult.Success([Session]));
        first.Apply(new FetchResult.RateLimited(TimeSpan.Zero), backoffWait: TimeSpan.FromMinutes(5));

        var (second, _) = Build(dir);
        UsageSnapshot? published = null;
        second.Changed += s => published = s;
        second.Load();

        second.Current.Status.Should().Be(SnapshotStatus.Stale);
        second.Current.Windows.Should().ContainSingle(w => w.Id == "session");
        second.Current.FetchedAt.Should().Be(Now);
        second.Current.BackoffUntil.Should().Be(Now.AddMinutes(5));
        published.Should().NotBeNull();
    }

    [Fact]
    public void Load_ignores_a_missing_or_corrupt_file()
    {
        using var dir = new TempDir();
        var (store, _) = Build(dir);
        store.Load();
        store.Current.Should().Be(UsageSnapshot.Empty);

        File.WriteAllText(dir.File("usage.json"), "{ corrupt");
        store.Load();
        store.Current.Should().Be(UsageSnapshot.Empty);
    }

    [Fact]
    public void Persisted_file_does_not_leave_a_temp_file_behind()
    {
        using var dir = new TempDir();
        var (store, _) = Build(dir);
        store.Apply(new FetchResult.Success([Session]));
        Directory.GetFiles(dir.Path).Should().ContainSingle().Which.Should().EndWith("usage.json");
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test --filter UsageStoreTests`
Expected: erreur de compilation.

- [ ] **Step 3 : Implémenter**

`src/UsageNotch.Core/Usage/UsageStore.cs` :
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace UsageNotch.Core.Usage;

/// <summary>
/// Compose le snapshot publié à partir des résultats d'appel : conserve la dernière bonne lecture,
/// la marque Stale en cas d'échec, persiste le tout (échéance de backoff comprise) et publie <see cref="Changed"/>.
/// </summary>
public sealed class UsageStore(string filePath, TimeProvider time, ILogger<UsageStore> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly object _gate = new();

    public UsageSnapshot Current { get; private set; } = UsageSnapshot.Empty;

    public event Action<UsageSnapshot>? Changed;

    public bool IsInBackoff => Current.BackoffUntil is { } until && until > time.GetUtcNow();

    /// <summary>Recharge la lecture persistée. Une vieille valeur vaut mieux qu'un anneau vide : elle est publiée en Stale.</summary>
    public void Load()
    {
        UsageSnapshot? saved;
        try
        {
            if (!File.Exists(filePath)) return;
            saved = JsonSerializer.Deserialize<UsageSnapshot>(File.ReadAllText(filePath), JsonOptions);
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Lecture de {Path} impossible, on repart de zéro", filePath);
            return;
        }
        if (saved is null) return;

        var restored = saved.Windows.Count > 0
            ? saved with { Status = SnapshotStatus.Stale }
            : saved with { Status = saved.Status == SnapshotStatus.Ok ? SnapshotStatus.Error : saved.Status };
        Publish(restored, persist: false);
    }

    public void Apply(FetchResult result, TimeSpan backoffWait = default)
    {
        var now = time.GetUtcNow();
        var prev = Current;
        var hasReading = prev.Windows.Count > 0;

        UsageSnapshot next = result switch
        {
            FetchResult.Success s => new UsageSnapshot(SnapshotStatus.Ok, s.Windows, now, "", null),
            FetchResult.NeedsAuth n => prev with { Status = SnapshotStatus.NeedsAuth, Note = n.Note, BackoffUntil = null },
            FetchResult.RateLimited => prev with
            {
                Status = hasReading ? SnapshotStatus.Stale : SnapshotStatus.Backoff,
                Note = $"Limite d'appels atteinte, nouvel essai dans {(int)backoffWait.TotalSeconds} s",
                BackoffUntil = now + backoffWait,
            },
            FetchResult.Failed f => prev with
            {
                Status = hasReading ? SnapshotStatus.Stale : SnapshotStatus.Error,
                Note = f.Note,
                BackoffUntil = null,
            },
            _ => prev,
        };

        Publish(next, persist: true);
    }

    /// <summary>« Rafraîchir maintenant » : l'échéance est oubliée pour que le prochain cycle appelle tout de suite.</summary>
    public void ClearBackoff()
    {
        if (Current.BackoffUntil is null) return;
        var next = Current with
        {
            BackoffUntil = null,
            Status = Current.Windows.Count > 0 ? SnapshotStatus.Stale : SnapshotStatus.Error,
        };
        Publish(next, persist: true);
    }

    private void Publish(UsageSnapshot next, bool persist)
    {
        lock (_gate)
        {
            Current = next;
            if (persist) Persist(next);
        }
        Changed?.Invoke(next);
    }

    private void Persist(UsageSnapshot snapshot)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var temp = filePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(snapshot, JsonOptions));
            File.Move(temp, filePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Écriture de {Path} impossible", filePath);
        }
    }
}
```

- [ ] **Step 4 : Lancer les tests**

Run: `dotnet test --filter UsageStoreTests`
Expected: 12 tests passés.

- [ ] **Step 5 : Commit**

```powershell
git add src/UsageNotch.Core/Usage/UsageStore.cs tests/UsageNotch.Core.Tests/Usage/UsageStoreTests.cs
git commit -m "feat(core): usage store with stale readings and persisted backoff"
```

---

### Task 7 : Planificateur d'usage

**Files:**
- Create: `src/UsageNotch.Core/Usage/ISessionActivity.cs`, `src/UsageNotch.Core/Usage/UsagePoller.cs`
- Test: `tests/UsageNotch.Core.Tests/Usage/UsagePollerTests.cs`

**Interfaces:**
- Consumes: `IUsageProvider`, `UsageStore`, `BackoffPolicy`.
- Produces:
  - `interface ISessionActivity { bool HasActiveSession { get; } }` — implémentée par `SessionStore` en Task 8.
  - `class UsagePoller(IUsageProvider provider, UsageStore store, ISessionActivity activity, TimeProvider time, ILogger<UsagePoller> logger) : BackgroundService` avec `void RequestRefresh()`, `TimeSpan NextInterval()`, `internal Task TickAsync(CancellationToken ct)`, `static readonly TimeSpan ActiveInterval` (60 s) et `IdleInterval` (5 min).

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Core.Tests/Usage/UsagePollerTests.cs` :
```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Tests.Usage;

public class UsagePollerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly LimitWindow Session = new("session", "Session en cours", 0.5, Now.AddHours(1));

    private sealed class FakeProvider : IUsageProvider
    {
        public Queue<FetchResult> Results { get; } = new();
        public int Calls { get; private set; }
        public string Id => "claude";
        public string DisplayName => "Claude";
        public string HeadlineWindowId => "session";
        public Task<FetchResult> FetchAsync(CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(Results.Count > 0 ? Results.Dequeue() : new FetchResult.Success([Session]));
        }
    }

    private sealed class FakeActivity : ISessionActivity
    {
        public bool HasActiveSession { get; set; }
    }

    private static (UsagePoller Poller, FakeProvider Provider, UsageStore Store, FakeActivity Activity, FakeTimeProvider Time) Build(TempDir dir)
    {
        var time = new FakeTimeProvider(Now);
        var provider = new FakeProvider();
        var store = new UsageStore(dir.File("usage.json"), time, NullLogger<UsageStore>.Instance);
        var activity = new FakeActivity();
        var poller = new UsagePoller(provider, store, activity, time, NullLogger<UsagePoller>.Instance);
        return (poller, provider, store, activity, time);
    }

    [Fact]
    public async Task A_tick_fetches_and_applies_a_success()
    {
        using var dir = new TempDir();
        var (poller, provider, store, _, _) = Build(dir);

        await poller.TickAsync(CancellationToken.None);

        provider.Calls.Should().Be(1);
        store.Current.Status.Should().Be(SnapshotStatus.Ok);
    }

    [Fact]
    public async Task No_call_is_made_during_a_backoff()
    {
        using var dir = new TempDir();
        var (poller, provider, store, _, time) = Build(dir);
        provider.Results.Enqueue(new FetchResult.RateLimited(TimeSpan.Zero));
        await poller.TickAsync(CancellationToken.None);
        store.Current.BackoffUntil.Should().Be(Now.AddSeconds(60));

        time.Advance(TimeSpan.FromSeconds(30));
        await poller.TickAsync(CancellationToken.None);

        provider.Calls.Should().Be(1);
    }

    [Fact]
    public async Task Consecutive_429s_double_the_wait_and_a_success_resets_it()
    {
        using var dir = new TempDir();
        var (poller, provider, store, _, time) = Build(dir);
        provider.Results.Enqueue(new FetchResult.RateLimited(TimeSpan.Zero));
        provider.Results.Enqueue(new FetchResult.RateLimited(TimeSpan.Zero));
        provider.Results.Enqueue(new FetchResult.Success([Session]));
        provider.Results.Enqueue(new FetchResult.RateLimited(TimeSpan.Zero));

        await poller.TickAsync(CancellationToken.None);
        store.Current.BackoffUntil.Should().Be(time.GetUtcNow().AddSeconds(60));

        time.Advance(TimeSpan.FromSeconds(61));
        await poller.TickAsync(CancellationToken.None);
        store.Current.BackoffUntil.Should().Be(time.GetUtcNow().AddSeconds(120));

        time.Advance(TimeSpan.FromSeconds(121));
        await poller.TickAsync(CancellationToken.None);
        store.Current.Status.Should().Be(SnapshotStatus.Ok);

        await poller.TickAsync(CancellationToken.None);
        store.Current.BackoffUntil.Should().Be(time.GetUtcNow().AddSeconds(60), "le compteur a été remis à zéro par le succès");
    }

    [Fact]
    public async Task RequestRefresh_clears_the_backoff_so_the_next_tick_calls()
    {
        using var dir = new TempDir();
        var (poller, provider, store, _, _) = Build(dir);
        provider.Results.Enqueue(new FetchResult.RateLimited(TimeSpan.Zero));
        await poller.TickAsync(CancellationToken.None);

        poller.RequestRefresh();
        store.IsInBackoff.Should().BeFalse();
        await poller.TickAsync(CancellationToken.None);

        provider.Calls.Should().Be(2);
    }

    [Fact]
    public async Task Retry_after_raises_the_backoff_floor()
    {
        using var dir = new TempDir();
        var (poller, provider, store, _, _) = Build(dir);
        provider.Results.Enqueue(new FetchResult.RateLimited(TimeSpan.FromSeconds(600)));
        await poller.TickAsync(CancellationToken.None);
        store.Current.BackoffUntil.Should().Be(Now.AddSeconds(600));
    }

    [Fact]
    public void Interval_is_60s_with_an_active_session_and_5min_otherwise()
    {
        using var dir = new TempDir();
        var (poller, _, _, activity, _) = Build(dir);
        activity.HasActiveSession = true;
        poller.NextInterval().Should().Be(TimeSpan.FromSeconds(60));
        activity.HasActiveSession = false;
        poller.NextInterval().Should().Be(TimeSpan.FromMinutes(5));
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test --filter UsagePollerTests`
Expected: erreur de compilation.

- [ ] **Step 3 : Implémenter**

`src/UsageNotch.Core/Usage/ISessionActivity.cs` :
```csharp
namespace UsageNotch.Core.Usage;

/// <summary>Ce que le planificateur a besoin de savoir des sessions : y en a-t-il une qui travaille ou attend ?</summary>
public interface ISessionActivity
{
    bool HasActiveSession { get; }
}
```

`src/UsageNotch.Core/Usage/UsagePoller.cs` :
```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UsageNotch.Core.Usage;

/// <summary>Appelle le fournisseur toutes les 60 s pendant une session active, toutes les 5 min sinon ; jamais pendant un backoff sauf rafraîchissement forcé.</summary>
public sealed class UsagePoller(
    IUsageProvider provider,
    UsageStore store,
    ISessionActivity activity,
    TimeProvider time,
    ILogger<UsagePoller> logger) : BackgroundService
{
    public static readonly TimeSpan ActiveInterval = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan IdleInterval = TimeSpan.FromMinutes(5);

    private readonly BackoffPolicy _backoff = new();
    private volatile bool _forced;
    private volatile CancellationTokenSource? _wake;

    /// <summary>« Rafraîchir maintenant » : efface le backoff et interrompt l'attente en cours.</summary>
    public void RequestRefresh()
    {
        _forced = true;
        store.ClearBackoff();
        _wake?.Cancel();
    }

    public TimeSpan NextInterval() => activity.HasActiveSession ? ActiveInterval : IdleInterval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        store.Load();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Échec du cycle d'usage, nouvel essai au prochain cycle");
            }
            await WaitAsync(NextInterval(), stoppingToken);
        }
    }

    internal async Task TickAsync(CancellationToken ct)
    {
        if (store.IsInBackoff && !_forced) return;
        _forced = false;

        var result = await provider.FetchAsync(ct);
        switch (result)
        {
            case FetchResult.Success:
                _backoff.Reset();
                store.Apply(result);
                break;
            case FetchResult.RateLimited limited:
                store.Apply(result, _backoff.Next(limited.RetryAfter));
                break;
            default:
                store.Apply(result);
                break;
        }
    }

    private async Task WaitAsync(TimeSpan delay, CancellationToken ct)
    {
        if (_forced) return;

        // Pendant un backoff, se réveiller dès son échéance plutôt qu'au prochain intervalle.
        if (store.Current.BackoffUntil is { } until)
        {
            var remaining = until - time.GetUtcNow() + TimeSpan.FromSeconds(1);
            if (remaining > TimeSpan.Zero && remaining < delay) delay = remaining;
        }

        using var wake = new CancellationTokenSource();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, wake.Token);
        _wake = wake;
        try
        {
            await Task.Delay(delay, time, linked.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Réveil par RequestRefresh : on repart tout de suite.
        }
        finally
        {
            _wake = null;
        }
    }
}
```

- [ ] **Step 4 : Lancer les tests**

Run: `dotnet test --filter UsagePollerTests`
Expected: 6 tests passés.

- [ ] **Step 5 : Commit**

```powershell
git add src/UsageNotch.Core/Usage/ISessionActivity.cs src/UsageNotch.Core/Usage/UsagePoller.cs tests/UsageNotch.Core.Tests/Usage/UsagePollerTests.cs
git commit -m "feat(core): usage poller with active/idle cadence and forced refresh"
```

---

### Task 8 : Sessions — modèles, machine à états, balayage

**Files:**
- Create: `src/UsageNotch.Core/Sessions/SessionState.cs`, `HookEvent.cs`, `Session.cs`, `SessionStore.cs`, `SessionSweeper.cs`
- Test: `tests/UsageNotch.Core.Tests/Sessions/SessionStoreTests.cs`

**Interfaces:**
- Consumes: `ISessionActivity` (Task 7).
- Produces:
  - `enum SessionState { Idle, Done, Running, Attention }` (ordre = coût d'attention croissant).
  - `record HookEvent(string Kind, string SessionId, int ParentPid, string Cwd, string Prompt, string Message, string ToolName, string ToolCommand, string Model)` avec les constantes `HookEvent.SessionStart = "session_start"`, `Running = "running"`, `Attention = "attention"`, `Done = "done"`, `SessionEnd = "session_end"`.
  - `record Session(string Id, string Title, SessionState State, DateTimeOffset Started, TimeSpan Total, string LastAction, string AttentionMessage, string Prompt, string Model, int ParentPid, string Cwd, DateTimeOffset LastEvent)`.
  - `class SessionStore(TimeProvider time) : ISessionActivity` avec `event Action? Changed`, `bool Apply(HookEvent ev)`, `bool Dismiss(string id)`, `bool Sweep()`, `IReadOnlyList<Session> Snapshot()`, `SessionState Aggregate`, `int? ParentPidOf(string id)`.
  - `class SessionSweeper(SessionStore store, TimeProvider time) : BackgroundService` — appelle `Sweep()` toutes les 30 s.

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Core.Tests/Sessions/SessionStoreTests.cs` :
```csharp
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Sessions;

namespace UsageNotch.Core.Tests.Sessions;

public class SessionStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private static HookEvent Ev(string kind, string id = "abcd1234-session", string cwd = @"C:\src\myproj",
        string prompt = "", string message = "", string tool = "", string cmd = "", string model = "", int ppid = 0) =>
        new(kind, id, ppid, cwd, prompt, message, tool, cmd, model);

    private static (SessionStore Store, FakeTimeProvider Time) Build()
    {
        var time = new FakeTimeProvider(Now);
        return (new SessionStore(time), time);
    }

    [Fact]
    public void Session_start_creates_an_idle_session_with_a_title_from_cwd()
    {
        var (store, _) = Build();
        store.Apply(Ev(HookEvent.SessionStart)).Should().BeTrue();
        var s = store.Snapshot().Single();
        s.State.Should().Be(SessionState.Idle);
        s.Title.Should().Be("myproj · abcd");
        store.HasActiveSession.Should().BeFalse();
    }

    [Fact]
    public void Running_records_prompt_and_start_time()
    {
        var (store, _) = Build();
        store.Apply(Ev(HookEvent.Running, prompt: "corrige le bug"));
        var s = store.Snapshot().Single();
        s.State.Should().Be(SessionState.Running);
        s.Prompt.Should().Be("corrige le bug");
        s.Started.Should().Be(Now);
        store.HasActiveSession.Should().BeTrue();
    }

    [Fact]
    public void Running_with_a_tool_records_the_last_action()
    {
        var (store, _) = Build();
        store.Apply(Ev(HookEvent.Running, tool: "Bash", cmd: "dotnet test"));
        store.Snapshot().Single().LastAction.Should().Be("🔧 Bash : dotnet test");
        store.Apply(Ev(HookEvent.Running, tool: "Read"));
        store.Snapshot().Single().LastAction.Should().Be("🔧 Read");
    }

    [Fact]
    public void Long_texts_are_truncated_with_an_ellipsis()
    {
        var (store, _) = Build();
        store.Apply(Ev(HookEvent.Running, prompt: new string('a', 200), tool: "Bash", cmd: new string('b', 100)));
        var s = store.Snapshot().Single();
        s.Prompt.Should().HaveLength(121).And.EndWith("…");
        s.LastAction.Should().EndWith("…");
        s.LastAction.Length.Should().Be("🔧 Bash : ".Length + 61);
    }

    [Fact]
    public void Attention_records_the_message_and_outranks_running()
    {
        var (store, _) = Build();
        store.Apply(Ev(HookEvent.Running, id: "one"));
        store.Apply(Ev(HookEvent.Attention, id: "two", message: "Autoriser Bash ?"));
        store.Snapshot()[0].Id.Should().Be("two");
        store.Snapshot()[0].AttentionMessage.Should().Be("Autoriser Bash ?");
        store.Aggregate.Should().Be(SessionState.Attention);
    }

    [Fact]
    public void Running_after_attention_clears_the_message_and_restarts_the_clock()
    {
        var (store, time) = Build();
        store.Apply(Ev(HookEvent.Running));
        time.Advance(TimeSpan.FromMinutes(1));
        store.Apply(Ev(HookEvent.Attention, message: "?"));
        time.Advance(TimeSpan.FromMinutes(1));
        store.Apply(Ev(HookEvent.Running));
        var s = store.Snapshot().Single();
        s.AttentionMessage.Should().BeEmpty();
        s.Started.Should().Be(Now.AddMinutes(2), "attention → running est un nouveau tour");
    }

    [Fact]
    public void Done_freezes_the_total_and_persists_until_the_next_prompt()
    {
        var (store, time) = Build();
        store.Apply(Ev(HookEvent.Running));
        time.Advance(TimeSpan.FromMinutes(3));
        store.Apply(Ev(HookEvent.Done));
        var s = store.Snapshot().Single();
        s.State.Should().Be(SessionState.Done);
        s.Total.Should().Be(TimeSpan.FromMinutes(3));
        store.HasActiveSession.Should().BeFalse();

        time.Advance(TimeSpan.FromMinutes(20));
        store.Sweep();
        store.Snapshot().Should().ContainSingle(x => x.State == SessionState.Done);

        store.Apply(Ev(HookEvent.Running));
        store.Snapshot().Single().State.Should().Be(SessionState.Running);
    }

    [Fact]
    public void Session_end_removes_the_session()
    {
        var (store, _) = Build();
        store.Apply(Ev(HookEvent.Running));
        store.Apply(Ev(HookEvent.SessionEnd)).Should().BeTrue();
        store.Snapshot().Should().BeEmpty();
        store.Apply(Ev(HookEvent.SessionEnd)).Should().BeFalse("déjà retirée");
    }

    [Fact]
    public void Dismiss_removes_a_session()
    {
        var (store, _) = Build();
        store.Apply(Ev(HookEvent.Done));
        store.Dismiss("abcd1234-session").Should().BeTrue();
        store.Snapshot().Should().BeEmpty();
    }

    [Fact]
    public void Changed_is_raised_only_on_a_visible_change()
    {
        var (store, _) = Build();
        var raised = 0;
        store.Changed += () => raised++;

        store.Apply(Ev(HookEvent.Running, tool: "Bash", cmd: "ls")).Should().BeTrue();
        store.Apply(Ev(HookEvent.Running, tool: "Bash", cmd: "ls")).Should().BeFalse();
        raised.Should().Be(1);
    }

    [Fact]
    public void Ppid_model_and_cwd_are_remembered_when_provided()
    {
        var (store, _) = Build();
        store.Apply(Ev(HookEvent.SessionStart, cwd: "", ppid: 4242));
        store.Apply(Ev(HookEvent.Running, cwd: @"D:\work\api", model: "claude-opus-5", ppid: 0));
        var s = store.Snapshot().Single();
        s.ParentPid.Should().Be(4242);
        s.Model.Should().Be("claude-opus-5");
        s.Title.Should().Be("api · abcd");
        store.ParentPidOf("abcd1234-session").Should().Be(4242);
        store.ParentPidOf("missing").Should().BeNull();
    }

    [Fact]
    public void Sweep_turns_a_silent_running_session_idle_after_30_minutes()
    {
        var (store, time) = Build();
        store.Apply(Ev(HookEvent.Running));
        time.Advance(TimeSpan.FromMinutes(31));
        store.Sweep().Should().BeTrue();
        store.Snapshot().Single().State.Should().Be(SessionState.Idle);
    }

    [Fact]
    public void Sweep_drops_idle_after_10_minutes_and_done_after_24_hours()
    {
        var (store, time) = Build();
        store.Apply(Ev(HookEvent.SessionStart, id: "idle"));
        store.Apply(Ev(HookEvent.Done, id: "done"));

        time.Advance(TimeSpan.FromMinutes(11));
        store.Sweep().Should().BeTrue();
        store.Snapshot().Select(s => s.Id).Should().Equal("done");

        time.Advance(TimeSpan.FromHours(24));
        store.Sweep().Should().BeTrue();
        store.Snapshot().Should().BeEmpty();

        store.Sweep().Should().BeFalse("rien n'a changé");
    }

    [Fact]
    public void Snapshot_orders_by_state_then_most_recent_start()
    {
        var (store, time) = Build();
        store.Apply(Ev(HookEvent.Running, id: "r-old"));
        time.Advance(TimeSpan.FromSeconds(1));
        store.Apply(Ev(HookEvent.Done, id: "d"));
        time.Advance(TimeSpan.FromSeconds(1));
        store.Apply(Ev(HookEvent.Running, id: "r-new"));
        time.Advance(TimeSpan.FromSeconds(1));
        store.Apply(Ev(HookEvent.Attention, id: "a"));

        store.Snapshot().Select(s => s.Id).Should().Equal("a", "r-new", "r-old", "d");
        store.Aggregate.Should().Be(SessionState.Attention);
    }

    [Fact]
    public void Aggregate_is_idle_when_there_is_nothing()
    {
        var (store, _) = Build();
        store.Aggregate.Should().Be(SessionState.Idle);
    }

    [Fact]
    public void Unknown_session_id_defaults_to_unknown_title()
    {
        var (store, _) = Build();
        store.Apply(Ev(HookEvent.Running, id: "unknown", cwd: ""));
        store.Snapshot().Single().Title.Should().Be("claude · unkn");
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test --filter SessionStoreTests`
Expected: erreur de compilation.

- [ ] **Step 3 : Implémenter les modèles**

`src/UsageNotch.Core/Sessions/SessionState.cs` :
```csharp
namespace UsageNotch.Core.Sessions;

/// <summary>Ordonné par coût d'attention croissant : l'agrégat est le maximum.</summary>
public enum SessionState
{
    Idle,
    Done,
    Running,
    Attention,
}
```

`src/UsageNotch.Core/Sessions/HookEvent.cs` :
```csharp
namespace UsageNotch.Core.Sessions;

/// <summary>Un événement relayé par UsageNotch.Hook. Tous les champs texte sont vides plutôt que null quand ils manquent.</summary>
public sealed record HookEvent(
    string Kind,
    string SessionId,
    int ParentPid,
    string Cwd,
    string Prompt,
    string Message,
    string ToolName,
    string ToolCommand,
    string Model)
{
    public const string SessionStart = "session_start";
    public const string Running = "running";
    public const string Attention = "attention";
    public const string Done = "done";
    public const string SessionEnd = "session_end";
}
```

`src/UsageNotch.Core/Sessions/Session.cs` :
```csharp
namespace UsageNotch.Core.Sessions;

public sealed record Session(
    string Id,
    string Title,
    SessionState State,
    DateTimeOffset Started,
    TimeSpan Total,
    string LastAction,
    string AttentionMessage,
    string Prompt,
    string Model,
    int ParentPid,
    string Cwd,
    DateTimeOffset LastEvent);
```

- [ ] **Step 4 : Implémenter le magasin et le balayeur**

`src/UsageNotch.Core/Sessions/SessionStore.cs` :
```csharp
using UsageNotch.Core.Usage;

namespace UsageNotch.Core.Sessions;

/// <summary>
/// Machine à quatre états par session. Done persiste jusqu'au prochain prompt, à un rejet manuel ou au balayage.
/// <see cref="Changed"/> n'est levé que si quelque chose de visible a changé.
/// </summary>
public sealed class SessionStore(TimeProvider time) : ISessionActivity
{
    public static readonly TimeSpan RunningStale = TimeSpan.FromMinutes(30);
    public static readonly TimeSpan IdleDrop = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan DoneStale = TimeSpan.FromHours(24);

    private const int PromptMax = 120;
    private const int CommandMax = 60;
    private const int MessageMax = 200;

    private readonly object _gate = new();
    private readonly Dictionary<string, Session> _sessions = new(StringComparer.Ordinal);

    public event Action? Changed;

    public bool HasActiveSession
    {
        get
        {
            lock (_gate) return _sessions.Values.Any(s => s.State is SessionState.Running or SessionState.Attention);
        }
    }

    public SessionState Aggregate
    {
        get
        {
            lock (_gate) return _sessions.Count == 0 ? SessionState.Idle : _sessions.Values.Max(s => s.State);
        }
    }

    public IReadOnlyList<Session> Snapshot()
    {
        lock (_gate)
        {
            return _sessions.Values
                .OrderByDescending(s => s.State)
                .ThenByDescending(s => s.Started)
                .ToList();
        }
    }

    public int? ParentPidOf(string id)
    {
        lock (_gate) return _sessions.TryGetValue(id, out var s) && s.ParentPid != 0 ? s.ParentPid : null;
    }

    public bool Apply(HookEvent ev)
    {
        bool changed;
        lock (_gate) changed = ApplyLocked(ev);
        if (changed) Changed?.Invoke();
        return changed;
    }

    public bool Dismiss(string id)
    {
        bool removed;
        lock (_gate) removed = _sessions.Remove(id);
        if (removed) Changed?.Invoke();
        return removed;
    }

    public bool Sweep()
    {
        var now = time.GetUtcNow();
        var changed = false;
        lock (_gate)
        {
            foreach (var (id, s) in _sessions.ToList())
            {
                if (s.State == SessionState.Running && now - s.LastEvent > RunningStale)
                {
                    _sessions[id] = s with { State = SessionState.Idle };
                    changed = true;
                }
            }
            var before = _sessions.Count;
            foreach (var (id, s) in _sessions.ToList())
            {
                var drop = (s.State == SessionState.Idle && now - s.LastEvent > IdleDrop)
                        || (s.State == SessionState.Done && now - s.LastEvent > DoneStale);
                if (drop) _sessions.Remove(id);
            }
            changed |= _sessions.Count != before;
        }
        if (changed) Changed?.Invoke();
        return changed;
    }

    private bool ApplyLocked(HookEvent ev)
    {
        var now = time.GetUtcNow();
        if (ev.Kind == HookEvent.SessionEnd)
        {
            return _sessions.Remove(ev.SessionId);
        }

        var isNew = !_sessions.TryGetValue(ev.SessionId, out var s);
        s ??= new Session(ev.SessionId, TitleOf(ev.Cwd, ev.SessionId), SessionState.Idle, now, TimeSpan.Zero,
            "", "", "", "", 0, ev.Cwd, now);

        var before = (s.State, s.LastAction, s.AttentionMessage, s.Prompt, s.Model);

        s = s with { LastEvent = now };
        if (ev.ParentPid != 0) s = s with { ParentPid = ev.ParentPid };
        if (ev.Model.Length > 0) s = s with { Model = ev.Model };
        if (ev.Cwd.Length > 0 && s.Cwd.Length == 0) s = s with { Cwd = ev.Cwd, Title = TitleOf(ev.Cwd, s.Id) };

        switch (ev.Kind)
        {
            case HookEvent.SessionStart:
                if (s.State != SessionState.Running) s = s with { State = SessionState.Idle };
                break;
            case HookEvent.Running:
                if (s.State != SessionState.Running) s = s with { Started = now };
                s = s with { State = SessionState.Running, AttentionMessage = "" };
                if (ev.Prompt.Length > 0) s = s with { Prompt = Truncate(ev.Prompt, PromptMax) };
                if (ev.ToolName.Length > 0)
                {
                    s = s with
                    {
                        LastAction = ev.ToolCommand.Length == 0
                            ? $"🔧 {ev.ToolName}"
                            : $"🔧 {ev.ToolName} : {Truncate(ev.ToolCommand, CommandMax)}",
                    };
                }
                break;
            case HookEvent.Attention:
                s = s with { State = SessionState.Attention };
                if (ev.Message.Length > 0) s = s with { AttentionMessage = Truncate(ev.Message, MessageMax) };
                break;
            case HookEvent.Done:
                if (s.State != SessionState.Done) s = s with { Total = now - s.Started };
                s = s with { State = SessionState.Done, AttentionMessage = "" };
                break;
            default:
                break;
        }

        _sessions[ev.SessionId] = s;
        return isNew || before != (s.State, s.LastAction, s.AttentionMessage, s.Prompt, s.Model);
    }

    internal static string TitleOf(string cwd, string id)
    {
        var last = cwd.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? "claude";
        var shortId = id.Length <= 4 ? id : id[..4];
        return $"{last} · {shortId}";
    }

    internal static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
```

`src/UsageNotch.Core/Sessions/SessionSweeper.cs` :
```csharp
using Microsoft.Extensions.Hosting;

namespace UsageNotch.Core.Sessions;

/// <summary>Balayage des sessions obsolètes toutes les 30 s.</summary>
public sealed class SessionSweeper(SessionStore store, TimeProvider time) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Interval, time, stoppingToken);
            store.Sweep();
        }
    }
}
```

- [ ] **Step 5 : Lancer les tests**

Run: `dotnet test --filter SessionStoreTests`
Expected: 16 tests passés.

- [ ] **Step 6 : Commit**

```powershell
git add src/UsageNotch.Core/Sessions tests/UsageNotch.Core.Tests/Sessions
git commit -m "feat(core): session state machine with sweep"
```

---

### Task 9 : Filtrage des requêtes et parseur d'événements

**Files:**
- Create: `src/UsageNotch.Core/Hooks/HookRequestGuard.cs`, `src/UsageNotch.Core/Hooks/HookEventParser.cs`
- Test: `tests/UsageNotch.Core.Tests/Hooks/HookRequestGuardTests.cs`, `tests/UsageNotch.Core.Tests/Hooks/HookEventParserTests.cs`

**Interfaces:**
- Consumes: `HookEvent` (Task 8).
- Produces:
  - `static bool HookRequestGuard.IsAllowedOrigin(string origin)`
  - `static bool HookRequestGuard.IsForbidden(IEnumerable<KeyValuePair<string, string>> headers)` — vrai si la requête doit être rejetée.
  - `static HookEvent HookEventParser.Parse(string kind, int parentPid, string body)` — parsing tolérant, jamais d'exception.

- [ ] **Step 1 : Écrire les tests du filtre**

`tests/UsageNotch.Core.Tests/Hooks/HookRequestGuardTests.cs` :
```csharp
using FluentAssertions;
using UsageNotch.Core.Hooks;

namespace UsageNotch.Core.Tests.Hooks;

public class HookRequestGuardTests
{
    [Theory]
    [InlineData("http://127.0.0.1:48666")]
    [InlineData("http://localhost:5173")]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://localhost")]
    [InlineData("https://127.0.0.1:48666")]
    [InlineData("https://localhost:3000")]
    [InlineData("127.0.0.1:48666")]
    [InlineData("localhost")]
    [InlineData("http://[::1]:8080")]
    [InlineData("http://[::1]")]
    [InlineData("http://LOCALHOST:3000")]
    public void Loopback_origins_are_allowed(string origin) =>
        HookRequestGuard.IsAllowedOrigin(origin).Should().BeTrue();

    [Theory]
    [InlineData("null")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://evil.com")]
    [InlineData("http://attacker.com:8080")]
    [InlineData("https://evil-localhost.com")]
    [InlineData("https://localhost.attacker.com")]
    [InlineData("http://127.0.0.1.attacker.com")]
    [InlineData("http://attacker.com:127.0.0.1")]
    [InlineData("http://localhost@attacker.com")]
    [InlineData("http://attacker.com/localhost")]
    [InlineData("http://attacker.com?localhost")]
    [InlineData("http://attacker.com#localhost")]
    [InlineData("file:///etc/passwd")]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://localhost:abc")]
    [InlineData("http://localhost:70000")]
    [InlineData("http://localhost:0")]
    [InlineData("http://localhost:")]
    public void Untrusted_origins_are_rejected(string origin) =>
        HookRequestGuard.IsAllowedOrigin(origin).Should().BeFalse();

    private static KeyValuePair<string, string> H(string k, string v) => new(k, v);

    [Fact]
    public void A_native_hook_request_without_origin_is_allowed() =>
        HookRequestGuard.IsForbidden([H("Host", "127.0.0.1:48666"), H("Content-Type", "application/json")]).Should().BeFalse();

    [Fact]
    public void An_untrusted_origin_is_forbidden() =>
        HookRequestGuard.IsForbidden([H("Origin", "https://evil.com")]).Should().BeTrue();

    [Fact]
    public void Two_origin_headers_are_forbidden() =>
        HookRequestGuard.IsForbidden([H("Origin", "http://localhost:3000"), H("Origin", "http://127.0.0.1:48666")]).Should().BeTrue();

    [Fact]
    public void A_trusted_origin_is_allowed() =>
        HookRequestGuard.IsForbidden([H("origin", "http://localhost:3000")]).Should().BeFalse();

    [Fact]
    public void Cross_site_fetch_metadata_is_forbidden_even_with_a_local_origin() =>
        HookRequestGuard.IsForbidden([H("Origin", "http://localhost:3000"), H("Sec-Fetch-Site", "cross-site")]).Should().BeTrue();

    [Fact]
    public void Same_origin_fetch_metadata_is_allowed() =>
        HookRequestGuard.IsForbidden([H("Sec-Fetch-Site", "same-origin")]).Should().BeFalse();
}
```

- [ ] **Step 2 : Écrire les tests du parseur**

`tests/UsageNotch.Core.Tests/Hooks/HookEventParserTests.cs` :
```csharp
using FluentAssertions;
using UsageNotch.Core.Hooks;
using UsageNotch.Core.Sessions;

namespace UsageNotch.Core.Tests.Hooks;

public class HookEventParserTests
{
    [Fact]
    public void Reads_every_field_claude_code_provides()
    {
        var body = """
        { "session_id": "s-1", "cwd": "C:\\src\\proj", "prompt": "fais X", "message": "Autoriser ?",
          "tool_name": "Bash", "tool_input": { "command": "dotnet build" }, "model": "claude-opus-5" }
        """;

        var ev = HookEventParser.Parse(HookEvent.Running, 1234, body);

        ev.Should().Be(new HookEvent(HookEvent.Running, "s-1", 1234, @"C:\src\proj", "fais X", "Autoriser ?", "Bash", "dotnet build", "claude-opus-5"));
    }

    [Fact]
    public void Missing_fields_become_empty_strings_and_unknown_session_id()
    {
        var ev = HookEventParser.Parse(HookEvent.Done, 0, "{}");
        ev.SessionId.Should().Be("unknown");
        ev.Cwd.Should().BeEmpty();
        ev.ToolCommand.Should().BeEmpty();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    public void Unreadable_bodies_do_not_throw(string body)
    {
        var ev = HookEventParser.Parse(HookEvent.Attention, 7, body);
        ev.Kind.Should().Be(HookEvent.Attention);
        ev.ParentPid.Should().Be(7);
        ev.SessionId.Should().Be("unknown");
    }

    [Fact]
    public void Tool_input_without_command_gives_an_empty_command()
    {
        var ev = HookEventParser.Parse(HookEvent.Running, 0, """{ "tool_name": "Read", "tool_input": { "file_path": "x" } }""");
        ev.ToolName.Should().Be("Read");
        ev.ToolCommand.Should().BeEmpty();
    }
}
```

- [ ] **Step 3 : Vérifier l'échec**

Run: `dotnet test --filter "HookRequestGuardTests|HookEventParserTests"`
Expected: erreur de compilation.

- [ ] **Step 4 : Implémenter le filtre**

`src/UsageNotch.Core/Hooks/HookRequestGuard.cs` :
```csharp
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
```

- [ ] **Step 5 : Implémenter le parseur**

`src/UsageNotch.Core/Hooks/HookEventParser.cs` :
```csharp
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
```

- [ ] **Step 6 : Lancer les tests**

Run: `dotnet test --filter "HookRequestGuardTests|HookEventParserTests"`
Expected: 42 tests passés (11 + 19 cas de Theory, 6 Facts du filtre, 6 du parseur).

- [ ] **Step 7 : Commit**

```powershell
git add src/UsageNotch.Core/Hooks/HookRequestGuard.cs src/UsageNotch.Core/Hooks/HookEventParser.cs tests/UsageNotch.Core.Tests/Hooks
git commit -m "feat(core): hook request guard and event parser"
```

---

### Task 10 : Récepteur HTTP des hooks

**Files:**
- Create: `src/UsageNotch.Core/Hooks/HookListener.cs`
- Test: `tests/UsageNotch.Core.Tests/Hooks/HookListenerTests.cs`

**Interfaces:**
- Consumes: `SessionStore.Apply(HookEvent)`, `HookRequestGuard.IsForbidden`, `HookEventParser.Parse`.
- Produces: `class HookListener(int port, SessionStore sessions, ILogger<HookListener> logger) : BackgroundService` avec `int Port`, `event Action? OpenSettingsRequested`, `const int DefaultPort = 48666`, `const int MaxBodyBytes = 262144`. Routes : `POST /event?e=<kind>&ppid=<pid>` et `POST /open-settings`.

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Core.Tests/Hooks/HookListenerTests.cs` :
```csharp
using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Hooks;
using UsageNotch.Core.Sessions;

namespace UsageNotch.Core.Tests.Hooks;

public class HookListenerTests : IAsyncLifetime
{
    private readonly SessionStore _sessions = new(new FakeTimeProvider(new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero)));
    private HookListener _listener = null!;
    private HttpClient _client = null!;

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    public async Task InitializeAsync()
    {
        var port = FreePort();
        _listener = new HookListener(port, _sessions, NullLogger<HookListener>.Instance);
        await _listener.StartAsync(CancellationToken.None);
        _client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}/") };
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _listener.StopAsync(CancellationToken.None);
    }

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    [Fact]
    public async Task A_hook_post_creates_a_session()
    {
        var response = await _client.PostAsync("event?e=running&ppid=4242",
            Json("""{ "session_id": "s-1", "cwd": "C:\\src\\app", "prompt": "hello" }"""));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("ok");
        var s = _sessions.Snapshot().Single();
        s.Id.Should().Be("s-1");
        s.State.Should().Be(SessionState.Running);
        s.ParentPid.Should().Be(4242);
        s.Prompt.Should().Be("hello");
    }

    [Fact]
    public async Task A_cross_site_browser_request_is_refused()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "event?e=running") { Content = Json("{}") };
        request.Headers.TryAddWithoutValidation("Origin", "https://evil.com");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        _sessions.Snapshot().Should().BeEmpty();
    }

    [Fact]
    public async Task Sec_fetch_site_cross_site_is_refused()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "event?e=running") { Content = Json("{}") };
        request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "cross-site");

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Open_settings_raises_the_event()
    {
        var raised = 0;
        _listener.OpenSettingsRequested += () => raised++;

        var response = await _client.PostAsync("open-settings", Json(""));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        raised.Should().Be(1);
    }

    [Fact]
    public async Task Unknown_routes_are_404()
    {
        var response = await _client.GetAsync("nothing");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task An_unreadable_body_is_still_accepted()
    {
        var response = await _client.PostAsync("event?e=done", new StringContent("not json"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _sessions.Snapshot().Single().Id.Should().Be("unknown");
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test --filter HookListenerTests`
Expected: erreur de compilation.

- [ ] **Step 3 : Implémenter**

`src/UsageNotch.Core/Hooks/HookListener.cs` :
```csharp
using System.Net;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UsageNotch.Core.Sessions;

namespace UsageNotch.Core.Hooks;

/// <summary>
/// Écoute sur 127.0.0.1 uniquement. <c>POST /event?e=&lt;kind&gt;&amp;ppid=&lt;pid&gt;</c> reçoit le JSON du hook ;
/// <c>POST /open-settings</c> est le signal d'une seconde instance. HttpListener se lie à la boucle locale sans droits administrateur.
/// </summary>
public sealed class HookListener(int port, SessionStore sessions, ILogger<HookListener> logger) : BackgroundService
{
    public const int DefaultPort = 48666;
    public const int MaxBodyBytes = 256 * 1024;

    private readonly HttpListener _listener = new();

    public int Port { get; } = port;

    public event Action? OpenSettingsRequested;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
        try
        {
            _listener.Start();
        }
        catch (HttpListenerException e)
        {
            logger.LogError(e, "Port {Port} indisponible — une autre instance tourne peut-être", Port);
            return;
        }

        using var stop = stoppingToken.Register(() => _listener.Stop());
        logger.LogInformation("Récepteur de hooks à l'écoute sur 127.0.0.1:{Port}", Port);

        while (!stoppingToken.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (HttpListenerException e)
            {
                logger.LogWarning(e, "Requête entrante rejetée par HttpListener");
                continue;
            }
            _ = HandleSafelyAsync(context);
        }
    }

    private async Task HandleSafelyAsync(HttpListenerContext context)
    {
        try
        {
            await HandleAsync(context);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Traitement d'une requête de hook échoué");
            try { context.Response.Abort(); } catch (ObjectDisposedException) { }
        }
    }

    internal async Task HandleAsync(HttpListenerContext context)
    {
        var request = context.Request;
        var path = request.Url?.AbsolutePath ?? "/";

        if (path is not ("/event" or "/open-settings"))
        {
            await RespondAsync(context.Response, 404, "not found");
            return;
        }

        var headers = request.Headers.AllKeys
            .Where(k => k is not null)
            .SelectMany(k => (request.Headers.GetValues(k!) ?? []).Select(v => new KeyValuePair<string, string>(k!, v)));
        if (HookRequestGuard.IsForbidden(headers))
        {
            logger.LogWarning("Requête refusée sur {Path} (origine non locale)", path);
            await RespondAsync(context.Response, 403, "forbidden");
            return;
        }

        if (path == "/open-settings")
        {
            OpenSettingsRequested?.Invoke();
            await RespondAsync(context.Response, 200, "ok");
            return;
        }

        var kind = request.QueryString["e"] ?? "";
        var ppid = int.TryParse(request.QueryString["ppid"], out var parsed) ? parsed : 0;
        var body = await ReadBodyAsync(request);
        var ev = HookEventParser.Parse(kind, ppid, body);
        logger.LogDebug("Hook {Kind} pour la session {Session}", ev.Kind, ev.SessionId.Length > 8 ? ev.SessionId[..8] : ev.SessionId);
        sessions.Apply(ev);
        await RespondAsync(context.Response, 200, "ok");
    }

    private static async Task<string> ReadBodyAsync(HttpListenerRequest request)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        long total = 0;
        int read;
        while ((read = await request.InputStream.ReadAsync(chunk)) > 0)
        {
            total += read;
            if (total > MaxBodyBytes) break;
            buffer.Write(chunk, 0, read);
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static async Task RespondAsync(HttpListenerResponse response, int status, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        response.StatusCode = status;
        response.ContentType = "text/plain; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    public override void Dispose()
    {
        _listener.Close();
        base.Dispose();
    }
}
```

- [ ] **Step 4 : Lancer les tests**

Run: `dotnet test --filter HookListenerTests`
Expected: 6 tests passés.

- [ ] **Step 5 : Commit**

```powershell
git add src/UsageNotch.Core/Hooks/HookListener.cs tests/UsageNotch.Core.Tests/Hooks/HookListenerTests.cs
git commit -m "feat(core): local HTTP listener for Claude Code hook events"
```

---

### Task 11 : Installeur des hooks dans settings.json

**Files:**
- Create: `src/UsageNotch.Core/Hooks/HookInstaller.cs`
- Test: `tests/UsageNotch.Core.Tests/Hooks/HookInstallerTests.cs`

**Interfaces:**
- Produces: `class HookInstaller(string settingsPath, string hookExePath, TimeProvider time)` avec `bool IsInstalled()`, `string Install()` (lève `FileNotFoundException` si l'exécutable hook manque), `string Uninstall()`, `static string DefaultSettingsPath`, `const string Marker = "UsageNotch.Hook"`, `static readonly (string Event, bool NeedsMatcher, string Argument)[] Wiring`.

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Core.Tests/Hooks/HookInstallerTests.cs` :
```csharp
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.Extensions.Time.Testing;
using UsageNotch.Core.Hooks;

namespace UsageNotch.Core.Tests.Hooks;

public class HookInstallerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);

    private static (HookInstaller Installer, string SettingsPath, string HookExe) Build(TempDir dir)
    {
        var hookExe = dir.File("UsageNotch.Hook.exe");
        File.WriteAllText(hookExe, "stub");
        var settings = Path.Combine(dir.Path, ".claude", "settings.json");
        return (new HookInstaller(settings, hookExe, new FakeTimeProvider(Now)), settings, hookExe);
    }

    private static JsonObject Load(string path) => (JsonObject)JsonNode.Parse(File.ReadAllText(path))!;

    [Fact]
    public void Install_creates_settings_with_the_seven_events()
    {
        using var dir = new TempDir();
        var (installer, settingsPath, hookExe) = Build(dir);

        installer.Install();

        var hooks = (JsonObject)Load(settingsPath)["hooks"]!;
        hooks.Select(kv => kv.Key).Should().BeEquivalentTo(
            "SessionStart", "UserPromptSubmit", "PreToolUse", "PostToolUse", "Notification", "Stop", "SessionEnd");

        var stop = (JsonObject)((JsonArray)hooks["Stop"]!)[0]!;
        stop["matcher"].Should().BeNull();
        var cmd = (JsonObject)((JsonArray)stop["hooks"]!)[0]!;
        cmd["type"]!.GetValue<string>().Should().Be("command");
        cmd["command"]!.GetValue<string>().Should().Be($"\"{hookExe}\" done");
        cmd["timeout"]!.GetValue<int>().Should().Be(5);

        var pre = (JsonObject)((JsonArray)hooks["PreToolUse"]!)[0]!;
        pre["matcher"]!.GetValue<string>().Should().Be("*");
        ((JsonObject)((JsonArray)pre["hooks"]!)[0]!)["command"]!.GetValue<string>().Should().EndWith(" running");

        installer.IsInstalled().Should().BeTrue();
    }

    [Fact]
    public void Install_preserves_third_party_hooks_and_other_settings()
    {
        using var dir = new TempDir();
        var (installer, settingsPath, _) = Build(dir);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, """
        { "model": "opus",
          "hooks": { "Stop": [ { "hooks": [ { "type": "command", "command": "other-tool notify" } ] } ] } }
        """);

        installer.Install();

        var root = Load(settingsPath);
        root["model"]!.GetValue<string>().Should().Be("opus");
        var stop = (JsonArray)root["hooks"]!["Stop"]!;
        stop.Should().HaveCount(2);
        ((JsonObject)((JsonArray)((JsonObject)stop[0]!)["hooks"]!)[0]!)["command"]!.GetValue<string>().Should().Be("other-tool notify");
    }

    [Fact]
    public void Installing_twice_replaces_our_entries_instead_of_duplicating_them()
    {
        using var dir = new TempDir();
        var (installer, settingsPath, _) = Build(dir);

        installer.Install();
        installer.Install();

        var stop = (JsonArray)Load(settingsPath)["hooks"]!["Stop"]!;
        stop.Should().HaveCount(1);
    }

    [Fact]
    public void Install_writes_a_timestamped_backup_when_a_file_existed()
    {
        using var dir = new TempDir();
        var (installer, settingsPath, _) = Build(dir);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, "{ \"model\": \"opus\" }");

        installer.Install();

        var backup = Path.Combine(Path.GetDirectoryName(settingsPath)!, $"settings.json.usagenotch-bak-{Now.ToUnixTimeSeconds()}");
        File.Exists(backup).Should().BeTrue();
        File.ReadAllText(backup).Should().Be("{ \"model\": \"opus\" }");
    }

    [Fact]
    public void A_corrupt_settings_file_is_backed_up_and_replaced()
    {
        using var dir = new TempDir();
        var (installer, settingsPath, _) = Build(dir);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, "{ corrupt");

        installer.Install();

        Load(settingsPath)["hooks"].Should().NotBeNull();
        Directory.GetFiles(Path.GetDirectoryName(settingsPath)!, "settings.json.usagenotch-bak-*").Should().ContainSingle();
    }

    [Fact]
    public void Install_fails_when_the_hook_executable_is_missing()
    {
        using var dir = new TempDir();
        var (_, settingsPath, _) = Build(dir);
        var installer = new HookInstaller(settingsPath, dir.File("missing.exe"), new FakeTimeProvider(Now));

        var act = () => installer.Install();

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void Uninstall_removes_only_our_entries()
    {
        using var dir = new TempDir();
        var (installer, settingsPath, _) = Build(dir);
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);
        File.WriteAllText(settingsPath, """
        { "hooks": { "Stop": [ { "hooks": [ { "type": "command", "command": "other-tool notify" } ] } ] } }
        """);
        installer.Install();

        var message = installer.Uninstall();

        message.Should().Contain("7");
        var hooks = (JsonObject)Load(settingsPath)["hooks"]!;
        hooks.Select(kv => kv.Key).Should().Equal("Stop");
        ((JsonArray)hooks["Stop"]!).Should().HaveCount(1);
        installer.IsInstalled().Should().BeFalse();
    }

    [Fact]
    public void Uninstall_without_a_file_is_a_no_op()
    {
        using var dir = new TempDir();
        var (installer, _, _) = Build(dir);
        installer.Uninstall().Should().Contain("rien");
        installer.IsInstalled().Should().BeFalse();
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test --filter HookInstallerTests`
Expected: erreur de compilation.

- [ ] **Step 3 : Implémenter**

`src/UsageNotch.Core/Hooks/HookInstaller.cs` :
```csharp
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UsageNotch.Core.Hooks;

/// <summary>
/// Fusionne les sept hooks UsageNotch dans ~/.claude/settings.json sans toucher aux hooks de l'utilisateur.
/// Nos entrées sont reconnues par <see cref="Marker"/> dans la commande. Sauvegarde horodatée avant toute écriture.
/// </summary>
public sealed class HookInstaller(string settingsPath, string hookExePath, TimeProvider time)
{
    public const string Marker = "UsageNotch.Hook";

    public static readonly (string Event, bool NeedsMatcher, string Argument)[] Wiring =
    [
        ("SessionStart", false, "session_start"),
        ("UserPromptSubmit", false, "running"),
        ("PreToolUse", true, "running"),
        ("PostToolUse", true, "running"),
        ("Notification", false, "attention"),
        ("Stop", false, "done"),
        ("SessionEnd", false, "session_end"),
    ];

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static string DefaultSettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");

    public string SettingsPath { get; } = settingsPath;
    public string HookExePath { get; } = hookExePath;

    public bool IsInstalled()
    {
        try
        {
            return File.Exists(SettingsPath) && File.ReadAllText(SettingsPath).Contains(Marker, StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
    }

    public string Install()
    {
        if (!File.Exists(HookExePath))
        {
            throw new FileNotFoundException("Exécutable hook introuvable", HookExePath);
        }

        var root = LoadObject();
        if (root["hooks"] is not JsonObject hooks)
        {
            hooks = new JsonObject();
            root["hooks"] = hooks;
        }

        foreach (var (evt, needsMatcher, argument) in Wiring)
        {
            var existing = hooks[evt] as JsonArray ?? new JsonArray();
            var kept = new JsonArray();
            foreach (var entry in existing.ToList())
            {
                if (entry is not null && !IsOurs(entry))
                {
                    existing.Remove(entry);
                    kept.Add(entry);
                }
            }
            var ours = new JsonObject
            {
                ["hooks"] = new JsonArray(new JsonObject
                {
                    ["type"] = "command",
                    ["command"] = $"\"{HookExePath}\" {argument}",
                    ["timeout"] = 5,
                }),
            };
            if (needsMatcher) ours["matcher"] = "*";
            kept.Add(ours);
            hooks[evt] = kept;
        }

        BackupAndWrite(root);
        return $"{Wiring.Length} hooks écrits dans {SettingsPath}";
    }

    public string Uninstall()
    {
        if (!File.Exists(SettingsPath)) return "settings.json absent, rien à retirer";

        var root = LoadObject();
        if (root["hooks"] is not JsonObject hooks) return "aucun hook configuré, rien à retirer";

        var removed = 0;
        foreach (var key in hooks.Select(kv => kv.Key).ToList())
        {
            if (hooks[key] is not JsonArray array) continue;
            var kept = new JsonArray();
            foreach (var entry in array.ToList())
            {
                array.Remove(entry);
                if (entry is not null && IsOurs(entry)) removed++;
                else if (entry is not null) kept.Add(entry);
            }
            if (kept.Count == 0) hooks.Remove(key);
            else hooks[key] = kept;
        }

        BackupAndWrite(root);
        return $"{removed} hook(s) UsageNotch retiré(s)";
    }

    private static bool IsOurs(JsonNode entry) =>
        entry is JsonObject obj
        && obj["hooks"] is JsonArray list
        && list.Any(h => h is JsonObject ho
                         && ho["command"] is JsonValue v
                         && v.TryGetValue<string>(out var cmd)
                         && cmd.Contains(Marker, StringComparison.Ordinal));

    private JsonObject LoadObject()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new JsonObject();
            return JsonNode.Parse(File.ReadAllText(SettingsPath)) as JsonObject ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }

    private void BackupAndWrite(JsonObject root)
    {
        var dir = Path.GetDirectoryName(SettingsPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        if (File.Exists(SettingsPath))
        {
            var stamp = time.GetUtcNow().ToUnixTimeSeconds();
            File.Copy(SettingsPath, $"{SettingsPath}.usagenotch-bak-{stamp}", overwrite: true);
        }

        var temp = SettingsPath + ".tmp";
        File.WriteAllText(temp, root.ToJsonString(WriteOptions));
        File.Move(temp, SettingsPath, overwrite: true);
    }
}
```

- [ ] **Step 4 : Lancer les tests**

Run: `dotnet test --filter HookInstallerTests`
Expected: 8 tests passés.

- [ ] **Step 5 : Commit**

```powershell
git add src/UsageNotch.Core/Hooks/HookInstaller.cs tests/UsageNotch.Core.Tests/Hooks/HookInstallerTests.cs
git commit -m "feat(core): merge UsageNotch hooks into Claude Code settings.json"
```

---

### Task 12 : Réglages — modèle, thème, persistance

**Files:**
- Create: `src/UsageNotch.Core/Settings/ScreenEdge.cs`, `VisibilityMode.cs`, `CellContent.cs`, `ThemePreset.cs`, `Theme.cs`, `Settings.cs`, `SettingsStore.cs`
- Test: `tests/UsageNotch.Core.Tests/Settings/SettingsStoreTests.cs`

**Interfaces:**
- Produces:
  - `enum ScreenEdge { Right, Left, Top, Bottom }`, `enum VisibilityMode { Expanded, Folded, Hidden }`, `enum CellContent { RingAndPercent, RingOnly, PercentOnly }`, `enum ThemePreset { Codenotch, Monochrome, SystemAccent, Custom }`.
  - `record Theme(...)` : dix couleurs en hexadécimal `#RRGGBB`, `PillOpacity`, `ThresholdWatch`, `ThresholdCritical` ; `static Theme Codenotch`, `static Theme Monochrome`, `static Theme ForPreset(ThemePreset, Theme custom, string? systemAccentHex)`, `Theme Clamp()`, `string LevelColor(double fraction)`.
  - `record Settings` (propriétés `init`, valeurs par défaut) avec `const int CurrentVersion = 1`, `Settings Clamp()`, `double PositionFor(ScreenEdge)`, `Settings WithPosition(ScreenEdge, double)`.
  - `class SettingsStore(string filePath, ILogger<SettingsStore> logger)` avec `Settings Current`, `event Action<Settings>? Changed`, `Settings Load()`, `void Save(Settings)`, `static string DefaultDirectory`, `static string DefaultPath`.

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Core.Tests/Settings/SettingsStoreTests.cs` :
```csharp
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using UsageNotch.Core.Settings;

namespace UsageNotch.Core.Tests.Settings;

public class SettingsStoreTests
{
    private static SettingsStore Build(TempDir dir) =>
        new(dir.File("settings.json"), NullLogger<SettingsStore>.Instance);

    [Fact]
    public void Load_without_a_file_returns_defaults_and_writes_them()
    {
        using var dir = new TempDir();
        var store = Build(dir);

        var s = store.Load();

        s.Should().Be(new UsageNotch.Core.Settings.Settings());
        s.Port.Should().Be(48666);
        s.Edge.Should().Be(ScreenEdge.Right);
        s.Visibility.Should().Be(VisibilityMode.Expanded);
        s.Scale.Should().Be(1.0);
        File.ReadAllText(dir.File("settings.json")).Should().Contain("\"port\": 48666");
    }

    [Fact]
    public void Save_then_Load_round_trips_every_field()
    {
        using var dir = new TempDir();
        var store = Build(dir);
        var custom = Theme.Monochrome with { LevelCritical = "#FF0000", ThresholdWatch = 0.4 };
        var s = new UsageNotch.Core.Settings.Settings
        {
            Port = 50000,
            Edge = ScreenEdge.Top,
            MonitorDeviceId = @"\\.\DISPLAY2",
            Scale = 0.75,
            CellContent = CellContent.PercentOnly,
            Visibility = VisibilityMode.Folded,
            FoldedThicknessPx = 6,
            ThemePreset = ThemePreset.Custom,
            CustomTheme = custom,
            AutoOpenCard = false,
            SoundEnabled = false,
            DoneSound = "Hand",
            AttentionSound = "Question",
            TrayIconVisible = false,
            DebugLogging = true,
        }.WithPosition(ScreenEdge.Top, 0.25).WithPosition(ScreenEdge.Right, 0.9);

        store.Save(s);
        var loaded = Build(dir).Load();

        loaded.Should().Be(s);
        loaded.PositionFor(ScreenEdge.Top).Should().Be(0.25);
        loaded.PositionFor(ScreenEdge.Right).Should().Be(0.9);
        loaded.PositionFor(ScreenEdge.Left).Should().Be(0.5);
    }

    [Fact]
    public void Unknown_keys_are_ignored_and_missing_keys_get_defaults()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("settings.json"), """{ "scale": 1.2, "futureOption": true, "edge": "Left" }""");

        var s = Build(dir).Load();

        s.Scale.Should().Be(1.2);
        s.Edge.Should().Be(ScreenEdge.Left);
        s.Port.Should().Be(48666);
        s.CustomTheme.Should().Be(Theme.Codenotch);
    }

    [Fact]
    public void Out_of_range_values_are_clamped_on_load()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("settings.json"),
            """{ "scale": 9, "foldedThicknessPx": 0, "port": 80, "positionRight": 4, "visibility": "Hidden", "trayIconVisible": false,
                 "customTheme": { "pillOpacity": 0, "thresholdWatch": 0.9, "thresholdCritical": 0.3 } }""");

        var s = Build(dir).Load();

        s.Scale.Should().Be(1.5);
        s.FoldedThicknessPx.Should().Be(2);
        s.Port.Should().Be(48666);
        s.PositionFor(ScreenEdge.Right).Should().Be(1.0);
        s.TrayIconVisible.Should().BeTrue("le mode Masqué exige l'icône de notification");
        s.CustomTheme.PillOpacity.Should().Be(0.2);
        s.CustomTheme.ThresholdWatch.Should().Be(0.9);
        s.CustomTheme.ThresholdCritical.Should().Be(0.95, "critique est toujours au-dessus de surveillance");
        s.CustomTheme.PillBackground.Should().Be("#000000", "une couleur absente reprend le préréglage");
    }

    [Fact]
    public void A_corrupt_file_yields_defaults_without_throwing()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("settings.json"), "{ corrupt");
        Build(dir).Load().Should().Be(new UsageNotch.Core.Settings.Settings());
    }

    [Fact]
    public void Save_is_atomic_and_publishes()
    {
        using var dir = new TempDir();
        var store = Build(dir);
        UsageNotch.Core.Settings.Settings? published = null;
        store.Changed += s => published = s;

        store.Save(new UsageNotch.Core.Settings.Settings { Scale = 0.5 });

        published!.Scale.Should().Be(0.5);
        store.Current.Scale.Should().Be(0.5);
        Directory.GetFiles(dir.Path).Should().ContainSingle().Which.Should().EndWith("settings.json");
    }

    [Theory]
    [InlineData(0.10, "#28E07B")]
    [InlineData(0.49, "#28E07B")]
    [InlineData(0.50, "#F5E400")]
    [InlineData(0.79, "#F5E400")]
    [InlineData(0.80, "#FF4500")]
    [InlineData(1.00, "#FF4500")]
    public void Theme_level_colour_follows_the_thresholds(double fraction, string expected) =>
        Theme.Codenotch.LevelColor(fraction).Should().Be(expected);

    [Fact]
    public void System_accent_preset_paints_ample_and_running_with_the_accent()
    {
        var theme = Theme.ForPreset(ThemePreset.SystemAccent, Theme.Codenotch, "#0078D4");
        theme.LevelAmple.Should().Be("#0078D4");
        theme.Running.Should().Be("#0078D4");
        theme.LevelCritical.Should().Be(Theme.Codenotch.LevelCritical);
        Theme.ForPreset(ThemePreset.SystemAccent, Theme.Codenotch, null).Should().Be(Theme.Codenotch);
    }

    [Fact]
    public void Custom_preset_returns_the_custom_theme()
    {
        var custom = Theme.Monochrome with { Text = "#123456" };
        Theme.ForPreset(ThemePreset.Custom, custom, null).Should().Be(custom);
        Theme.ForPreset(ThemePreset.Monochrome, custom, null).Should().Be(Theme.Monochrome);
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test --filter SettingsStoreTests`
Expected: erreur de compilation.

- [ ] **Step 3 : Implémenter les enums et le thème**

`src/UsageNotch.Core/Settings/ScreenEdge.cs` :
```csharp
namespace UsageNotch.Core.Settings;

public enum ScreenEdge { Right, Left, Top, Bottom }
```

`src/UsageNotch.Core/Settings/VisibilityMode.cs` :
```csharp
namespace UsageNotch.Core.Settings;

/// <summary>Déplié : pilule complète. Replié : fine bande, dépli au survol. Masqué : rien à l'écran, icône de notification forcée.</summary>
public enum VisibilityMode { Expanded, Folded, Hidden }
```

`src/UsageNotch.Core/Settings/CellContent.cs` :
```csharp
namespace UsageNotch.Core.Settings;

public enum CellContent { RingAndPercent, RingOnly, PercentOnly }
```

`src/UsageNotch.Core/Settings/ThemePreset.cs` :
```csharp
namespace UsageNotch.Core.Settings;

public enum ThemePreset { Codenotch, Monochrome, SystemAccent, Custom }
```

`src/UsageNotch.Core/Settings/Theme.cs` :
```csharp
namespace UsageNotch.Core.Settings;

/// <summary>Couleurs en <c>#RRGGBB</c> (Core ne dépend pas de WPF ; l'App les convertit), opacité du fond et seuils de niveau.</summary>
public sealed record Theme(
    string PillBackground,
    string PillBorder,
    string RingTrack,
    string LevelAmple,
    string LevelWatch,
    string LevelCritical,
    string Running,
    string Attention,
    string Done,
    string Text,
    double PillOpacity,
    double ThresholdWatch,
    double ThresholdCritical)
{
    public static Theme Codenotch { get; } = new(
        PillBackground: "#000000", PillBorder: "#2E2E2E", RingTrack: "#3A3A3A",
        LevelAmple: "#28E07B", LevelWatch: "#F5E400", LevelCritical: "#FF4500",
        Running: "#28E07B", Attention: "#FFBF00", Done: "#57C7FF",
        Text: "#FFFFFF", PillOpacity: 1.0, ThresholdWatch: 0.50, ThresholdCritical: 0.80);

    public static Theme Monochrome { get; } = new(
        PillBackground: "#111111", PillBorder: "#3A3A3A", RingTrack: "#333333",
        LevelAmple: "#E0E0E0", LevelWatch: "#A0A0A0", LevelCritical: "#FFFFFF",
        Running: "#E0E0E0", Attention: "#FFFFFF", Done: "#B0B0B0",
        Text: "#FFFFFF", PillOpacity: 0.9, ThresholdWatch: 0.50, ThresholdCritical: 0.80);

    /// <summary>Le thème effectif. L'accent système est fourni par l'App ; sans accent, le préréglage Codenotch sert de repli.</summary>
    public static Theme ForPreset(ThemePreset preset, Theme custom, string? systemAccentHex) => preset switch
    {
        ThemePreset.Monochrome => Monochrome,
        ThemePreset.SystemAccent => systemAccentHex is null
            ? Codenotch
            : Codenotch with { LevelAmple = systemAccentHex, Running = systemAccentHex },
        ThemePreset.Custom => custom,
        _ => Codenotch,
    };

    public string LevelColor(double fraction) =>
        fraction >= ThresholdCritical ? LevelCritical
        : fraction >= ThresholdWatch ? LevelWatch
        : LevelAmple;

    /// <summary>Bornes des seuils et de l'opacité ; une couleur absente d'un JSON partiel reprend celle du préréglage Codenotch.</summary>
    public Theme Clamp()
    {
        var watch = Math.Clamp(ThresholdWatch, 0.05, 0.95);
        var critical = Math.Clamp(ThresholdCritical, watch + 0.05, 1.0);
        var d = Codenotch;
        return this with
        {
            PillBackground = Or(PillBackground, d.PillBackground),
            PillBorder = Or(PillBorder, d.PillBorder),
            RingTrack = Or(RingTrack, d.RingTrack),
            LevelAmple = Or(LevelAmple, d.LevelAmple),
            LevelWatch = Or(LevelWatch, d.LevelWatch),
            LevelCritical = Or(LevelCritical, d.LevelCritical),
            Running = Or(Running, d.Running),
            Attention = Or(Attention, d.Attention),
            Done = Or(Done, d.Done),
            Text = Or(Text, d.Text),
            PillOpacity = Math.Clamp(PillOpacity, 0.2, 1.0),
            ThresholdWatch = watch,
            ThresholdCritical = critical,
        };
    }

    // Un record positionnel désérialisé depuis un JSON partiel peut porter null malgré le type non-nullable.
    private static string Or(string? value, string fallback) => string.IsNullOrEmpty(value) ? fallback : value;
}
```

- [ ] **Step 4 : Implémenter les réglages et leur magasin**

`src/UsageNotch.Core/Settings/Settings.cs` :
```csharp
namespace UsageNotch.Core.Settings;

/// <summary>Réglages immuables. Une clé absente du JSON garde sa valeur par défaut ; <see cref="Clamp"/> ramène les valeurs dans les bornes.</summary>
public sealed record Settings
{
    public const int CurrentVersion = 1;
    public const double ScaleMin = 0.4;
    public const double ScaleMax = 1.5;
    public const int FoldedThicknessMin = 2;
    public const int FoldedThicknessMax = 12;
    public const int DefaultPort = 48666;

    public int Version { get; init; } = CurrentVersion;
    public int Port { get; init; } = DefaultPort;
    public ScreenEdge Edge { get; init; } = ScreenEdge.Right;
    public double PositionRight { get; init; } = 0.5;
    public double PositionLeft { get; init; } = 0.5;
    public double PositionTop { get; init; } = 0.5;
    public double PositionBottom { get; init; } = 0.5;
    /// <summary>Identifiant de périphérique de l'écran d'ancrage ; null = écran principal.</summary>
    public string? MonitorDeviceId { get; init; }
    public double Scale { get; init; } = 1.0;
    public CellContent CellContent { get; init; } = CellContent.RingAndPercent;
    public VisibilityMode Visibility { get; init; } = VisibilityMode.Expanded;
    public int FoldedThicknessPx { get; init; } = 4;
    public ThemePreset ThemePreset { get; init; } = ThemePreset.Codenotch;
    public Theme CustomTheme { get; init; } = Theme.Codenotch;
    public bool AutoOpenCard { get; init; } = true;
    public bool SoundEnabled { get; init; } = true;
    /// <summary>Noms de sons système Windows : Asterisk, Beep, Exclamation, Hand, Question.</summary>
    public string DoneSound { get; init; } = "Asterisk";
    public string AttentionSound { get; init; } = "Exclamation";
    public bool TrayIconVisible { get; init; } = true;
    public bool DebugLogging { get; init; }

    public double PositionFor(ScreenEdge edge) => edge switch
    {
        ScreenEdge.Left => PositionLeft,
        ScreenEdge.Top => PositionTop,
        ScreenEdge.Bottom => PositionBottom,
        _ => PositionRight,
    };

    public Settings WithPosition(ScreenEdge edge, double fraction)
    {
        var f = Math.Clamp(fraction, 0.0, 1.0);
        return edge switch
        {
            ScreenEdge.Left => this with { PositionLeft = f },
            ScreenEdge.Top => this with { PositionTop = f },
            ScreenEdge.Bottom => this with { PositionBottom = f },
            _ => this with { PositionRight = f },
        };
    }

    public Settings Clamp() => this with
    {
        Version = CurrentVersion,
        Port = Port is >= 1024 and <= 65535 ? Port : DefaultPort,
        PositionRight = Math.Clamp(PositionRight, 0.0, 1.0),
        PositionLeft = Math.Clamp(PositionLeft, 0.0, 1.0),
        PositionTop = Math.Clamp(PositionTop, 0.0, 1.0),
        PositionBottom = Math.Clamp(PositionBottom, 0.0, 1.0),
        Scale = Math.Clamp(Scale, ScaleMin, ScaleMax),
        FoldedThicknessPx = Math.Clamp(FoldedThicknessPx, FoldedThicknessMin, FoldedThicknessMax),
        CustomTheme = CustomTheme.Clamp(),
        // Sans pilule ni icône, l'app serait injoignable.
        TrayIconVisible = Visibility == VisibilityMode.Hidden || TrayIconVisible,
    };
}
```

`src/UsageNotch.Core/Settings/SettingsStore.cs` :
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace UsageNotch.Core.Settings;

public sealed class SettingsStore(string filePath, ILogger<SettingsStore> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static string DefaultDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UsageNotch");

    public static string DefaultPath => Path.Combine(DefaultDirectory, "settings.json");

    public string FilePath { get; } = filePath;

    public Settings Current { get; private set; } = new();

    public event Action<Settings>? Changed;

    /// <summary>Lit le fichier (tolérant : clés absentes → défauts, clés inconnues ignorées, fichier corrompu → défauts) et le réécrit normalisé pour que le hook y trouve toujours le port.</summary>
    public Settings Load()
    {
        Settings loaded = new();
        try
        {
            if (File.Exists(FilePath))
            {
                loaded = JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath), JsonOptions) ?? new Settings();
            }
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Réglages illisibles dans {Path}, valeurs par défaut", FilePath);
        }

        Current = loaded.Clamp();
        Write(Current);
        return Current;
    }

    public void Save(Settings settings)
    {
        Current = settings.Clamp();
        Write(Current);
        Changed?.Invoke(Current);
    }

    private void Write(Settings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var temp = FilePath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temp, FilePath, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Écriture des réglages impossible dans {Path}", FilePath);
        }
    }
}
```

- [ ] **Step 5 : Lancer les tests**

Run: `dotnet test --filter SettingsStoreTests`
Expected: 14 tests passés (8 Facts + 6 cas de Theory).

- [ ] **Step 6 : Commit**

```powershell
git add src/UsageNotch.Core/Settings tests/UsageNotch.Core.Tests/Settings
git commit -m "feat(core): settings model, theme presets and tolerant settings store"
```

---

### Task 13 : Calcul de placement (pilule, carte, écran)

**Files:**
- Create: `src/UsageNotch.Core/Placement/PixelRect.cs`, `MonitorInfo.cs`, `PillPlacement.cs`
- Test: `tests/UsageNotch.Core.Tests/Placement/PillPlacementTests.cs`

**Interfaces:**
- Consumes: `ScreenEdge` (Task 12).
- Produces:
  - `readonly record struct PixelRect(int X, int Y, int Width, int Height)` avec `Right`, `Bottom`, `CenterX`, `CenterY`.
  - `record MonitorInfo(string DeviceId, bool IsPrimary, PixelRect Bounds, PixelRect WorkArea, double Scale)`.
  - `static class PillPlacement` : `bool IsVertical(ScreenEdge)`, `PixelRect PillRect(PixelRect monitor, ScreenEdge edge, double fraction, int length, int thickness)`, `double FractionOf(PixelRect monitor, ScreenEdge edge, PixelRect pill)`, `PixelRect CardRect(PixelRect monitor, ScreenEdge edge, PixelRect pill, PixelRect anchor, int cardWidth, int cardHeight, int gap, int margin)`, `MonitorInfo Choose(IReadOnlyList<MonitorInfo> monitors, string? preferredDeviceId)`.
  - Tout est en pixels physiques ; l'App convertit les tailles logiques avec le facteur d'échelle de l'écran cible avant d'appeler.

- [ ] **Step 1 : Écrire les tests**

`tests/UsageNotch.Core.Tests/Placement/PillPlacementTests.cs` :
```csharp
using FluentAssertions;
using UsageNotch.Core.Placement;
using UsageNotch.Core.Settings;

namespace UsageNotch.Core.Tests.Placement;

public class PillPlacementTests
{
    // Écran 2560×1440 à 150 %, placé à droite d'un écran principal 1920 px de large
    private static readonly PixelRect Monitor = new(1920, 0, 2560, 1440);
    private const int Length = 300;
    private const int Thickness = 100;

    [Theory]
    [InlineData(ScreenEdge.Right, true)]
    [InlineData(ScreenEdge.Left, true)]
    [InlineData(ScreenEdge.Top, false)]
    [InlineData(ScreenEdge.Bottom, false)]
    public void Vertical_on_the_sides_horizontal_top_and_bottom(ScreenEdge edge, bool vertical) =>
        PillPlacement.IsVertical(edge).Should().Be(vertical);

    [Fact]
    public void Right_edge_at_the_middle_is_flush_and_centred()
    {
        var r = PillPlacement.PillRect(Monitor, ScreenEdge.Right, 0.5, Length, Thickness);
        r.Should().Be(new PixelRect(1920 + 2560 - 100, 570, 100, 300));
    }

    [Fact]
    public void Left_edge_extremes_stay_inside_the_monitor()
    {
        PillPlacement.PillRect(Monitor, ScreenEdge.Left, 0.0, Length, Thickness).Should().Be(new PixelRect(1920, 0, 100, 300));
        PillPlacement.PillRect(Monitor, ScreenEdge.Left, 1.0, Length, Thickness).Should().Be(new PixelRect(1920, 1140, 100, 300));
        PillPlacement.PillRect(Monitor, ScreenEdge.Left, 7.0, Length, Thickness).Y.Should().Be(1140, "la fraction est bornée");
    }

    [Fact]
    public void Top_and_bottom_lay_the_pill_along_the_horizontal_edge()
    {
        PillPlacement.PillRect(Monitor, ScreenEdge.Top, 0.5, Length, Thickness).Should().Be(new PixelRect(1920 + 1130, 0, 300, 100));
        PillPlacement.PillRect(Monitor, ScreenEdge.Bottom, 0.0, Length, Thickness).Should().Be(new PixelRect(1920, 1340, 300, 100));
    }

    [Fact]
    public void Fraction_is_the_inverse_of_placement()
    {
        foreach (var edge in Enum.GetValues<ScreenEdge>())
        {
            var pill = PillPlacement.PillRect(Monitor, edge, 0.3, Length, Thickness);
            PillPlacement.FractionOf(Monitor, edge, pill).Should().BeApproximately(0.3, 0.001, $"bord {edge}");
        }
    }

    [Fact]
    public void Card_opens_toward_the_centre_and_is_centred_on_the_anchor()
    {
        var pill = PillPlacement.PillRect(Monitor, ScreenEdge.Right, 0.5, Length, Thickness);
        var anchor = new PixelRect(pill.X + 20, pill.Y + 20, 60, 60); // première cellule
        var card = PillPlacement.CardRect(Monitor, ScreenEdge.Right, pill, anchor, cardWidth: 400, cardHeight: 200, gap: 12, margin: 8);

        card.Right.Should().Be(pill.X - 12);
        card.Width.Should().Be(400);
        card.CenterY.Should().Be(anchor.CenterY);

        var left = PillPlacement.PillRect(Monitor, ScreenEdge.Left, 0.5, Length, Thickness);
        PillPlacement.CardRect(Monitor, ScreenEdge.Left, left, anchor with { X = left.X + 20 }, 400, 200, 12, 8).X.Should().Be(left.Right + 12);

        var top = PillPlacement.PillRect(Monitor, ScreenEdge.Top, 0.5, Length, Thickness);
        PillPlacement.CardRect(Monitor, ScreenEdge.Top, top, new PixelRect(top.X + 20, top.Y + 20, 60, 60), 400, 200, 12, 8).Y.Should().Be(top.Bottom + 12);

        var bottom = PillPlacement.PillRect(Monitor, ScreenEdge.Bottom, 0.5, Length, Thickness);
        PillPlacement.CardRect(Monitor, ScreenEdge.Bottom, bottom, new PixelRect(bottom.X + 20, bottom.Y + 20, 60, 60), 400, 200, 12, 8).Bottom.Should().Be(bottom.Y - 12);
    }

    [Fact]
    public void Card_is_kept_inside_the_monitor_with_a_margin()
    {
        var pill = PillPlacement.PillRect(Monitor, ScreenEdge.Right, 0.0, Length, Thickness);
        var anchor = new PixelRect(pill.X + 20, pill.Y + 4, 60, 60);

        var card = PillPlacement.CardRect(Monitor, ScreenEdge.Right, pill, anchor, 400, 600, 12, 8);

        card.Y.Should().Be(Monitor.Y + 8);
        card.Bottom.Should().BeLessThanOrEqualTo(Monitor.Bottom - 8);
    }

    [Fact]
    public void Choose_prefers_the_remembered_monitor_then_the_primary_then_the_first()
    {
        var primary = new MonitorInfo(@"\\.\DISPLAY1", true, new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 0, 1920, 1040), 1.0);
        var second = new MonitorInfo(@"\\.\DISPLAY2", false, Monitor, Monitor, 1.5);
        var monitors = new[] { second, primary };

        PillPlacement.Choose(monitors, @"\\.\DISPLAY2").Should().Be(second);
        PillPlacement.Choose(monitors, @"\\.\DISPLAY9").Should().Be(primary);
        PillPlacement.Choose(monitors, null).Should().Be(primary);
        PillPlacement.Choose([second], null).Should().Be(second);
    }

    [Fact]
    public void Choose_throws_without_monitors()
    {
        var act = () => PillPlacement.Choose([], null);
        act.Should().Throw<InvalidOperationException>();
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test --filter PillPlacementTests`
Expected: erreur de compilation.

- [ ] **Step 3 : Implémenter**

`src/UsageNotch.Core/Placement/PixelRect.cs` :
```csharp
namespace UsageNotch.Core.Placement;

/// <summary>Rectangle en pixels physiques (coordonnées écran Win32).</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;
    public int Bottom => Y + Height;
    public int CenterX => X + Width / 2;
    public int CenterY => Y + Height / 2;

    public bool Contains(int px, int py) => px >= X && py >= Y && px < Right && py < Bottom;
}
```

`src/UsageNotch.Core/Placement/MonitorInfo.cs` :
```csharp
namespace UsageNotch.Core.Placement;

/// <summary>Un écran tel que l'App l'énumère. <paramref name="Scale"/> : 1.0 = 100 %, 1.5 = 150 %.</summary>
public sealed record MonitorInfo(string DeviceId, bool IsPrimary, PixelRect Bounds, PixelRect WorkArea, double Scale);
```

`src/UsageNotch.Core/Placement/PillPlacement.cs` :
```csharp
using UsageNotch.Core.Settings;

namespace UsageNotch.Core.Placement;

/// <summary>Géométrie pure : où va la pilule sur un bord, où va la carte face à une cellule. Aucune dépendance système.</summary>
public static class PillPlacement
{
    public static bool IsVertical(ScreenEdge edge) => edge is ScreenEdge.Right or ScreenEdge.Left;

    /// <summary>La pilule collée au bord physique de l'écran, glissée le long du bord selon <paramref name="fraction"/> (0 = début, 1 = fin).</summary>
    public static PixelRect PillRect(PixelRect monitor, ScreenEdge edge, double fraction, int length, int thickness)
    {
        var f = Math.Clamp(fraction, 0.0, 1.0);
        if (IsVertical(edge))
        {
            var travel = Math.Max(0, monitor.Height - length);
            var y = monitor.Y + (int)Math.Round(travel * f);
            var x = edge == ScreenEdge.Right ? monitor.Right - thickness : monitor.X;
            return new PixelRect(x, y, thickness, length);
        }
        else
        {
            var travel = Math.Max(0, monitor.Width - length);
            var x = monitor.X + (int)Math.Round(travel * f);
            var y = edge == ScreenEdge.Bottom ? monitor.Bottom - thickness : monitor.Y;
            return new PixelRect(x, y, length, thickness);
        }
    }

    /// <summary>Inverse de <see cref="PillRect"/>, pour mémoriser la position après un glisser.</summary>
    public static double FractionOf(PixelRect monitor, ScreenEdge edge, PixelRect pill)
    {
        if (IsVertical(edge))
        {
            var travel = monitor.Height - pill.Height;
            return travel <= 0 ? 0.0 : Math.Clamp((pill.Y - monitor.Y) / (double)travel, 0.0, 1.0);
        }
        else
        {
            var travel = monitor.Width - pill.Width;
            return travel <= 0 ? 0.0 : Math.Clamp((pill.X - monitor.X) / (double)travel, 0.0, 1.0);
        }
    }

    /// <summary>La carte face à <paramref name="anchor"/>, vers le centre de l'écran, maintenue dans l'écran à <paramref name="margin"/> près.</summary>
    public static PixelRect CardRect(PixelRect monitor, ScreenEdge edge, PixelRect pill, PixelRect anchor, int cardWidth, int cardHeight, int gap, int margin)
    {
        int x, y;
        switch (edge)
        {
            case ScreenEdge.Right:
                x = pill.X - gap - cardWidth;
                y = anchor.CenterY - cardHeight / 2;
                break;
            case ScreenEdge.Left:
                x = pill.Right + gap;
                y = anchor.CenterY - cardHeight / 2;
                break;
            case ScreenEdge.Top:
                x = anchor.CenterX - cardWidth / 2;
                y = pill.Bottom + gap;
                break;
            default:
                x = anchor.CenterX - cardWidth / 2;
                y = pill.Y - gap - cardHeight;
                break;
        }

        x = Math.Clamp(x, monitor.X + margin, Math.Max(monitor.X + margin, monitor.Right - margin - cardWidth));
        y = Math.Clamp(y, monitor.Y + margin, Math.Max(monitor.Y + margin, monitor.Bottom - margin - cardHeight));
        return new PixelRect(x, y, cardWidth, cardHeight);
    }

    /// <summary>L'écran mémorisé s'il est présent, sinon le principal, sinon le premier. Le choix n'est jamais perdu : l'appelant garde l'identifiant.</summary>
    public static MonitorInfo Choose(IReadOnlyList<MonitorInfo> monitors, string? preferredDeviceId)
    {
        if (monitors.Count == 0) throw new InvalidOperationException("Aucun écran détecté");
        if (preferredDeviceId is not null)
        {
            var preferred = monitors.FirstOrDefault(m => m.DeviceId.Equals(preferredDeviceId, StringComparison.OrdinalIgnoreCase));
            if (preferred is not null) return preferred;
        }
        return monitors.FirstOrDefault(m => m.IsPrimary) ?? monitors[0];
    }
}
```

- [ ] **Step 4 : Lancer les tests**

Run: `dotnet test --filter PillPlacementTests`
Expected: 12 tests passés (8 Facts + 4 cas de Theory).

- [ ] **Step 5 : Commit**

```powershell
git add src/UsageNotch.Core/Placement tests/UsageNotch.Core.Tests/Placement
git commit -m "feat(core): pure placement geometry for pill, card and monitor choice"
```

---

### Task 14 : L'exécutable hook (Native AOT)

**Files:**
- Create: `src/UsageNotch.Hook/PortReader.cs`, `src/UsageNotch.Hook/ParentProcess.cs`
- Modify: `src/UsageNotch.Hook/Program.cs` (remplace le `return 0;` provisoire de la Task 1)
- Test: `tests/UsageNotch.Core.Tests/Hook/PortReaderTests.cs` (le fichier `PortReader.cs` est lié en source dans le projet de tests depuis la Task 1)

**Interfaces:**
- Consumes: le récepteur de la Task 10 (`POST /event?e=<kind>&ppid=<pid>`), le fichier de réglages de la Task 12 (clé `"port"`).
- Produces: `UsageNotch.Hook.exe <kind>` — lit stdin, poste, lance `UsageNotch.App.exe` (Plan 2) s'il ne répond pas, sort toujours en 0.
  - `static int PortReader.Read(string? settingsJson)`, `const int PortReader.DefaultPort = 48666`, `static string PortReader.DefaultSettingsPath`.
  - `static int ParentProcess.Id()`.

- [ ] **Step 1 : Écrire les tests du lecteur de port**

`tests/UsageNotch.Core.Tests/Hook/PortReaderTests.cs` :
```csharp
using FluentAssertions;
using UsageNotch.Hook;

namespace UsageNotch.Core.Tests.Hook;

public class PortReaderTests
{
    [Fact]
    public void Null_or_empty_gives_the_default_port()
    {
        PortReader.Read(null).Should().Be(48666);
        PortReader.Read("").Should().Be(48666);
    }

    [Theory]
    [InlineData("""{ "port": 5000 }""", 5000)]
    [InlineData("""{"scale":1,"port":48670,"edge":"Right"}""", 48670)]
    [InlineData("""{ "port" : 1234 }""", 1234)]
    public void Reads_the_port_key(string json, int expected) =>
        PortReader.Read(json).Should().Be(expected);

    [Theory]
    [InlineData("""{ "port": "abc" }""")]
    [InlineData("""{ "port": 99999 }""")]
    [InlineData("""{ "port": 80 }""")]
    [InlineData("""{ "portable": 7 }""")]
    [InlineData("""{ "scale": 1 }""")]
    [InlineData("not json at all")]
    public void Anything_else_gives_the_default_port(string json) =>
        PortReader.Read(json).Should().Be(48666);

    [Fact]
    public void Default_settings_path_is_under_appdata()
    {
        PortReader.DefaultSettingsPath.Should().EndWith(Path.Combine("UsageNotch", "settings.json"));
    }
}
```

- [ ] **Step 2 : Vérifier l'échec**

Run: `dotnet test --filter PortReaderTests`
Expected: erreur de compilation (`UsageNotch.Hook.PortReader` inconnu ; le lien source est conditionnel à l'existence du fichier).

- [ ] **Step 3 : Implémenter le lecteur de port**

`src/UsageNotch.Hook/PortReader.cs` :
```csharp
namespace UsageNotch.Hook;

/// <summary>Lecture textuelle minimale de <c>"port": N</c> dans settings.json : pas de parseur JSON dans le hook, pour rester minuscule et rapide.</summary>
public static class PortReader
{
    public const int DefaultPort = 48666;

    public static string DefaultSettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "UsageNotch", "settings.json");

    public static int Read(string? settingsJson)
    {
        if (string.IsNullOrEmpty(settingsJson)) return DefaultPort;

        var key = settingsJson.IndexOf("\"port\"", StringComparison.Ordinal);
        if (key < 0) return DefaultPort;

        var i = key + "\"port\"".Length;
        while (i < settingsJson.Length && (settingsJson[i] == ' ' || settingsJson[i] == ':' || settingsJson[i] == '\t')) i++;

        var start = i;
        while (i < settingsJson.Length && char.IsAsciiDigit(settingsJson[i])) i++;
        if (i == start) return DefaultPort;

        return int.TryParse(settingsJson.AsSpan(start, i - start), out var port) && port is >= 1024 and <= 65535
            ? port
            : DefaultPort;
    }
}
```

- [ ] **Step 4 : Lancer les tests du lecteur de port**

Run: `dotnet test --filter PortReaderTests`
Expected: 11 tests passés (2 Facts + 9 cas de Theory).

- [ ] **Step 5 : Implémenter le PID parent et le programme**

`src/UsageNotch.Hook/ParentProcess.cs` :
```csharp
using System.Runtime.InteropServices;

namespace UsageNotch.Hook;

/// <summary>PID du processus parent via NtQueryInformationProcess, sans dépendance. 0 en cas d'échec.</summary>
internal static partial class ParentProcess
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public nint ExitStatus;
        public nint PebBaseAddress;
        public nint AffinityMask;
        public nint BasePriority;
        public nint UniqueProcessId;
        public nint InheritedFromUniqueProcessId;
    }

    [LibraryImport("ntdll.dll")]
    private static partial int NtQueryInformationProcess(nint processHandle, int informationClass, ref ProcessBasicInformation information, int length, out int returnLength);

    public static int Id()
    {
        if (!OperatingSystem.IsWindows()) return 0;
        var info = default(ProcessBasicInformation);
        var status = NtQueryInformationProcess(-1, 0, ref info, Marshal.SizeOf<ProcessBasicInformation>(), out _);
        return status == 0 ? (int)info.InheritedFromUniqueProcessId : 0;
    }
}
```

`src/UsageNotch.Hook/Program.cs` :
```csharp
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace UsageNotch.Hook;

/// <summary>
/// Appelé par les hooks de Claude Code : relaie l'événement (argument) et le JSON (stdin) à l'app.
/// Règle absolue : ne jamais bloquer Claude Code — budget ~2 s, toute erreur sort en 0 sans bruit.
/// </summary>
internal static class Program
{
    private const int MaxStdinBytes = 256 * 1024;
    private const int ConnectTimeoutMs = 300;
    private const int IoTimeoutMs = 700;
    private const string AppExeName = "UsageNotch.App.exe";

    private static int Main(string[] args)
    {
        try
        {
            Run(args);
        }
        catch
        {
            // Silence : le hook ne doit jamais faire échouer Claude Code.
        }
        return 0;
    }

    private static void Run(string[] args)
    {
        var kind = args.Length > 0 ? args[0] : "ping";
        var body = ReadStdin();
        var port = PortReader.Read(ReadSettings());
        var ppid = ParentProcess.Id();

        if (Send(port, kind, ppid, body)) return;

        // L'app ne répond pas : la lancer détachée puis réessayer brièvement.
        LaunchApp();
        for (var i = 0; i < 20; i++)
        {
            Thread.Sleep(100);
            if (Send(port, kind, ppid, body)) return;
        }
    }

    private static string ReadStdin()
    {
        if (!Console.IsInputRedirected) return "";
        using var input = Console.OpenStandardInput();
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = input.Read(chunk, 0, chunk.Length)) > 0)
        {
            buffer.Write(chunk, 0, read);
            if (buffer.Length >= MaxStdinBytes) break;
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)Math.Min(buffer.Length, MaxStdinBytes));
    }

    private static string? ReadSettings()
    {
        try
        {
            var path = PortReader.DefaultSettingsPath;
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch
        {
            return null;
        }
    }

    private static bool Send(int port, string kind, int ppid, string body)
    {
        try
        {
            using var client = new TcpClient();
            var connect = client.ConnectAsync(IPAddress.Loopback, port);
            if (!connect.Wait(ConnectTimeoutMs) || !client.Connected) return false;
            client.SendTimeout = IoTimeoutMs;
            client.ReceiveTimeout = IoTimeoutMs;

            using var stream = client.GetStream();
            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var head = $"POST /event?e={Uri.EscapeDataString(kind)}&ppid={ppid} HTTP/1.1\r\n"
                     + "Host: 127.0.0.1\r\nContent-Type: application/json\r\n"
                     + $"Content-Length: {bodyBytes.Length}\r\nConnection: close\r\n\r\n";
            stream.Write(Encoding.ASCII.GetBytes(head));
            stream.Write(bodyBytes);
            stream.Flush();

            var ack = new byte[64];
            _ = stream.Read(ack, 0, ack.Length); // un fragment de réponse confirme la livraison ; l'échec n'a pas d'importance
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void LaunchApp()
    {
        try
        {
            var exe = Path.Combine(AppContext.BaseDirectory, AppExeName);
            if (!File.Exists(exe)) return;
            var start = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = AppContext.BaseDirectory,
            };
            Process.Start(start);
        }
        catch
        {
            // L'app absente ou impossible à lancer : on abandonne en silence.
        }
    }
}
```

- [ ] **Step 6 : Compiler en Native AOT et vérifier à la main**

Run (la publication AOT a besoin des outils C++ de Visual Studio 2022, présents sur la machine) :
```powershell
dotnet publish src/UsageNotch.Hook -c Release -r win-x64
```
Expected: `src\UsageNotch.Hook\bin\Release\net9.0\win-x64\publish\UsageNotch.Hook.exe` existe, taille de l'ordre de 2 à 4 Mo.

Vérification sans app en écoute (doit sortir en 0 en environ 2,3 s : 300 ms de connexion + 20 × 100 ms) :
```powershell
$sw = [Diagnostics.Stopwatch]::StartNew()
'{"session_id":"manual-test","cwd":"C:\\tmp"}' | .\src\UsageNotch.Hook\bin\Release\net9.0\win-x64\publish\UsageNotch.Hook.exe running
"exit=$LASTEXITCODE elapsed=$($sw.ElapsedMilliseconds)ms"
```
Expected: `exit=0`, moins de 3000 ms.

Vérification avec un récepteur (PowerShell joue l'app le temps du test) :
```powershell
$l = New-Object System.Net.HttpListener; $l.Prefixes.Add("http://127.0.0.1:48666/"); $l.Start()
Start-Job -ScriptBlock { '{"session_id":"manual-test"}' | & "$using:PWD\src\UsageNotch.Hook\bin\Release\net9.0\win-x64\publish\UsageNotch.Hook.exe" done } | Out-Null
$ctx = $l.GetContext(); $body = (New-Object IO.StreamReader($ctx.Request.InputStream)).ReadToEnd()
"$($ctx.Request.HttpMethod) $($ctx.Request.RawUrl) body=$body"
$ctx.Response.StatusCode = 200; $ctx.Response.Close(); $l.Stop()
```
Expected: `POST /event?e=done&ppid=<nombre> body={"session_id":"manual-test"}`.

- [ ] **Step 7 : Lancer toute la suite**

Run: `dotnet test`
Expected: tous les tests passés, 0 échec.

- [ ] **Step 8 : Commit**

```powershell
git add src/UsageNotch.Hook tests/UsageNotch.Core.Tests/Hook
git commit -m "feat(hook): Native AOT relay for Claude Code hook events"
```

---

## Ce que le Plan 2 (App WPF) consommera

Le Plan 2 sera rédigé une fois ce plan exécuté, à partir des signatures réellement produites. Il branchera :

- `ClaudeCredentialReader(ClaudeCredentialReader.DefaultDirectory, TimeProvider.System)` ; `HttpClient` avec `Timeout = ClaudeUsageProvider.Timeout` ; `ClaudeUsageProvider` enregistré comme `IUsageProvider`.
- `UsageStore(Path.Combine(SettingsStore.DefaultDirectory, "usage.json"), …)` ; `UsagePoller` et `SessionSweeper` et `HookListener` comme `IHostedService` ; `SessionStore` comme singleton, aussi exposé comme `ISessionActivity`.
- `SettingsStore(SettingsStore.DefaultPath, …)`, `Load()` au démarrage avant tout le reste ; le port du `HookListener` vient de `Settings.Port`.
- `HookInstaller(HookInstaller.DefaultSettingsPath, Path.Combine(AppContext.BaseDirectory, "UsageNotch.Hook.exe"), TimeProvider.System)`.
- `PillPlacement` avec des `MonitorInfo` construits par l'interop Win32 et des tailles converties en pixels physiques.
- L'exécutable hook attend `UsageNotch.App.exe` dans son propre dossier : les deux projets publient dans le même répertoire.
- `HookListener.OpenSettingsRequested` pour l'instance unique : un second lancement poste sur `/open-settings` puis quitte.

## Auto-relecture

**Couverture de la spec.** §3 architecture : Tasks 1, 7, 8, 10 (BackgroundService). §4 fournisseur : Tasks 2 à 7 (modèle, jeton, appel, parseur, erreurs, cadence, persistance). §5 hooks : Tasks 8 à 11, 14 (installation, transport, machine à états, agrégation ; le retour au terminal et l'auto-ouverture sont de l'App, Plan 2). §7 thème et réglages : Task 12. §8 écrans et persistance : Tasks 12 et 13 (l'énumération Win32 et le mutex sont de l'App, Plan 2 ; le signal `/open-settings` est en Task 10). §9 tests : chaque ligne du tableau de la spec a sa classe de tests, sauf « ViewModels » (Plan 2).

**Cohérence des types.** `FetchResult` (Task 2) est consommé tel quel en Tasks 5, 6, 7. `ISessionActivity` (Task 7) est implémenté par `SessionStore` (Task 8). `HookEvent` (Task 8) est produit par `HookEventParser` (Task 9) et consommé par `HookListener` (Task 10). `ScreenEdge` (Task 12) est consommé par `PillPlacement` (Task 13). Le port par défaut 48666 est le même dans `HookListener.DefaultPort`, `Settings.DefaultPort` et `PortReader.DefaultPort`.
