using System.Text.Json;
using System.Text.Json.Serialization;

namespace Claims;

public sealed record ClaimResult(int EstimatedPayableCents, string Queue, IReadOnlyList<string> Reasons);
public sealed record ErrorBody(string Error);
public sealed record Health(string Status);
public sealed record ReleaseInfo(string Release);

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ClaimResult))]
[JsonSerializable(typeof(ErrorBody))]
[JsonSerializable(typeof(Health))]
[JsonSerializable(typeof(ReleaseInfo))]
internal sealed partial class ClaimsJsonContext : JsonSerializerContext;

public static class ClaimAnalysis
{
    public const int BodyLimit = 2048;
    private static readonly string[] Fields = ["lossCents", "deductibleCents", "limitCents", "daysToReport"];

    // Money is expressed in integer USD cents, never floating-point dollars.
    public static ClaimResult Analyze(JsonElement input)
    {
        if (input.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("Supply a JSON object.");
        var names = input.EnumerateObject().Select(p => p.Name).ToList();
        if (names.Count != Fields.Length || !Fields.All(names.Contains))
            throw new ArgumentException("Supply exactly lossCents, deductibleCents, limitCents and daysToReport.");

        int loss = Integer(input, "lossCents", 1, 1_000_000_000);
        int deductible = Integer(input, "deductibleCents", 0, 1_000_000_000);
        int limit = Integer(input, "limitCents", 1, 1_000_000_000);
        int days = Integer(input, "daysToReport", 0, 3650);

        var reasons = new List<string>();
        int net = Math.Max(0, loss - deductible);
        if (net == 0) reasons.Add("WITHIN_DEDUCTIBLE");
        if (net > limit) reasons.Add("POLICY_LIMIT_APPLIED");
        if (days > 30) reasons.Add("LATE_REPORT");
        return new ClaimResult(Math.Min(net, limit), reasons.Count == 0 ? "standard" : "adjuster-review", reasons);
    }

    private static int Integer(JsonElement input, string name, int min, int max)
    {
        var value = input.GetProperty(name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out int result) || result < min || result > max)
            throw new ArgumentException($"{name} must be an integer between {min} and {max}.");
        return result;
    }
}
