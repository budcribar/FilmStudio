using System.Reflection;
using PageToMovie.Core.Abstractions;
using PageToMovie.Engine.Abstractions;
using PageToMovie.Engine.ModelExecution;
using Xunit;

namespace PageToMovie.Tests;

public sealed class EngineNamespaceBoundaryTests
{
    private static readonly HashSet<Type> ModelClientTypes =
    [
        typeof(IVideoClient),
        typeof(IImageClient),
        typeof(IChatClient),
        typeof(IVisionClient),
        typeof(IGeminiVideoAnalysisClient),
        typeof(IAudioClient),
        typeof(ILipSyncClient),
        typeof(IVoiceCloneClient),
    ];

    [Fact]
    public void Engine_contains_the_three_execution_boundaries()
    {
        var namespaces = typeof(PageToMovie.Engine.Deterministic.NamespaceMarker).Assembly
            .GetTypes()
            .Select(type => type.Namespace)
            .Where(value => value is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains("PageToMovie.Engine.Deterministic", namespaces);
        Assert.Contains("PageToMovie.Engine.ModelBacked", namespaces);
        Assert.Contains("PageToMovie.Engine.ModelExecution", namespaces);
    }

    [Fact]
    public void Deterministic_types_do_not_declare_model_or_network_dependencies()
    {
        var violations = typeof(PageToMovie.Engine.Deterministic.NamespaceMarker).Assembly
            .GetTypes()
            .Where(type => type.Namespace?.StartsWith(
                "PageToMovie.Engine.Deterministic",
                StringComparison.Ordinal) == true)
            .SelectMany(FindForbiddenDependencies)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Deterministic code must not declare model-client or network dependencies:\n" +
            string.Join("\n", violations));
    }

    private static IEnumerable<string> FindForbiddenDependencies(Type owner)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Static |
                                   BindingFlags.Public | BindingFlags.NonPublic |
                                   BindingFlags.DeclaredOnly;

        var dependencies = new List<(string Member, Type Type)>();
        if (owner.BaseType is not null)
            dependencies.Add(("base type", owner.BaseType));
        dependencies.AddRange(owner.GetInterfaces().Select(type => ("interface", type)));
        dependencies.AddRange(owner.GetFields(flags).Select(field => ($"field {field.Name}", field.FieldType)));
        dependencies.AddRange(owner.GetProperties(flags).Select(property => ($"property {property.Name}", property.PropertyType)));

        foreach (var method in owner.GetMethods(flags))
        {
            dependencies.Add(($"method {method.Name} return", method.ReturnType));
            dependencies.AddRange(method.GetParameters()
                .Select(parameter => ($"method {method.Name} parameter {parameter.Name}", parameter.ParameterType)));
        }

        foreach (var constructor in owner.GetConstructors(flags))
        {
            dependencies.AddRange(constructor.GetParameters()
                .Select(parameter => ($"constructor parameter {parameter.Name}", parameter.ParameterType)));
        }

        foreach (var dependency in dependencies)
        {
            foreach (var type in Flatten(dependency.Type))
            {
                if (ModelClientTypes.Contains(type) ||
                    type == typeof(HttpClient) ||
                    type.Namespace?.StartsWith("System.Net.Http", StringComparison.Ordinal) == true ||
                    type.Namespace?.StartsWith("PageToMovie.Engine.ModelExecution", StringComparison.Ordinal) == true)
                {
                    yield return $"{owner.FullName}: {dependency.Member} -> {type.FullName}";
                }
            }
        }
    }

    [Fact]
    public void Migrated_adaptation_services_do_not_call_model_clients_directly()
    {
        var root = FindRepositoryRoot();
        string[] migratedServices =
        [
            "CastFromScreenplayService.cs", "CastVisualLiteralizeService.cs",
            "CinematicLightingClassifier.cs", "ColorPaletteGradingClassifier.cs",
            "NegativePromptClassifier.cs", "ClipAutoReviewService.cs",
            "ClipDialogueVerificationService.cs", "MovieAutoReviewService.cs",
        ];
        string[] directCallMarkers =
        [
            ".CompleteAsync(", ".AnalyzeVideoAsync(", ".AnalyzeImageAsync(",
            ".ClassifyCharactersOnImageAsync(", ".GenerateAsync(",
        ];
        var violations = migratedServices
            .Select(name => Path.Combine(root, "host", "PageToMovie.Engine", name))
            .Where(File.Exists)
            .Where(path => directCallMarkers.Any(marker =>
                File.ReadAllText(path).Contains(marker, StringComparison.Ordinal)))
            .Select(Path.GetFileName)
            .ToArray();
        Assert.True(violations.Length == 0,
            "Migrated adaptation services must delegate model requests to ModelBacked operations: " +
            string.Join(", ", violations));
    }

    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "docs", "architecture", "MODEL_CALL_INVENTORY.md")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;
        if (type.HasElementType && type.GetElementType() is { } elementType)
        {
            foreach (var nested in Flatten(elementType))
                yield return nested;
        }

        if (!type.IsGenericType)
            yield break;

        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var nested in Flatten(argument))
                yield return nested;
        }
    }
}
