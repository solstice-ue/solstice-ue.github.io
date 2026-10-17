using Solstice.Bin;

namespace Solstice.Core.Tests;

/// <summary>
/// 外部样本探针：通过环境变量 SOLSTICE_PROBE_BIN 指向任意 bin，打印分段诊断。
/// 不参与常规断言，只用于人工排查。
/// </summary>
public class ExternalBinProbeTests
{
    [Fact]
    public void 探针()
    {
        var path = Environment.GetEnvironmentVariable("SOLSTICE_PROBE_BIN");
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        var bytes = File.ReadAllBytes(path);
        Console.WriteLine($"[probe] size={bytes.Length:N0}");

        if (!BinLayout.TryParse(bytes, out var layout, out var error))
        {
            Console.WriteLine($"[probe] 分段失败: {error}");
            return;
        }

        Console.WriteLine($"[probe] version={layout!.Version} filterEditorOnly={layout.FilterEditorOnly}");
        Console.WriteLine($"[probe] nameCount={layout.NameCount:N0} numStringBytes={layout.NumStringBytes:N0} hashVersion=0x{layout.HashVersion:X}");
        Console.WriteLine($"[probe] headerEnd={layout.HeaderEnd} nameBatchEnd={layout.NameBatchEnd:N0} storeEnd={layout.StoreEnd:N0} assetCount={layout.AssetCount:N0}");

        // name 哈希校验
        var mism = Solstice.AssetRegistry.NameBatchRebuilder.CountMismatches(bytes, out var hashErr, out var n);
        Console.WriteLine($"[probe] name 哈希：{n:N0} 条，不匹配 {mism}，错误 {hashErr ?? "无"}");

        // store 解析
        try
        {
            var store = TagStore.Parse(bytes, layout.NameBatchEnd, layout.StoreEnd);
            var reser = store.Serialize();
            var orig = bytes.AsSpan(layout.NameBatchEnd, layout.StoreEnd - layout.NameBatchEnd).ToArray();
            Console.WriteLine($"[probe] store 解析成功：重序列化 {reser.Length:N0} vs 原始 {orig.Length:N0}，一致={reser.AsSpan().SequenceEqual(orig)}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[probe] store 解析失败: {ex.GetType().Name}: {ex.Message}");
        }

        // 用既有读取器解析资产
        try
        {
            var doc = Solstice.Core.AssetRegistry.AssetRegistryDocument.Load(bytes);
            Console.WriteLine($"[probe] 读取器解析出 {doc.Count:N0} 条资产");

            var basePath = Environment.GetEnvironmentVariable("SOLSTICE_BASE_BIN");
            if (!string.IsNullOrEmpty(basePath) && File.Exists(basePath))
            {
                var baseDoc = Solstice.Core.AssetRegistry.AssetRegistryDocument.Load(File.ReadAllBytes(basePath));
                var baseSet = baseDoc.GetEntries().Select(e => e.ObjectPath).ToHashSet(StringComparer.Ordinal);
                var mine = doc.GetEntries().Select(e => e.ObjectPath).ToHashSet(StringComparer.Ordinal);

                var added = mine.Except(baseSet, StringComparer.Ordinal).ToArray();
                var removed = baseSet.Except(mine, StringComparer.Ordinal).ToArray();

                Console.WriteLine($"[diff] 新增 {added.Length}，删除 {removed.Length}");
                foreach (var a in added.Take(10))
                {
                    Console.WriteLine($"  + {a}");
                }

                foreach (var r in removed.Take(5))
                {
                    Console.WriteLine($"  - {r}");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[probe] 读取器失败: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
