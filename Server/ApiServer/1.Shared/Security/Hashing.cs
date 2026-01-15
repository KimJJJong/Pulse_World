using System.Security.Cryptography;
using System.Text;

namespace ApiServer.Shared.Security;

/// <summary>
/// 서버 저장용 해시.
/// - RefreshToken 원문은 저장하지 않고, hash만 DB에 저장.
/// - pepper(서버 비밀) 섞어서 해시하면 DB 유출에도 더 강함.
/// </summary>
public static class Hashing
{
    public static string Sha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return ConvertToHex(hash);
    }

    public static string Sha256HexWithPepper(string input, string pepper)
    {
        // pepper는 서버 비밀(환경변수/secret)로 관리 권장
        return Sha256Hex($"{pepper}|{input}");
    }

    private static string ConvertToHex(ReadOnlySpan<byte> bytes)
    {
        // .NET 표준 hex
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
