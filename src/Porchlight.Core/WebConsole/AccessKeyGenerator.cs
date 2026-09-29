using System.Security.Cryptography;
using System.Text;

namespace Porchlight.Core.WebConsole;

/// <summary>Creates and checks the web console's access key: a random secret a browser must send
/// (as <c>Authorization: Bearer &lt;key&gt;</c>) before it is given any stats.</summary>
public static class AccessKeyGenerator
{
    /// <summary>128 bits of entropy - far beyond anything guessable over a network.</summary>
    private const int KeyBytes = 16;

    /// <summary>A new random key: 32 lowercase hex characters, safe to put in a URL.</summary>
    public static string Generate() => Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(KeyBytes));

    /// <summary>Whether <paramref name="provided"/> equals <paramref name="expected"/>, compared in
    /// constant time so response timing does not leak how much of a guess was right. An empty
    /// <paramref name="expected"/> key never matches anything.</summary>
    public static bool Matches(string expected, string? provided)
    {
        if (string.IsNullOrEmpty(expected) || provided is null)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(provided));
    }
}
