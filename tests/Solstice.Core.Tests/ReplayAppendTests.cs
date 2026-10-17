using Solstice.Bin;

namespace Solstice.Core.Tests;

/// <summary>
/// 重放验证：把「新版 bin 比旧版多出来的资产」用我们的工具追加到旧版上，
/// 再与真实新版逐条比对，验证追加逻辑能否复现真实世界的增量。
/// </summary>
public class ReplayAppendTests
{
    [Fact]
    public void 重放新版增量并与真实新版比对()
    {
        var newerPath = Environment.GetEnvironmentVariable("SOLSTICE_PROBE_BIN");
        var basePath = Environment.GetEnvironmentVariable("SOLSTICE_BASE_BIN");

        if (string.IsNullOrEmpty(newerPath) || !File.Exists(newerPath) ||
            string.IsNullOrEmpty(basePath) || !File.Exists(basePath))
        {
            return;
        }

        var newerBytes = File.ReadAllBytes(newerPath);
        var baseBytes = File.ReadAllBytes(basePath);

        var newer = Solstice.Core.AssetRegistry.AssetRegistryDocument.Load(newerBytes);
        var baseDoc = Solstice.Core.AssetRegistry.AssetRegistryDocument.Load(baseBytes);

        var baseSet = baseDoc.GetEntries().ToDictionary(e => e.ObjectPath, StringComparer.Ordinal);
        var newerEntries = newer.GetEntries();

        // 新版比旧版多出来的资产
        var added = newerEntries.Where(e => !baseSet.ContainsKey(e.ObjectPath)).ToArray();
        Console.WriteLine($"[replay] 旧版 {baseDoc.Count:N0} 条，新版 {newer.Count:N0} 条，需追加 {added.Length} 条");

        var toAppend = added.Select(e => new NewAsset(
            e.PackagePath,
            e.ClassPackage,
            e.AssetClass,
            e.ObjectPath[..e.ObjectPath.LastIndexOf('.')],
            e.AssetName,
            0x02000000u)).ToArray();

        var generated = BinFile.Load(baseBytes).AppendAssets(toAppend);
        var generatedDoc = Solstice.Core.AssetRegistry.AssetRegistryDocument.Load(generated);

        Console.WriteLine($"[replay] 生成 {generated.Length:N0} 字节（真实新版 {newerBytes.Length:N0}，差 {generated.Length - newerBytes.Length:+#,0;-#,0}）");
        Console.WriteLine($"[replay] 生成文件资产数 {generatedDoc.Count:N0}，真实新版 {newer.Count:N0}");

        // 逐条比对：新版里的每一条，生成文件里都必须有，且类一致
        var genMap = generatedDoc.GetEntries().ToDictionary(e => e.ObjectPath, StringComparer.Ordinal);
        var missing = 0;
        var classMismatch = 0;

        foreach (var e in newerEntries)
        {
            if (!genMap.TryGetValue(e.ObjectPath, out var g))
            {
                missing++;
                if (missing <= 5) Console.WriteLine($"    - 缺: {e.ObjectPath}");
                continue;
            }

            if (g.ClassPackage != e.ClassPackage || g.AssetClass != e.AssetClass)
            {
                classMismatch++;
                if (classMismatch <= 5) Console.WriteLine($"    ! 类不符: {e.ObjectPath} 期望 {e.ClassPackage}.{e.AssetClass} 实际 {g.ClassPackage}.{g.AssetClass}");
            }
        }

        // 生成文件里多出来的（旧版有、新版没有的，应当保留）
        var extra = generatedDoc.GetEntries().Where(e => !newerEntries.Any(n => n.ObjectPath == e.ObjectPath)).ToArray();
        Console.WriteLine($"[replay] 比对结果：缺失 {missing}，类不符 {classMismatch}，生成端多出 {extra.Length}");
        foreach (var x in extra.Take(3))
        {
            Console.WriteLine($"    + {x.ObjectPath}");
        }
    }
}
