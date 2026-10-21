using System.Globalization;
using System.Text.RegularExpressions;

namespace GroceryHelper.Api.Lists;

public sealed record GroceryListName
{
    private static readonly Regex SpacesAndTabs = new("[ \\t]+", RegexOptions.CultureInvariant);

    private GroceryListName(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public static GroceryListNameResult Create(string? input)
    {
        if (input?.Any(IsRejectedCharacter) is true)
        {
            return new GroceryListNameResult(
                null,
                "A list name cannot contain line breaks or control characters.");
        }

        var normalized = SpacesAndTabs.Replace(input?.Trim() ?? string.Empty, " ");
        if (normalized.Length == 0)
        {
            return new GroceryListNameResult(null, "A list name is required.");
        }

        if (normalized.Length > 250)
        {
            return new GroceryListNameResult(
                null,
                "A list name cannot exceed 250 UTF-16 code units.");
        }

        return new GroceryListNameResult(new GroceryListName(normalized), null);
    }

    private static bool IsRejectedCharacter(char character) =>
        (char.IsControl(character) && character != '\t') ||
        char.GetUnicodeCategory(character) is
            UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;
}

public sealed record GroceryListNameResult(GroceryListName? Value, string? Error)
{
    public bool IsValid => Value is not null;
}
