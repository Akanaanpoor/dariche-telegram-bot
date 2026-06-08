using System.Security.Cryptography;
using System.Text;

namespace Dariche.Shared.Agent;

public static class HmacSigner
{
    public static string ComputeSignature(string secret, string method, string path, string timestamp, string nonce, string body)
    {
        var data = $"{method}\n{path}\n{timestamp}\n{nonce}\n{body}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Convert.ToBase64String(hash);
    }

    public static bool FixedTimeEquals(string expected, string actual)
    {
        if (expected.Length != actual.Length)
            return false;
            
        var result = 0;
        for (var i = 0; i < expected.Length; i++)
            result |= expected[i] ^ actual[i];
            
        return result == 0;
    }
}