using System.Security.Cryptography;

namespace GroceryHelper.Api.Authentication;

public sealed class SessionStore
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(8);

    private readonly Lock sync = new();
    private readonly Dictionary<string, Session> sessions = [];

    public CreatedSession Create(DateTimeOffset now)
    {
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var expiresAt = now.Add(Lifetime);
        lock (sync)
        {
            sessions[Hash(token)] = new Session(expiresAt);
        }

        return new CreatedSession(token, expiresAt);
    }

    public bool IsValid(string? token, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        lock (sync)
        {
            return sessions.TryGetValue(Hash(token), out var session) &&
                session.ExpiresAt > now;
        }
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private sealed record Session(DateTimeOffset ExpiresAt);
}

public sealed record CreatedSession(string Token, DateTimeOffset ExpiresAt);
