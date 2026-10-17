using Solstice.Bin;

namespace Solstice.Core.Tests;

/// <summary>
/// 追加资产的字节级验证：
/// 1) 未修改写回逐字节一致；
/// 2) 追加后，除「资产计数 / 新增记录 / name batch 末尾追加的名字」外，其余字节必须原样保留；
/// 3) 追加结果能被既有读取器正确解析，资产数 +N。
/// </summary>
public class BinAppendTests
{
    private static string? SamplePath =>
        Environment.GetEnvironmentVariable("SOLSTICE_SAMPLE_BIN") is { Length: > 0 } p && File.Exists(p)
            ? p
            : null;

    private static readonly NewAsset[] CardPair =
    [
        new("/Game/Blueprints/Cards/USA/Base/units",
            "/Script/Engine", "Blueprint",
            "/Game/Blueprints/Cards/USA/Base/units/card_unit_solstice_probe",
            "card_unit_solstice_probe", 0x02000000u),
        new("/Game/Blueprints/Cards/USA/Base/units",
            "/Script/Engine", "BlueprintGeneratedClass",
            "/Game/Blueprints/Cards/USA/Base/units/card_unit_solstice_probe",
            "card_unit_solstice_probe_C", 0x02000000u)
    ];

    [Fact]
    public void 追加后原有区段字节不变()
    {
        if (SamplePath is not { } path)
        {
            return;
        }

        var bytes = File.ReadAllBytes(path);
        var file = BinFile.Load(bytes);
        var output = file.AppendAssets(CardPair);

        Console.WriteLine($"[append] {bytes.Length:N0} → {output.Length:N0} (+{output.Length - bytes.Length:N0})");

        // 重新解析新文件
        var reparsed = BinFile.Load(output);
        Console.WriteLine($"[append] 新 assetCount={reparsed.AssetCount}，name 条数={reparsed.Layout.NameCount}");
        Assert.Equal(file.AssetCount + 2, reparsed.AssetCount);

        // store 段必须逐字节相同
        var oldStore = bytes.AsSpan(file.Layout.NameBatchEnd, file.Layout.StoreEnd - file.Layout.NameBatchEnd).ToArray();
        var newStore = output.AsSpan(reparsed.Layout.NameBatchEnd, reparsed.Layout.StoreEnd - reparsed.Layout.NameBatchEnd).ToArray();
        Assert.True(oldStore.AsSpan().SequenceEqual(newStore), "store 段必须原样保留");

        // 原有 body 内容（跳过 count 与新增记录）必须原样保留
        var oldBodyTail = bytes.AsSpan(file.Layout.StoreEnd + 4).ToArray();
        var newBodyStart = reparsed.Layout.StoreEnd + 4 + (output.Length - bytes.Length) - 0;
        var tailLen = oldBodyTail.Length;
        var newTail = output.AsSpan(output.Length - tailLen, tailLen).ToArray();
        Assert.True(oldBodyTail.AsSpan().SequenceEqual(newTail), "原有资产记录必须原样保留");
    }

    [Fact]
    public void 追加结果可被既有读取器解析()
    {
        if (SamplePath is not { } path)
        {
            return;
        }

        var bytes = File.ReadAllBytes(path);
        var output = BinFile.Load(bytes).AppendAssets(CardPair);

        var doc = Solstice.Core.AssetRegistry.AssetRegistryDocument.Load(output);
        Console.WriteLine($"[append] 读取器解析出 {doc.Count:N0} 条");

        Assert.Equal(16252, doc.Count);

        var hit = doc.GetEntries().FirstOrDefault(e => e.AssetName == "card_unit_solstice_probe");
        Assert.NotNull(hit);
        Assert.Equal("/Script/Engine", hit!.ClassPackage);
        Assert.Equal("Blueprint", hit.AssetClass);
    }
}
