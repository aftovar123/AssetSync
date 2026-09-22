using AssetSync.Domain;

namespace AssetSync.Infrastructure;

public class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
