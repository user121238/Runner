namespace Runner.Core.Geolocation;

public enum FilterResult
{
    Accept,
    RejectDistanceTooSmall,
    RejectSpeedTooFast,
    RejectTimeIntervalTooShort,
    RejectInvalidCoordinate,
    RejectAccuracyTooPoor,
    RejectStaleLocation,
    RejectFutureLocation
}
