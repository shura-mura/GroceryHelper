using System.Security.Cryptography;

namespace GroceryHelper.Api.Authentication;

public sealed class DemoAccountAuthenticator
{
    private const int Iterations = 210_000;
    private static readonly byte[] PasswordSalt =
        Convert.FromHexString("A438B74D728F6D8535603530156176AC");
    private static readonly byte[] PasswordHash =
        Convert.FromBase64String("h0grWlQnV8tIBNG2c5v5hrcYbhdRPuTI/B7ANqbiq4U=");

    public bool Authenticate(string? username, string? password)
    {
        var usernameMatches = string.Equals(
            username?.Trim(),
            "demo",
            StringComparison.OrdinalIgnoreCase);
        var suppliedHash = Rfc2898DeriveBytes.Pbkdf2(
            password ?? string.Empty,
            PasswordSalt,
            Iterations,
            HashAlgorithmName.SHA256,
            PasswordHash.Length);
        var passwordMatches = CryptographicOperations.FixedTimeEquals(
            suppliedHash,
            PasswordHash);
        return usernameMatches & passwordMatches;
    }
}
