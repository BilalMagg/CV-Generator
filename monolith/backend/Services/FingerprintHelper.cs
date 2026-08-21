using System.Text.RegularExpressions;
using CV_Generator.Models;

namespace CV_Generator.Services;

public static partial class FingerprintHelper
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    private const int MaxPartLength = 120;

    /// <summary>
    /// Builds a dedup fingerprint from company name and position title.
    /// Normalization: trim → lowercase → collapse internal whitespace.
    /// </summary>
    public static string Compute(string companyName, string positionTitle)
        => $"{Normalize(companyName)}|{Normalize(positionTitle)}";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = WhitespaceRegex().Replace(value.Trim().ToLowerInvariant(), " ");
        return normalized.Length > MaxPartLength ? normalized[..MaxPartLength] : normalized;
    }

    /// <summary>
    /// Recomputes the fingerprint for an existing application (used when details change).
    /// </summary>
    public static string ComputeFor(Application app)
        => Compute(app.CompanyName, app.PositionTitle);
}
