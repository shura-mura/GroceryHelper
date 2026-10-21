using GroceryHelper.Api.Authentication;

namespace GroceryHelper.UnitTests;

public class DemoAccountAuthenticatorTests
{
    [Test]
    [Arguments("demo")]
    [Arguments(" DEMO ")]
    public async Task Documented_credentials_authenticate_with_normalized_username(
        string username)
    {
        // Arrange
        var authenticator = new DemoAccountAuthenticator();

        // Act
        var authenticated = authenticator.Authenticate(username, "GroceryDemo1!");

        // Assert
        await Assert.That(authenticated).IsTrue();
    }

    [Test]
    [Arguments("other", "GroceryDemo1!")]
    [Arguments("demo", "grocerydemo1!")]
    [Arguments("demo", "GroceryDemo1! ")]
    public async Task Username_or_password_mismatch_is_rejected(
        string username,
        string password)
    {
        // Arrange
        var authenticator = new DemoAccountAuthenticator();

        // Act
        var authenticated = authenticator.Authenticate(username, password);

        // Assert
        await Assert.That(authenticated).IsFalse();
    }
}
