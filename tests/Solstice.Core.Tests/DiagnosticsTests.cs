using Solstice.Core.AssetRegistry;

namespace Solstice.Core.Tests;

/// <summary>
/// 诊断用：量出「写回」到底改了什么，以及卡牌资产的双条目规律。
/// 这些输出是设计输入，不是断言。
/// </summary>
public class DiagnosticsTests
{
    private static string? SamplePath =>
        Environment.GetEnvironmentVariable("SOLSTICE_SAMPLE_BIN") is { Length: > 0 } p && File.Exists(p)
            ? p
            : null;

    [Fact]
    public void 无修改写回_与原文件差多少字节()
    {
        if (SamplePath is not { } path)
        {
            return;
        }

        var original = File.ReadAllBytes(path);
        var doc = AssetRegistryDocument.Load(original);
        var output = doc.Save();

        Console.WriteLine($"[bytes] 原始 {original.Length:N0} / 输出 {output.Length:N0} / 差 {output.Length - original.Length:+#,0;-#,0}");

        var min = Math.Min(original.Length, output.Length);
        var firstDiff = -1;
        for (var i = 0; i < min; i++)
        {
            if (original[i] != output[i])
            {
                firstDiff = i;
                break;
            }
        }

        if (firstDiff < 0 && original.Length == output.Length)
        {
            Console.WriteLine("[bytes] 完全相同");
            return;
        }

        var differing = 0;
        for (var i = 0; i < min; i++)
        {
            if (original[i] != output[i])
            {
                differing++;
            }
        }

        Console.WriteLine($"[bytes] 首个差异 @0x{firstDiff:X}；前 {min:N0} 字节中 {differing:N0} 个不同");
        if (firstDiff >= 0)
        {
            var ctx = Math.Min(24, min - firstDiff);
            Console.WriteLine($"[bytes] 原始: {Convert.ToHexString(original, firstDiff, ctx)}");
            Console.WriteLine($"[bytes] 输出: {Convert.ToHexString(output, firstDiff, ctx)}");
        }
    }

    [Fact]
    public void 写回依赖的版本字段()
    {
        if (SamplePath is not { } path)
        {
            return;
        }

        var doc = Solstice.Core.AssetRegistry.AssetRegistryDocument.Load(File.ReadAllBytes(path));
        Console.WriteLine($"[ver] RegistryVersion={doc.RegistryVersion}  ObjectVersion={doc.ObjectVersion}");
        Console.WriteLine($"[ver] 首条 ChunkIDs={doc.FirstChunkIds}  PackageFlags={doc.FirstPackageFlags}");
    }

    [Fact]
    public void 卡牌资产的双条目规律()
    {
        if (SamplePath is not { } path)
        {
            return;
        }

        var entries = AssetRegistryDocument.Load(File.ReadAllBytes(path)).GetEntries();
        var byPath = entries.Select(e => e.ObjectPath).ToHashSet(StringComparer.Ordinal);

        // 形如 .../BP_Cxxx.BP_Cxxx 的条目，看是否存在 .../BPxxx.BPxxx
        var candidates = entries
            .Where(e => e.AssetName.StartsWith("BP_C", StringComparison.Ordinal))
            .ToArray();

        var paired = 0;
        var unpaired = new List<string>();
        foreach (var c in candidates)
        {
            var twin = c.AssetName["BP_C".Length..];
            var twinPath = c.ObjectPath.Replace("BP_C" + twin, "BP" + twin);
            if (byPath.Contains(twinPath))
            {
                paired++;
            }
            else
            {
                unpaired.Add(c.ObjectPath);
            }
        }

        Console.WriteLine($"[bp] 总数 {entries.Count}；BP_C* 条目 {candidates.Length}；配对成功 {paired}；未配对 {unpaired.Count}");
        foreach (var e in candidates.Take(3))
        {
            Console.WriteLine($"[bp] 样例 {e.ObjectPath}  ({e.AssetClass})");
        }
        foreach (var u in unpaired.Take(3))
        {
            Console.WriteLine($"[bp] 未配对 {u}");
        }

        // 再看 card_* 资产：它们是否也成对
        var cards = entries.Where(e => e.AssetName.StartsWith("card_", StringComparison.Ordinal)).ToArray();
        Console.WriteLine($"[bp] card_* 条目 {cards.Length}");
        foreach (var e in cards.Take(3))
        {
            Console.WriteLine($"[bp] 卡牌 {e.ObjectPath}  ({e.AssetClass})");
        }

        // 类名分布，帮助判断两张表各自的 AssetClass
        foreach (var g in candidates.GroupBy(c => c.AssetClass).OrderByDescending(g => g.Count()))
        {
            Console.WriteLine($"[bp] BP_C* 类名 {g.Key} × {g.Count()}");
        }
    }
}
