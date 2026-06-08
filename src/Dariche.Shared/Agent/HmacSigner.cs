using System.Security.Cryptography;
using System.Text;

namespace Dariche.Shared.Agent;

public static class HmacSigner
{
    public static string ComputeSignature(string secret, string method, string pathAndQuery, string timestamp, string nonce, string body)
    {
        var canonical = string.Join("", method.ToUpperInvariant(), pathAndQuery, timestamp, nonce, body ?? string.Empty);
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left ?? string.Empty);
        var rightBytes = Encoding.UTF8.GetBytes(right ?? string.Empty);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
