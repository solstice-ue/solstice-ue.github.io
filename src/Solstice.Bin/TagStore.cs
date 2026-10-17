using System.Buffers.Binary;
using System.Text;

namespace Solstice.Bin;

/// <summary>
/// 全局 tag store 的读写。
///
/// <para>布局：<c>BeginMagic | int32 nums[11] | uint32 textBytes | textData
/// | numberlessNames | names | numberlessExportPaths | exportPaths
/// | ansiOffsets | wideOffsets | ansiStrings | wideStrings
/// | numberlessPairs | pairs | EndMagic</c>。</para>
///
/// <para>nums 顺序：numberlessNames, names, numberlessExportPaths, exportPaths, texts,
/// ansiOffsets, wideOffsets, ansiBytes, wideStrings(utf16 个数), numberlessPairs, pairs。</para>
/// </summary>
public sealed class TagStore
{
    private int[] _nums = new int[11];

    /// <summary>
    /// 数组之间是否有 16 字节对齐填充。
    /// CUE4Parse：<c>AlignPosInArchive</c> 在 <c>version >= MemoryMappedTagDataStore</c> 时
    /// 把位置对齐到 16。样本 v21 是否启用取决于该阈值，所以两种模式都试。
    /// </summary>
    private bool _align16;
    private byte[] _textData = [];
    private byte[] _numberlessNames = [];
    private byte[] _names = [];
    private byte[] _numberlessExportPaths = [];
    private byte[] _exportPaths = [];
    private byte[] _wideOffsets = [];
    private byte[] _wideStrings = [];
    private byte[] _numberlessPairs = [];

    private readonly List<uint> _ansiOffsets = new();
    private byte[] _ansiStrings = [];
    private readonly List<(uint Key, uint Value)> _pairs = new();

    public int PairCount => _pairs.Count;

    public static TagStore Parse(byte[] file, int storeStart, int storeEnd)
    {
        try
        {
            return ParseCore(file, storeStart, true);
        }
        catch (InvalidOperationException)
        {
            return ParseCore(file, storeStart, false);
        }
    }

    private static TagStore ParseCore(byte[] file, int storeStart, bool align16)
    {
        var s = storeStart + 4; // 跳过 BeginMagic
        var nums = new int[11];
        for (var i = 0; i < 11; i++)
        {
            nums[i] = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(s + i * 4, 4));
        }

        s += 44;

        var textBytes = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(s, 4));
        s += 4;
        s = Align(s, align16);

        // CUE4Parse 的读法：这里不是「按字节长度切一块」，而是 nums[4] 个 FString
        // （int32 长度前缀；正数为 ASCII，负数为 UTF-16 字符数）。按字节切会在
        // 含非 ASCII 时错位。
        var textStart = s;
        for (var i = 0; i < nums[4]; i++)
        {
            var len = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(s, 4));
            s += 4;
            s += len < 0 ? -len * 2 : len;
        }

        var textData = file.AsSpan(textStart, s - textStart).ToArray();
        _ = textBytes;

        var store = new TagStore { _nums = nums, _textData = textData, _align16 = align16 };

        s = Align(s, align16);
        store._numberlessNames = file.AsSpan(s, nums[0] * 4).ToArray(); s += nums[0] * 4;

        s = Align(s, align16);
        store._names = file.AsSpan(s, nums[1] * 4).ToArray(); s += nums[1] * 4;

        s = Align(s, align16);
        store._numberlessExportPaths = file.AsSpan(s, nums[2] * 16).ToArray(); s += nums[2] * 16;

        s = Align(s, align16);
        store._exportPaths = file.AsSpan(s, nums[3] * 16).ToArray(); s += nums[3] * 16;

        s = Align(s, align16);
        for (var i = 0; i < nums[5]; i++)
        {
            store._ansiOffsets.Add(BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(s + i * 4, 4)));
        }

        s += nums[5] * 4;

        s = Align(s, align16);
        store._wideOffsets = file.AsSpan(s, nums[6] * 4).ToArray(); s += nums[6] * 4;

        s = Align(s, align16);
        store._ansiStrings = file.AsSpan(s, nums[7]).ToArray(); s += nums[7];

        s = Align(s, align16);
        store._wideStrings = file.AsSpan(s, nums[8] * 2).ToArray(); s += nums[8] * 2;

        s = Align(s, align16);
        store._numberlessPairs = file.AsSpan(s, nums[9] * 8).ToArray(); s += nums[9] * 8;

        s = Align(s, align16);
        for (var i = 0; i < nums[10]; i++)
        {
            var key = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(s + i * 8, 4));
            var val = BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(s + i * 8 + 4, 4));
            store._pairs.Add((key, val));
        }

        s += nums[10] * 8;

        if (BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(s, 4)) != BinLayout.StoreEndMagic)
        {
            throw new InvalidOperationException("store EndMagic 不匹配，布局假设有误。");
        }

        return store;
    }

    private static int Align(int pos, bool align16) => align16 ? (pos + 15) & ~15 : pos;

    /// <summary>追加一个 AnsiString，返回它的索引（用于拼 valueId）。</summary>
    public uint AddAnsiString(string value)
    {
        var raw = Encoding.ASCII.GetBytes(value);

        var newBlob = new byte[_ansiStrings.Length + raw.Length + 1];
        _ansiStrings.CopyTo(newBlob, 0);
        raw.CopyTo(newBlob, _ansiStrings.Length);
        // 末尾保留 0 终止符（newBlob 已初始化为 0）

        _ansiOffsets.Add((uint)_ansiStrings.Length);
        _ansiStrings = newBlob;

        return (uint)(_ansiOffsets.Count - 1);
    }

    /// <summary>追加一个 pair（key 是 name 索引，valueId = (类型 | 索引 &lt;&lt; 3)）。返回起始索引。</summary>
    public uint AddPairs(IReadOnlyList<(uint Key, uint Value)> pairs)
    {
        var start = (uint)_pairs.Count;
        _pairs.AddRange(pairs);
        return start;
    }

    public byte[] Serialize()
    {
        using var ms = new MemoryStream(_ansiStrings.Length + 1024);
        using var w = new BinaryWriter(ms);

        w.Write(BinLayout.StoreBeginMagic);

        var nums = (int[])_nums.Clone();
        nums[5] = _ansiOffsets.Count;
        nums[7] = _ansiStrings.Length;
        nums[10] = _pairs.Count;

        foreach (var n in nums)
        {
            w.Write(n);
        }

        w.Write(_textData.Length);
        Pad(ms);
        w.Write(_textData);
        Pad(ms);

        w.Write(_numberlessNames); Pad(ms);
        w.Write(_names); Pad(ms);
        w.Write(_numberlessExportPaths); Pad(ms);
        w.Write(_exportPaths); Pad(ms);

        foreach (var off in _ansiOffsets)
        {
            w.Write(off);
        }

        Pad(ms);
        w.Write(_wideOffsets); Pad(ms);
        w.Write(_ansiStrings); Pad(ms);
        w.Write(_wideStrings); Pad(ms);
        w.Write(_numberlessPairs); Pad(ms);

        foreach (var (key, value) in _pairs)
        {
            w.Write(key);
            w.Write(value);
        }

        Pad(ms);
        w.Write(BinLayout.StoreEndMagic);

        w.Flush();
        return ms.ToArray();
    }

    /// <summary>按 16 字节对齐填充（仅在 <see cref="_align16"/> 时生效）。</summary>
    private void Pad(MemoryStream ms)
    {
        if (!_align16)
        {
            return;
        }

        while (ms.Position % 16 != 0)
        {
            ms.WriteByte(0);
        }
    }
}
