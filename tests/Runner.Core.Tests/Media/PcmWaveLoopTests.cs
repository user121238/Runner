using System.Buffers.Binary;
using Runner.Business.Media;

namespace Runner.Core.Tests.Media;

[TestClass]
public sealed class PcmWaveLoopTests
{
    [TestMethod]
    [DataRow(120, 4000)]
    [DataRow(180, 2667)]
    [DataRow(300, 1600)]
    public void Build_RequestedCadence_UsesNearestWholeSampleAndPreservesSound(int cadence, int frames)
    {
        var source = CreateWave();

        byte[] result = PcmWaveLoop.Read(source).Build(cadence);

        Assert.HasCount(44 + frames * 2, result);
        Assert.AreEqual(result.Length - 8, BinaryPrimitives.ReadInt32LittleEndian(result.AsSpan(4)));
        Assert.AreEqual(8000, BinaryPrimitives.ReadInt32LittleEndian(result.AsSpan(24)));
        Assert.AreEqual(frames * 2, BinaryPrimitives.ReadInt32LittleEndian(result.AsSpan(40)));
        Assert.AreEqual("12345678", Convert.ToHexString(result.AsSpan(44, 4)));
        Assert.AreEqual(-1, result.AsSpan(48).IndexOfAnyExcept((byte)0));
    }

    [TestMethod]
    public void Read_OddSizedUnknownChunk_SkipsPaddingWithoutDamagingSound()
    {
        byte[] source = CreateWave();
        byte[] withJunk = [.. source[..12], (byte)'J', (byte)'U', (byte)'N', (byte)'K', 1, 0, 0, 0, 0x7F, 0, .. source[12..]];
        BinaryPrimitives.WriteInt32LittleEndian(withJunk.AsSpan(4), withJunk.Length - 8);

        byte[] result = PcmWaveLoop.Read(withJunk).Build(120);

        Assert.AreEqual("12345678", Convert.ToHexString(result.AsSpan(44, 4)));
        Assert.HasCount(8044, result);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Build_NonPositiveCadence_RejectsIt(int cadence)
    {
        var wave = PcmWaveLoop.Read(CreateWave());

        var error = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => wave.Build(cadence));

        Assert.AreEqual("stepsPerMinute", error.ParamName);
    }

    [TestMethod]
    public void Build_IntervalShorterThanSound_RejectsTruncation()
    {
        var wave = PcmWaveLoop.Read(CreateWave());

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => wave.Build(1_000_000));
    }

    [TestMethod]
    public void Read_NonPcmFormat_RejectsIt()
    {
        byte[] source = CreateWave();
        BinaryPrimitives.WriteUInt16LittleEndian(source.AsSpan(20), 3);

        Assert.ThrowsExactly<InvalidDataException>(() => PcmWaveLoop.Read(source));
    }

    [TestMethod]
    public void Read_TruncatedSamples_RejectsIt()
    {
        byte[] source = CreateWave()[..^1];

        Assert.ThrowsExactly<InvalidDataException>(() => PcmWaveLoop.Read(source));
    }

    private static byte[] CreateWave()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(40);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write(8000);
        writer.Write(16000);
        writer.Write((ushort)2);
        writer.Write((ushort)16);
        writer.Write("data"u8);
        writer.Write(4);
        writer.Write(new byte[] { 0x12, 0x34, 0x56, 0x78 });
        return stream.ToArray();
    }
}
