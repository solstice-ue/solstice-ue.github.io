using Solstice.Core.AssetRegistry;

namespace Solstice.Core.Tests;

/// <summary>
/// 真实样本的往返测试。
///
/// <para>
/// 样本是游戏资产，**不进仓库**。通过环境变量 <c>SOLSTICE_SAMPLE_BIN</c> 指向本地文件；
/// 没设置就跳过（返回而不是失败），这样 CI 上也能稳定通过。
/// </para>
/// </summary>
public class AssetRegistryDocumentTests
{
    private static string? SamplePath =>
        Environment.GetEnvironmentVariable("SOLSTICE_SAMPLE_BIN") is { Length: > 0 } p && File.Exists(p)
            ? p
            : null;

    [Fact]
    public void 真实样本_解析条数稳定()
    {
        if (SamplePath is not { } path)
        {
            return; // 无样本，跳过
        }

        var doc = AssetRegistryDocument.Load(File.ReadAllBytes(path));

        Assert.Equal(16250, doc.Count);
    }

    [Fact]
    public void 真实样本_新增一条后写回再读回()
    {
        if (SamplePath is not { } path)
        {
            return;
        }

        var original = File.ReadAllBytes(path);
        var doc = AssetRegistryDocument.Load(original);
        var before = doc.Count;

        const string assetName = "card_unit_solstice_probe";
        var added = doc.AddAsset(
            packagePath: "/Game/Blueprints/Cards/USA/Base/units",
            assetName: assetName,
            assetClass: "Blueprint",
            tags: new Dictionary<string, string> { ["ParentClass"] = "Class'/Script/Engine.Actor'" });

        Assert.True(added);

        var bytes = doc.Save();
        Assert.True(bytes.Length > 0);

        // 读回：新条目在，且总数 +1
        var reloaded = AssetRegistryDocument.Load(bytes);
        Assert.Equal(before + 1, reloaded.Count);

        var hit = reloaded.GetEntries()
            .FirstOrDefault(e => e.AssetName == assetName);
        Assert.NotNull(hit);
        Assert.Equal("Blueprint", hit!.AssetClass);
        // PackagePath 是所在目录，不含资产名
        Assert.Equal("/Game/Blueprints/Cards/USA/Base/units", hit.PackagePath);

        // 重复添加应被拒绝
        Assert.False(reloaded.AddAsset(
            packagePath: "/Game/Blueprints/Cards/USA/Base/units",
            assetName: assetName,
            assetClass: "Blueprint"));
    }
}
