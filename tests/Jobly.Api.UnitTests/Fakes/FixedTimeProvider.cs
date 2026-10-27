namespace Jobly.Api.UnitTests.Fakes;

/// <summary>A clock that always says the same time, so tests give the same result every day.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
