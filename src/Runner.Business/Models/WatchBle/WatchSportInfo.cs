using System.Diagnostics;

namespace Runner.Business.Models.WatchBle;

public class WatchSportInfo
{
    public int GpsState { get; set; } = -1;
    public int ElapsedSeconds { get; set; } = -1;
    public int StartTime { get; set; } = -1;
    public int DistanceMeters { get; set; } = -1;
    public int PaceSecPerKm { get; set; } = -1;
    public int HeartRate { get; set; } = -1;
    public int Steps { get; set; } = -1;
    public int Speed { get; set; } = -1;
    public int Calories { get; set; } = -1;
    public int StepFrequency { get; set; } = -1;
    public int StrideLength { get; set; } = -1;
    public int Ascent { get; set; } = -1;
    public int Descent { get; set; } = -1;
    public int MaxSpeed { get; set; } = -1;
    public int AvePace { get; set; } = -1;
    public int AveSpeed { get; set; } = -1;

    public TimeSpan Elapsed => ElapsedSeconds >= 0 ? TimeSpan.FromSeconds(ElapsedSeconds) : TimeSpan.Zero;
    public double DistanceKm => DistanceMeters >= 0 ? DistanceMeters / 1000.0 : 0;
    public double SpeedKmh => Speed >= 0 ? Speed / 100.0 : 0;
    public double PaceMinPerKm => PaceSecPerKm > 0 ? PaceSecPerKm / 60.0 : 0;
    public string PaceText => PaceSecPerKm > 0 ? $"{PaceSecPerKm / 60}:{PaceSecPerKm % 60:D2}/km" : "--:--/km";

    public static WatchSportInfo Parse(byte[] data)
    {
        var info = new WatchSportInfo();
        if (data == null || data.Length < 4) return info;

        try
        {
            int bitmask = BitConverter.ToInt32(data, 0);
            int offset = 4;

            if ((bitmask & (1 << 0)) != 0 && offset < data.Length)
                info.GpsState = data[offset++];

            if ((bitmask & (1 << 1)) != 0 && offset + 4 <= data.Length)
            {
                info.StartTime = BitConverter.ToInt32(data, offset);
                offset += 4;
            }

            if ((bitmask & (1 << 2)) != 0 && offset + 4 <= data.Length)
            {
                info.ElapsedSeconds = BitConverter.ToInt32(data, offset);
                offset += 4;
            }

            if ((bitmask & (1 << 3)) != 0 && offset + 4 <= data.Length)
            {
                info.DistanceMeters = BitConverter.ToInt32(data, offset);
                offset += 4;
            }

            if ((bitmask & (1 << 4)) != 0 && offset + 4 <= data.Length)
            {
                info.PaceSecPerKm = BitConverter.ToInt32(data, offset);
                offset += 4;
            }

            if ((bitmask & (1 << 5)) != 0 && offset < data.Length)
                info.HeartRate = data[offset++];

            if ((bitmask & (1 << 6)) != 0 && offset + 4 <= data.Length)
            {
                info.Steps = BitConverter.ToInt32(data, offset);
                offset += 4;
            }

            if ((bitmask & (1 << 7)) != 0 && offset + 4 <= data.Length)
            {
                info.Speed = BitConverter.ToInt32(data, offset);
                offset += 4;
            }

            if ((bitmask & (1 << 8)) != 0 && offset + 4 <= data.Length)
            {
                info.Calories = BitConverter.ToInt32(data, offset);
                offset += 4;
            }

            if ((bitmask & (1 << 9)) != 0 && offset + 2 <= data.Length)
            {
                info.StepFrequency = BitConverter.ToInt16(data, offset);
                offset += 2;
            }

            if ((bitmask & (1 << 10)) != 0 && offset + 2 <= data.Length)
            {
                info.StrideLength = BitConverter.ToInt16(data, offset);
                offset += 2;
            }

            if ((bitmask & (1 << 11)) != 0 && offset + 4 <= data.Length)
            {
                info.Ascent = BitConverter.ToInt32(data, offset);
                offset += 4;
            }

            if ((bitmask & (1 << 12)) != 0 && offset + 4 <= data.Length)
            {
                info.Descent = BitConverter.ToInt32(data, offset);
                offset += 4;
            }

            if ((bitmask & (1 << 13)) != 0 && offset + 4 <= data.Length)
            {
                info.MaxSpeed = BitConverter.ToInt32(data, offset);
                offset += 4;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[WatchSportInfo] Parse error: {ex.Message}");
        }

        return info;
    }
}
