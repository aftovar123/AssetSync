namespace AssetSync.Domain;

/// <summary>
/// Seam around "what time is it right now" so tests can pin an exact
/// instant instead of asserting loosely around whenever the test happened
/// to run. Same pattern as Questlog's Clock/SystemClock.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
