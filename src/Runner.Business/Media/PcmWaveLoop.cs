using System.Text;

namespace Runner.Business.Media;

/// <summary>把应用内 PCM16 提示音补齐为一拍的长度，让原生音频引擎无缝循环而非依赖 UI 定时器。</summary>
internal sealed class PcmWaveLoop(byte[] samples, int sampleRate, ushort channels)
{
    public static PcmWaveLoop Read(byte[] wave)
    {
        using var stream = new MemoryStream(wave, writable: false);
        using var reader = new BinaryReader(stream, Encoding.ASCII);
        if (ReadTag(reader) != "RIFF") throw new InvalidDataException("节拍音不是 RIFF 音频");
        reader.ReadUInt32();
        if (ReadTag(reader) != "WAVE") throw new InvalidDataException("节拍音不是 WAV 音频");

        int sampleRate = 0;
        ushort channels = 0;
        byte[]? samples = null;
        while (stream.Position + 8 <= stream.Length)
        {
            string tag = ReadTag(reader);
            uint size = reader.ReadUInt32();
            long end = stream.Position + size;
            if (end > stream.Length) throw new InvalidDataException("节拍音数据不完整");
            if (tag == "fmt ")
            {
                if (size < 16 || reader.ReadUInt16() != 1)
                    throw new InvalidDataException("节拍音需要 PCM16 格式");
                channels = reader.ReadUInt16();
                sampleRate = reader.ReadInt32();
                reader.ReadUInt32();
                reader.ReadUInt16();
                if (reader.ReadUInt16() != 16) throw new InvalidDataException("节拍音需要 16 位采样");
            }
            else if (tag == "data")
            {
                samples = reader.ReadBytes(checked((int)size));
            }
            stream.Position = end + (size & 1);
        }

        if (samples == null || samples.Length == 0 || channels is < 1 or > 2 || sampleRate is < 1 or > 192000)
            throw new InvalidDataException("节拍音缺少有效的 PCM 数据");
        if (samples.Length % (channels * 2) != 0) throw new InvalidDataException("节拍音采样帧不完整");
        return new PcmWaveLoop(samples, sampleRate, channels);
    }

    public byte[] Build(int stepsPerMinute)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(stepsPerMinute);
        int frameBytes = channels * 2;
        int frameCount = checked((int)Math.Round(sampleRate * 60d / stepsPerMinute));
        int dataLength = checked(frameCount * frameBytes);
        if (dataLength < samples.Length) throw new ArgumentOutOfRangeException(nameof(stepsPerMinute), "拍间隔短于提示音");
        var data = new byte[dataLength];
        samples.CopyTo(data, 0);

        using var output = new MemoryStream(44 + dataLength);
        using var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true);
        writer.Write("RIFF"u8);
        writer.Write(36 + dataLength);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write(channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * frameBytes);
        writer.Write((ushort)frameBytes);
        writer.Write((ushort)16);
        writer.Write("data"u8);
        writer.Write(dataLength);
        writer.Write(data);
        return output.ToArray();
    }

    private static string ReadTag(BinaryReader reader) => Encoding.ASCII.GetString(reader.ReadBytes(4));
}
