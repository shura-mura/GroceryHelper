namespace GroceryHelper.UnitTests;

// Infrastructure smoke only. Business-rule unit tests arrive with feature issues.
public class RuntimeSmokeTests
{
    [Test]
    public async Task Test_process_uses_the_supported_runtime()
    {
        // Arrange
        const int supportedMajorVersion = 10;

        // Act
        var actualMajorVersion = Environment.Version.Major;

        // Assert
        await Assert.That(actualMajorVersion).IsEqualTo(supportedMajorVersion);
    }
}
