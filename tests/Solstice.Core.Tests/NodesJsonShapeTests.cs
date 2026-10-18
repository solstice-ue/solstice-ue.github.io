using System.Text.Json;
using Solstice.Core.Kismet;

namespace Solstice.Core.Tests;

public class NodesJsonShapeTests
{
    [Fact]
    public void 页面生成的JSON可被解析()
    {
        // 与 Wasm.razor 的 ToJson/NodesJson 完全一致的构造
        static string Esc(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        static string? ToJson(string kind, string var, int value, string fn, string owner) => kind switch
        {
            "return" => "{ \"kind\": \"return\" }",
            "assign" => $"{{ \"kind\": \"assign\", \"var\": \"{Esc(var)}\", \"value\": {value} }}",
            "assign-bool" => $"{{ \"kind\": \"assign-bool\", \"var\": \"{Esc(var)}\", \"value\": {value != 0} }}",
            "call" => $"{{ \"kind\": \"call\", \"function\": \"{Esc(fn)}\"{(string.IsNullOrWhiteSpace(owner) ? "" : $", \"owner\": \"{Esc(owner)}\"")}, \"args\": [] }}",
            "context-call" => $"{{ \"kind\": \"context-call\", \"object\": \"{Esc(var)}\", \"function\": \"{Esc(fn)}\"{(string.IsNullOrWhiteSpace(owner) ? "" : $", \"owner\": \"{Esc(owner)}\"")}, \"args\": [] }}",
            "virtual-call" => $"{{ \"kind\": \"virtual-call\", \"function\": \"{Esc(fn)}\", \"args\": [] }}",
            _ => null
        };

        var items = new[]
        {
            ToJson("assign", "EntryPoint", 5, "", ""),
            ToJson("return", "", 0, "", "")
        };
        var json = "{\n  \"nodes\": [\n    " + string.Join(",\n    ", items) + "\n  ]\n}";
        Console.WriteLine("[json] " + json.Replace("\n", "\\n"));

        var nodes = KismetWorkspace.ParseNodes(json);
        Console.WriteLine($"[json] 解析出 {nodes.Count} 个节点");
        Assert.Equal(2, nodes.Count);
    }
}
