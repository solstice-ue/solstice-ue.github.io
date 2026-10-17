namespace Solstice.Core.AssetRegistry;

/// <summary>
/// AssetRegistry.bin 的固定头部信息。
///
/// 仅覆盖已在真实样本上验证过的 24 字节头部，不推断其后的任何结构。
/// 证据见 docs/samples.md：KARDS 样本 version=21、bFilterEditorOnly=1。
/// </summary>
/// <param name="GuidMatches">文件开头的 16 字节是否等于 UE 的 AssetRegistry 标识。</param>
/// <param name="Version">格式版本号。KARDS 实测为 21。</param>
/// <param name="FilterEditorOnly">是否过滤掉仅编辑器资产。</param>
/// <param name="GuidHex">实际读到的 GUID，便于排查不匹配的情况。</param>
public sealed record AssetRegistryHeader(
    bool GuidMatches,
    int Version,
    bool FilterEditorOnly,
    string GuidHex);
