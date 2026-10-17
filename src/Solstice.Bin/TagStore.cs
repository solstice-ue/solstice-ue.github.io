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
        var s = storeStart + 4; // 跳过 BeginMagic
        var nums = new int[11];
        for (var i = 0; i < 11; i++)
        {
            nums[i] = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(s + i * 4, 4));
        }

        s += 44;

        var textBytes = BinaryPrimitives.ReadInt32LittleEndian(file.AsSpan(s, 4));
        s += 4;
        var textData = file.AsSpan(s, textBytes).ToArray();
        s += textBytes;

        var store = new TagStore { _nums = nums, _textData = textData };

        store._numberlessNames = file.AsSpan(s, nums[0] * 4).ToArray(); s += nums[0] * 4;
        store._names = file.AsSpan(s, nums[1] * 4).ToArray(); s += nums[1] * 4;
        store._numberlessExportPaths = file.AsSpan(s, nums[2] * 16).ToArray(); s += nums[2] * 16;
        store._exportPaths = file.AsSpan(s, nums[3] * 16).ToArray(); s += nums[3] * 16;

        for (var i = 0; i < nums[5]; i++)
        {
            store._ansiOffsets.Add(BinaryPrimitives.ReadUInt32LittleEndian(file.AsSpan(s + i * 4, 4)));
        }

        s += nums[5] * 4;

        store._wideOffsets = file.AsSpan(s, nums[6] * 4).ToArray(); s += nums[6] * 4;

        store._ansiStrings = file.AsSpan(s, nums[7]).ToArray(); s += nums[7];

        store._wideStrings = file.AsSpan(s, nums[8] * 2).ToArray(); s += nums[8] * 2;

        store._numberlessPairs = file.AsSpan(s, nums[9] * 8).ToArray(); s += nums[9] * 8;

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
        w.Write(_textData);

        w.Write(_numberlessNames);
        w.Write(_names);
        w.Write(_numberlessExportPaths);
        w.Write(_exportPaths);

        foreach (var off in _ansiOffsets)
        {
            w.Write(off);
        }

        w.Write(_wideOffsets);
        w.Write(_ansiStrings);
        w.Write(_wideStrings);
        w.Write(_numberlessPairs);

        foreach (var (key, value) in _pairs)
        {
            w.Write(key);
            w.Write(value);
        }

        w.Write(BinLayout.StoreEndMagic);

        w.Flush();
        return ms.ToArray();
    }
}
