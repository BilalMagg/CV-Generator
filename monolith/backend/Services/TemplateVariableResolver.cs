using System.Text.RegularExpressions;
using CV_Generator.Dto;
using CV_Generator.Models;

namespace CV_Generator.Services;

/// <summary>
/// Resolves {{token}} placeholders in generic schedule templates. Supported tokens:
/// company_name, company_description, my_name, my_phone, my_email.
/// A value missing from the company/user profile falls back to the template's defaults;
/// if still missing it is replaced with an empty string.
/// </summary>
public static class TemplateVariableResolver
{
    private static readonly Regex Token = new(@"\{\{\s*(\w+)\s*\}\}", RegexOptions.Compiled);

    public static string Render(string template, ScheduleVariableDefaults? defaults, Company? company, User user)
    {
        if (string.IsNullOrEmpty(template)) return template;

        var myName = $"{user.FirstName} {user.LastName}".Trim();
        var lookup = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["company_name"] = company?.Name,
            ["company_description"] = company?.Description,
            ["my_name"] = string.IsNullOrWhiteSpace(myName) ? defaults?.MyName : myName,
            ["my_phone"] = user.PhoneNumber,
            ["my_email"] = user.Email,
        };

        return Token.Replace(template, m =>
        {
            var key = m.Groups[1].Value;
            if (!lookup.TryGetValue(key, out var value))
                return m.Value; // leave unknown tokens untouched

            var resolved = string.IsNullOrWhiteSpace(value)
                ? key.ToLowerInvariant() switch
                {
                    "company_name" => defaults?.CompanyName,
                    "company_description" => defaults?.CompanyDescription,
                    "my_name" => defaults?.MyName,
                    "my_phone" => defaults?.MyPhone,
                    "my_email" => defaults?.MyEmail,
                    _ => null
                }
                : value;

            return string.IsNullOrEmpty(resolved) ? string.Empty : resolved!;
        });
    }
}