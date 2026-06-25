using System.Security.Cryptography;

namespace ApiServer.Shared.Security;

/// <summary>
/// 외부로 내려줄 "원문 토큰" 생성기.
/// - RefreshToken은 Opaque(랜덤 문자열) 권장.
/// </summary>
public static class RandomTokenGenerator
{
    /// <summary>
    /// base64url(패딩/+/ 제거) 형태의 랜덤 토큰 생성.
    /// </summary>
    public static string CreateBase64UrlToken(int byteLength = 32)
    {
        if (byteLength <= 0) throw new ArgumentOutOfRangeException(nameof(byteLength));

        Span<byte> bytes = stackalloc byte[Math.Min(byteLength, 256)];
        byte[]? rented = null;

        try
        {
            if (byteLength > bytes.Length)
            {
                rented = new byte[byteLength];
                RandomNumberGenerator.Fill(rented);
                return Base64UrlEncode(rented);
            }

            bytes = bytes[..byteLength];
            RandomNumberGenerator.Fill(bytes);
            return Base64UrlEncode(bytes);
        }
        finally
        {
            if (rented != null) Array.Clear(rented, 0, rented.Length);
        }
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> data)
    {
        // Convert.ToBase64String은 Span overload가 .NET 버전별로 다를 수 있어 배열로 처리
        var b64 = Convert.ToBase64String(data.ToArray());
        // base64url
        return b64.Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
