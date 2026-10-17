using System.Security.Cryptography;

namespace Solstice.Core.IO;

/// <summary>
/// 文件指纹工具。用于在界面上如实展示「我到底处理了哪个文件」，
/// 以及在写回校验时确认输出确实是新文件。
/// </summary>
public static class FileFingerprint
{
    /// <summary>计算字节数组的 SHA-256，返回大写十六进制字符串。</summary>
    public static string Sha256(ReadOnlySpan<byte> data) =>
        Convert.ToHexString(SHA256.HashData(data));

    /// <summary>计算流内容的 SHA-256，返回大写十六进制字符串。</summary>
    public static async Task<string> Sha256Async(Stream stream, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken).ConfigureAwait(false);
        return Convert.ToHexString(hash);
    }
}
