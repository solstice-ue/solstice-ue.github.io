using System.Text.Json;

namespace Solstice.Tools.JmapExtractor;

/// <summary>
/// 把 trumank/jmap 导出的活对象转储（JSON）抽成两份可供 UKAPI 使用的表。
///
/// <para>用法：<c>JmapExtractor &lt;jmap.json&gt; &lt;outDir&gt;</c></para>
///
/// <para>产出四份文件：</para>
/// <list type="bullet">
///   <item><c>objects.json</c> —— 对象表，含 outer / class / super_struct，用于构建 import 表；</item>
///   <item><c>objects.kards.json</c> —— 同上，只保留 4 个 Kards 包，用于缓存；</item>
///   <item><c>functions.json</c> —— 以对象完整路径为 key，函数嵌在对象里，含参数名与 in/out；</item>
///   <item><c>functions.kards.json</c> —— 同上，只保留 Kards 包。</item>
/// </list>
/// </summary>
public static class Program
{
    /// <summary>「kards 模块」指这四个脚本包。</summary>
    private static readonly string[] KardsPackages =
    [
        "/Script/kards",
        "/Script/KardsCore",
        "/Script/KardsSubsystems",
        "/Script/KardsWidgets"
    ];

    public static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("用法: JmapExtractor <jmap.json> <outDir>");
            return 2;
        }

        var inputPath = args[0];
        var outDir = args[1];

        if (!File.Exists(inputPath))
        {
            Console.Error.WriteLine($"找不到输入文件: {inputPath}");
            return 2;
        }

        Directory.CreateDirectory(outDir);

        Console.WriteLine($"[读入] {inputPath} ({new FileInfo(inputPath).Length / 1024.0 / 1024.0:F1} MB)");

        using var doc = JsonDocument.Parse(
            File.ReadAllBytes(inputPath),
            new JsonDocumentOptions { MaxDepth = 128 });

        var root = doc.RootElement;
        var metadata = root.TryGetProperty("metadata", out var md) ? md : default;
        var objectsElement = root.GetProperty("objects");

        var objects = new Dictionary<string, ObjInfo>(StringComparer.Ordinal);
        var functions = new Dictionary<string, List<FuncInfo>>(StringComparer.Ordinal);

        foreach (var prop in objectsElement.EnumerateObject())
        {
            var path = prop.Name;
            var o = prop.Value;

            var type = GetString(o, "type");
            var outer = GetString(o, "outer");
            var cls = GetString(o, "class");
            var super = GetString(o, "super_struct");

            objects[path] = new ObjInfo(type, outer, cls, super);

            if (type == "Function" && outer is not null)
            {
                var shortName = path.Contains(':') ? path[(path.LastIndexOf(':') + 1)..] : path;
                var info = new FuncInfo(
                    shortName,
                    GetString(o, "function_flags"),
                    ReadParams(o));

                if (!functions.TryGetValue(outer, out var list))
                {
                    list = [];
                    functions[outer] = list;
                }

                list.Add(info);
            }
        }

        var generatedAt = DateTimeOffset.UtcNow.ToString("O");
        var counts = new
        {
            objects = objects.Count,
            functionOwners = functions.Count,
            functions = functions.Sum(kv => kv.Value.Count)
        };

        WriteObjects(Path.Combine(outDir, "objects.json"), objects, metadata, generatedAt, counts, null);
        WriteObjects(Path.Combine(outDir, "objects.kards.json"), objects, metadata, generatedAt, counts, IsKards);
        WriteFunctions(Path.Combine(outDir, "functions.json"), objects, functions, metadata, generatedAt, counts, null);
        WriteFunctions(Path.Combine(outDir, "functions.kards.json"), objects, functions, metadata, generatedAt, counts, IsKards);

        Console.WriteLine($"[完成] 对象 {counts.objects:N0}，函数宿主 {counts.functionOwners:N0}，函数 {counts.functions:N0}");
        Console.WriteLine($"[输出] {outDir}");

        foreach (var f in new[] { "objects.json", "objects.kards.json", "functions.json", "functions.kards.json" })
        {
            var fi = new FileInfo(Path.Combine(outDir, f));
            Console.WriteLine($"   {f,-22} {fi.Length / 1024.0 / 1024.0:F2} MB");
        }

        return 0;
    }

    private static bool IsKards(string path)
    {
        foreach (var pkg in KardsPackages)
        {
            if (path.Equals(pkg, StringComparison.Ordinal) ||
                path.StartsWith(pkg + ".", StringComparison.Ordinal) ||
                path.StartsWith(pkg + ":", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? GetString(JsonElement o, string name)
        => o.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    /// <summary>
    /// 读函数参数。方向由 flags 判定：
    /// CPF_ReturnParm → return，CPF_OutParm → out，其余 → in。
    /// </summary>
    private static List<ParamInfo> ReadParams(JsonElement o)
    {
        var list = new List<ParamInfo>();

        if (!o.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var p in props.EnumerateArray())
        {
            var flags = GetString(p, "flags") ?? string.Empty;

            var dir = flags.Contains("CPF_ReturnParm", StringComparison.Ordinal) ? "return"
                : flags.Contains("CPF_OutParm", StringComparison.Ordinal) ? "out"
                : "in";

            list.Add(new ParamInfo(
                Name: GetString(p, "name") ?? string.Empty,
                Type: GetString(p, "type") ?? string.Empty,
                Dir: dir,
                Cls: GetString(p, "property_class"),
                Enum: GetString(p, "enum"),
                Size: p.TryGetProperty("size", out var s) && s.TryGetInt32(out var sv) ? sv : 0,
                Offset: p.TryGetProperty("offset", out var off) && off.TryGetInt32(out var ov) ? ov : 0));
        }

        return list;
    }

    private static void WriteObjects(
        string path,
        Dictionary<string, ObjInfo> objects,
        JsonElement metadata,
        string generatedAt,
        object counts,
        Func<string, bool>? filter)
    {
        using var stream = File.Create(path);
        using var w = new Utf8JsonWriter(stream);

        w.WriteStartObject();
        WriteMetadata(w, metadata, generatedAt, counts);

        w.WriteStartObject("objects");
        foreach (var (key, value) in objects)
        {
            if (filter is not null && !filter(key))
            {
                continue;
            }

            w.WriteStartObject(key);
            w.WriteString("type", value.Type);
            WriteNullable(w, "outer", value.Outer);
            WriteNullable(w, "class", value.Class);
            WriteNullable(w, "super", value.Super);
            w.WriteEndObject();
        }

        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static void WriteFunctions(
        string path,
        Dictionary<string, ObjInfo> objects,
        Dictionary<string, List<FuncInfo>> functions,
        JsonElement metadata,
        string generatedAt,
        object counts,
        Func<string, bool>? filter)
    {
        using var stream = File.Create(path);
        using var w = new Utf8JsonWriter(stream);

        w.WriteStartObject();
        WriteMetadata(w, metadata, generatedAt, counts);

        w.WriteStartObject("objects");
        foreach (var (owner, funcs) in functions)
        {
            if (filter is not null && !filter(owner))
            {
                continue;
            }

            objects.TryGetValue(owner, out var info);

            w.WriteStartObject(owner);
            w.WriteString("type", info?.Type ?? "Object");
            WriteNullable(w, "super", info?.Super);

            w.WriteStartObject("functions");
            foreach (var f in funcs)
            {
                w.WriteStartObject(f.Name);
                WriteNullable(w, "flags", f.Flags);

                w.WriteStartArray("params");
                foreach (var p in f.Params)
                {
                    w.WriteStartObject();
                    w.WriteString("name", p.Name);
                    w.WriteString("type", p.Type);
                    w.WriteString("dir", p.Dir);
                    WriteNullable(w, "class", p.Cls);
                    WriteNullable(w, "enum", p.Enum);
                    w.WriteNumber("offset", p.Offset);
                    w.WriteNumber("size", p.Size);
                    w.WriteEndObject();
                }

                w.WriteEndArray();
                w.WriteEndObject();
            }

            w.WriteEndObject();
            w.WriteEndObject();
        }

        w.WriteEndObject();
        w.WriteEndObject();
    }

    private static void WriteMetadata(Utf8JsonWriter w, JsonElement metadata, string generatedAt, object counts)
    {
        w.WriteStartObject("metadata");
        w.WriteString("extractor", "Solstice.Tools.JmapExtractor");
        w.WriteString("generated_at", generatedAt);

        if (metadata.ValueKind == JsonValueKind.Object)
        {
            if (metadata.TryGetProperty("source", out var src))
            {
                w.WriteString("source", src.GetString());
            }

            if (metadata.TryGetProperty("build_change_list", out var bcl))
            {
                w.WriteString("build", bcl.GetString());
            }

            if (metadata.TryGetProperty("engine_version", out var ev) && ev.ValueKind == JsonValueKind.Object)
            {
                var major = ev.TryGetProperty("major", out var mj) ? mj.GetInt32() : 0;
                var minor = ev.TryGetProperty("minor", out var mn) ? mn.GetInt32() : 0;
                w.WriteString("engine", $"{major}.{minor}");
            }
        }

        w.WritePropertyName("counts");
        JsonSerializer.Serialize(w, counts);
        w.WriteEndObject();
    }

    private static void WriteNullable(Utf8JsonWriter w, string name, string? value)
    {
        if (value is null)
        {
            w.WriteNull(name);
        }
        else
        {
            w.WriteString(name, value);
        }
    }

    private sealed record ObjInfo(string? Type, string? Outer, string? Class, string? Super);

    private sealed record FuncInfo(string Name, string? Flags, List<ParamInfo> Params);

    private sealed record ParamInfo(string Name, string Type, string Dir, string? Cls, string? Enum, int Size, int Offset);
}
