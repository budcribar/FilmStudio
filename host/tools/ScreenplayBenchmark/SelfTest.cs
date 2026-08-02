namespace ScreenplayBenchmark;

/// <summary>
/// Zero-cost, zero-API-call regression check (<c>--self-test</c>) for the extraction/parsing logic
/// that a real Call of the Wild pilot run exposed as broken: <see cref="AdaptationSessionPilot.ExtractJson"/>
/// only recognized a top-level <c>{</c>, so a cast/locations response that came back as a bare
/// array (deviating from the instructed <c>{"cast_seeds":{"characters":[...]}}</c> shape) got
/// silently reduced to just its first element — no exception, no warning, a corrupted artifact
/// written to disk as if it were correct. This tool has no xunit test project, so this is a small
/// permanent guard against that exact failure mode recurring, run via <c>dotnet run --project
/// tools/ScreenplayBenchmark -- --self-test</c> (no book, no API key, no spend).
/// </summary>
internal static class SelfTest
{
    public static int Run()
    {
        var failures = new List<string>();
        void Check(string name, bool condition)
        {
            Console.WriteLine(condition ? $"  ✅ {name}" : $"  ❌ {name}");
            if (!condition) failures.Add(name);
        }

        Console.WriteLine("Self-test: ExtractJson + ParseCastAndLocationKeys");
        Console.WriteLine();

        // 1. Original, still-supported shape: a top-level object.
        var objectShape = """
            Here is the result:
            {"cast_seeds":{"characters":[{"key":"BUCK","display_name":"Buck"}]},"location_bible":{"locations":[{"key":"LOC_CABIN"}]}}
            Done.
            """;
        var extractedObject = AdaptationSessionPilot.ExtractJson(objectShape);
        Check("ExtractJson still returns a wrapped object shape intact",
            extractedObject.Contains("\"cast_seeds\"") && extractedObject.Contains("\"location_bible\""));

        // 2. The actual regression: a bare top-level array (no wrapper object) — the exact shape
        // observed in a real Call of the Wild run's malformed model response.
        var arrayShape = """
            [{"key":"BUCK","display_name":"Buck"},{"key":"JUDGE_MILLER","display_name":"Judge Miller"}]
            """;
        var extractedArray = AdaptationSessionPilot.ExtractJson(arrayShape);
        Check("ExtractJson returns the WHOLE array, not just the first element",
            extractedArray.Contains("\"BUCK\"") && extractedArray.Contains("\"JUDGE_MILLER\""));

        // 3. Cast-key parsing must recover keys from the object-wrapped shape (existing behavior).
        var (wrappedCastKeys, wrappedLocationKeys) = AdaptationPackageValidator.ParseCastAndLocationKeys(extractedObject);
        Check("ParseCastAndLocationKeys recovers cast keys from the wrapped shape",
            wrappedCastKeys.Contains("BUCK"));
        Check("ParseCastAndLocationKeys recovers location keys from the wrapped shape",
            wrappedLocationKeys.Contains("LOC_CABIN"));

        // 4. Cast-key parsing must ALSO recover keys from a bare array (the fallback added to fix
        // the regression) — this is the check that would have caught the real bug before it ever
        // reached a paid pilot run.
        var (arrayCastKeys, _) = AdaptationPackageValidator.ParseCastAndLocationKeys(extractedArray);
        Check("ParseCastAndLocationKeys recovers BOTH cast keys from a bare array (no wrapper)",
            arrayCastKeys.Contains("BUCK") && arrayCastKeys.Contains("JUDGE_MILLER"));

        // 5. HasValidCastLocationsShape must correctly flag the malformed shape as needing a
        // corrective retry (this is what actually triggers the retry, not just the parser fallback).
        Check("HasValidCastLocationsShape flags the wrapped shape as valid",
            AdaptationSessionPilot.HasValidCastLocationsShape(extractedObject));
        Check("HasValidCastLocationsShape flags the bare-array shape as INVALID (needs retry)",
            !AdaptationSessionPilot.HasValidCastLocationsShape(extractedArray));
        Check("HasValidCastLocationsShape flags cast-only (no location_bible) as INVALID",
            !AdaptationSessionPilot.HasValidCastLocationsShape(
                """{"cast_seeds":{"characters":[{"key":"BUCK"}]}}"""));

        Console.WriteLine();
        if (failures.Count > 0)
        {
            Console.WriteLine($"❌ {failures.Count} self-test check(s) failed: {string.Join(", ", failures)}");
            return 1;
        }
        Console.WriteLine("✅ All self-test checks passed.");
        return 0;
    }
}
