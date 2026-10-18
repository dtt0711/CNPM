using System.Security.Cryptography;
using System.Text;

namespace TourismHcm.Domain.Helpers;

public static class ContentHash
{
    public static string Compute(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
