namespace Runner.Business.Services.Database;

public class SportReport
{
    public string Sid { get; init; } = "";
    public string Key { get; init; } = "";
    public long Time { get; init; }
    public string? Category { get; init; }
    public long? TimeIn0Tz { get; init; }
    public string? Value { get; init; }
}
