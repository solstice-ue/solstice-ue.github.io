using Solstice.Core.Kismet;

namespace Solstice.Core.Tests;

public class FunctionCatalogTests
{
    private const string TablePath = @"D:\files\projects\jmap-out\functions.kards.json";
    private const string TablePathFull = @"D:\files\projects\jmap-out\functions.json";

    [Fact]
    public void 解析函数表并匹配导入的owner()
    {
        if (!TestAssets.Available) { Console.WriteLine("[fc] 跳过"); return; }
        if (!File.Exists(TablePathFull)) { Console.WriteLine("[fc] 跳过：无函数表"); return; }

        var copy = TestAssets.Prepare();
        var ws = KismetWorkspace.Open(
            File.ReadAllBytes(copy.AssetPath),
            File.ReadAllBytes(Path.ChangeExtension(copy.AssetPath, ".uexp")),
            File.ReadAllBytes(copy.UsmapPath),
            Path.Combine(Path.GetDirectoryName(copy.AssetPath)!, "fc"));

        var catalog = FunctionCatalog.Load(File.ReadAllText(TablePathFull));
        ws.AttachCatalog(catalog);
        Console.WriteLine($"[fc] 函数表 {catalog.Count} 条签名");

        foreach (var name in new[] { "IsValid", "EqualEqual_ByteByte", "GetOrSpawnActor" })
        {
            var owner = ws.FunctionOwnerPath(name);
            var sig = ws.SignatureOf(name);
            Console.WriteLine($"[fc] {name}");
            Console.WriteLine($"[fc]   owner={owner}");
            if (sig is null) { Console.WriteLine("[fc]   无签名"); continue; }
            Console.WriteLine($"[fc]   flags={sig.Flags}");
            Console.WriteLine($"[fc]   static={sig.IsStatic}  入参={sig.Inputs.Count} 出参={sig.Outputs.Count}");
            foreach (var p in sig.Params)
            {
                Console.WriteLine($"[fc]     {p.Dir,-7} {p.Name,-24} {p.Type}");
            }
        }
    }
}
