using Solstice.Core.AssetRegistry;

namespace Solstice.Core.Tests;

/// <summary>
/// 写回保真度检查：证明「文件变小」不等于「丢数据」。
///
/// 写回会把 tags 统一重编码为 AnsiString（原文件可能是 WideString），
/// 所以字节数必然变化；但**逐条语义**必须完全一致。
/// </summary>
public class AssetRegistryRoundTripFidelityTests
{
    private static string? SamplePath =>
        Environment.GetEnvironmentVariable("SOLSTICE_SAMPLE_BIN") is { Length: > 0 } p && File.Exists(p)
            ? p
            : null;

    [Fact]
    public void 写回后原有条目逐条一致()
    {
        if (SamplePath is not { } path)
        {
            return; // 无样本，跳过
        }

        var original = File.ReadAllBytes(path);
        var doc = AssetRegistryDocument.Load(original);
        var beforeSnapshot = doc.Snapshot().ToHashSet(StringComparer.Ordinal);

        doc.AddAsset(
            packagePath: "/Game/Blueprints/Cards/USA/Base/units",
            assetName: "card_unit_fidelity_probe",
            assetClass: "Blueprint",
            tags: null);

        var output = doc.Save();
        var reloaded = AssetRegistryDocument.Load(output);
        var afterSnapshot = reloaded.Snapshot().ToHashSet(StringComparer.Ordinal);

        // 1) 新增的那条必须在
        Assert.Contains(afterSnapshot, s => s.Contains("card_unit_fidelity_probe"));

        // 2) 原有每一条都必须原样还在（tags / ChunkIDs / PackageFlags 全部一致）
        var missing = beforeSnapshot.Except(afterSnapshot, StringComparer.Ordinal).ToArray();

        Console.WriteLine($"[fidelity] 原始 {original.Length:N0} B → 输出 {output.Length:N0} B " +
                          $"({(output.Length - original.Length) * 100.0 / original.Length:F1}%)");
        Console.WriteLine($"[fidelity] 原始条目 {beforeSnapshot.Count}，输出条目 {afterSnapshot.Count}，丢失 {missing.Length}");

        if (missing.Length > 0)
        {
            Console.WriteLine("[fidelity] 丢失样例：");
            foreach (var m in missing.Take(5))
            {
                Console.WriteLine("  " + m.Replace("\u0001", " ‖ "));
            }
        }

        Assert.Empty(missing);
        Assert.Equal(beforeSnapshot.Count + 1, afterSnapshot.Count);
    }
}
