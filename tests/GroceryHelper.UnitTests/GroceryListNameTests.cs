using GroceryHelper.Api.Lists;

namespace GroceryHelper.UnitTests;

public class GroceryListNameTests
{
    [Test]
    [Arguments("  Weekly\t groceries  ", "Weekly groceries")]
    [Arguments("Produce", "Produce")]
    public async Task Valid_list_names_are_normalized(string input, string expected)
    {
        // Arrange

        // Act
        var result = GroceryListName.Create(input);

        // Assert
        await Assert.That(result.IsValid).IsTrue();
        await Assert.That(result.Value?.Value).IsEqualTo(expected);
        await Assert.That(result.Error).IsNull();
    }

    [Test]
    [Arguments(null, "A list name is required.")]
    [Arguments(" \t ", "A list name is required.")]
    [Arguments("Weekly\ngroceries", "A list name cannot contain line breaks or control characters.")]
    [Arguments("Weekly\0groceries", "A list name cannot contain line breaks or control characters.")]
    [Arguments("Weekly\u2028groceries", "A list name cannot contain line breaks or control characters.")]
    [Arguments("Weekly\u2029groceries", "A list name cannot contain line breaks or control characters.")]
    public async Task Missing_or_control_character_names_are_rejected(
        string? input,
        string expectedError)
    {
        // Arrange

        // Act
        var result = GroceryListName.Create(input);

        // Assert
        await Assert.That(result.IsValid).IsFalse();
        await Assert.That(result.Value).IsNull();
        await Assert.That(result.Error).IsEqualTo(expectedError);
    }

    [Test]
    public async Task Normalized_name_must_not_exceed_250_utf16_code_units()
    {
        // Arrange
        var permittedInput = new string('a', 248) + "😀";
        var excessiveInput = new string('a', 249) + "😀";

        // Act
        var permitted = GroceryListName.Create(permittedInput);
        var excessive = GroceryListName.Create(excessiveInput);

        // Assert
        await Assert.That(permitted.IsValid).IsTrue();
        await Assert.That(excessive.IsValid).IsFalse();
        await Assert.That(excessive.Error)
            .IsEqualTo("A list name cannot exceed 250 UTF-16 code units.");
    }
}
