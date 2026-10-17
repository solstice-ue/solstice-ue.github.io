using Solstice.Bin;

namespace Solstice.Core.Tests;

/// <summary>
/// 用 Solstice.Bin 往外部样本里追加两条卡牌，并输出结构差异报告。
/// </summary>
public class AppendDiffTests
{
    private static readonly NewAsset[] CardPair =
    [
        new("/Game/Blueprints/Cards/USA/Base/units",
            "/Script/Engine", "Blueprint",
            "/Game/Blueprints/Cards/USA/Base/units/card_unit_solstice_probe",
            "card_unit_solstice_probe", 0x02000000u,
            Tags: [new KeyValuePair<string, string>("ParentClass", "Class'/Script/Engine.Actor'")]),
        new("/Game/Blueprints/Cards/USA/Base/units",
            "/Script/Engine", "BlueprintGeneratedClass",
            "/Game/Blueprints/Cards/USA/Base/units/card_unit_solstice_probe",
            "card_unit_solstice_probe_C", 0x02000000u)
    ];

    [Fact]
    public void 追加两项并报告结构差异()
    {
        var path = Environment.GetEnvironmentVariable("SOLSTICE_PROBE_BIN");
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return;
        }

        var input = File.ReadAllBytes(path);
        var before = BinFile.Load(input);
        var output = before.AppendAssets(CardPair);
        var after = BinFile.Load(output);

        Console.WriteLine($"[diff] 输入 {input.Length:N0} → 输出 {output.Length:N0}  (+{output.Length - input.Length:N0})");
        Console.WriteLine($"[diff] name 条数 {before.Layout.NameCount:N0} → {after.Layout.NameCount:N0}  (+{after.Layout.NameCount - before.Layout.NameCount})");
        Console.WriteLine($"[diff] store 大小 {before.Layout.StoreEnd - before.Layout.NameBatchEnd:N0} → {after.Layout.StoreEnd - after.Layout.NameBatchEnd:N0}  (+{(after.Layout.StoreEnd - after.Layout.NameBatchEnd) - (before.Layout.StoreEnd - before.Layout.NameBatchEnd):N0})");
        Console.WriteLine($"[diff] 资产数 {before.AssetCount:N0} → {after.AssetCount:N0}  (+{after.AssetCount - before.AssetCount})");
        Console.WriteLine($"[diff] 新 nameBatchEnd={after.Layout.NameBatchEnd:N0} 新 storeEnd={after.Layout.StoreEnd:N0}");

        // 新名字
        var oldNames = NameBatch.Parse(input, before.Layout.HeaderEnd, out _).Names.ToHashSet(StringComparer.Ordinal);
        var newNames = NameBatch.Parse(output, after.Layout.HeaderEnd, out _).Names;
        var added = newNames.Where(n => !oldNames.Contains(n)).ToArray();
        Console.WriteLine($"[diff] 新增 name {added.Length} 个:");
        foreach (var n in added)
        {
            Console.WriteLine($"    + {n}");
        }

        // 读回
        var doc = Solstice.Core.AssetRegistry.AssetRegistryDocument.Load(output);
        Console.WriteLine($"[diff] 读回 {doc.Count:N0} 条");
        foreach (var e in doc.GetEntries().Where(e => e.AssetName.Contains("solstice_probe")))
        {
            Console.WriteLine($"    = {e.ObjectPath}  class={e.ClassPackage}.{e.AssetClass}");
        }

        try
        {
            var outPath = Path.Combine(@"D:\files\projects", "AssetRegistry.appended.bin");
            File.WriteAllBytes(outPath, output);
            Console.WriteLine($"[diff] 输出文件: {outPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[diff] 写输出文件失败: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
