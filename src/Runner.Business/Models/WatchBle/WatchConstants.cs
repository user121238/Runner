namespace Runner.Business.Models.WatchBle;

public static class WatchConstants
{
    public const string WATCH_NAME_PREFIX = "Xiaomi Watch";
    public static readonly Guid SPP_UUID = Guid.Parse("00001101-0000-1000-8000-00805F9B34FB");

    // MiConnect (com.xiaomi.mi_connect_service) uses FDAA service with custom 128-bit characteristics
    public static readonly Guid SERVICE_MILI = Guid.Parse("0000FDAA-0000-1000-8000-00805F9B34FB");
    public static readonly Guid SERVICE_DEVICE_INFO = Guid.Parse("0000180A-0000-1000-8000-00805F9B34FB");

    // FDAA (MiConnect) - used by com.xiaomi.mi_connect_service
    public static readonly Guid CHAR_CONTROL_POINT = Guid.Parse("00000007-09da-4bed-9652-f507366fcfc5");
    public static readonly Guid CHAR_DATA = Guid.Parse("00000009-09da-4bed-9652-f507366fcfc5");

    // FDAB (classic BLE) - fallback for some Xiaomi devices
    public static readonly Guid CHAR_FDAB_CONTROL = Guid.Parse("00000002-0000-1000-8000-00805F9B34FB");
    public static readonly Guid CHAR_FDAB_DATA = Guid.Parse("00000003-0000-1000-8000-00805F9B34FB");

    // Custom companion service (cc353442-be58-4ea2-876e-11d8d6976366)
    public static readonly Guid SERVICE_COMPANION = Guid.Parse("cc353442-be58-4ea2-876e-11d8d6976366");
    public static readonly Guid CHAR_COMPANION = Guid.Parse("c551c36a-0377-4a29-9657-74ffb655a188");

    public static readonly Guid CHAR_SERIAL = Guid.Parse("00002A25-0000-1000-8000-00805F9B34FB");
    public static readonly Guid CHAR_FW_REV = Guid.Parse("00002A28-0000-1000-8000-00805F9B34FB");

    public static readonly Guid CHAR_CCC = Guid.Parse("00002902-0000-1000-8000-00805F9B34FB");

    public const byte CMD_GET_HEADER = 0x01;
    public const byte CMD_START_TRANSFER = 0x02;
    public const byte CMD_STOP_TRANSFER = 0x03;
    public const byte CMD_QUERY_VERSION = 0x00;

    public const byte SPORT_KIND_OUTDOOR = 1;
    public const byte SPORT_KIND_INDOOR = 2;
    public const byte SPORT_KIND_RIDING = 3;
    public const byte SPORT_KIND_WALK = 4;
    public const byte SPORT_KIND_EXERCISE = 5;
}
