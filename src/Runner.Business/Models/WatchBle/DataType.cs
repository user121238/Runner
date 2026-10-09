namespace Runner.Business.Models.WatchBle;

public enum DataType : byte
{
    Wristlet = 0x01,
    HeartRate = 0x02,
    ECG = 0x03,
    Temperature = 0x04,
    GpsSummary = 0x05,
    GpsDetail = 0x06,
    PpgRr = 0x08,
    AfTime = 0x0A,
    AfPpg = 0x0B,
    Pai = 0x0D,
    Coaching = 0x0E,
    Ppg = 0x10,
    Af = 0x11,
    BloodPressureChar = 0x35,
    FirstBeatSimple = 0x24,
    FirstBeatDetail = 0x23,
    ActivityToday = 0x3F
}
