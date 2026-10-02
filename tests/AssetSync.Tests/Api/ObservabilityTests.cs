using AssetSync.Api;

namespace AssetSync.Tests.Api;

public class ObservabilityTests
{
    [Theory]
    [InlineData("InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://westus3-1.in.applicationinsights.azure.com/")]
    [InlineData("InstrumentationKey=00000000-0000-0000-0000-000000000000")]
    [InlineData("instrumentationkey=00000000-0000-0000-0000-000000000000; IngestionEndpoint=https://x/ ;")]
    public void IsValidAzureMonitorConnectionString_WellFormed_ReturnsTrue(string value)
    {
        Assert.True(Observability.IsValidAzureMonitorConnectionString(value));
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000")] // the bare instrumentation key
    [InlineData("InstrumentationKey=")]
    [InlineData("IngestionEndpoint=https://westus3-1.in.applicationinsights.azure.com/")]
    [InlineData("InstrumentationKey=abc;garbage")]
    [InlineData(";")]
    public void IsValidAzureMonitorConnectionString_Malformed_ReturnsFalse(string value)
    {
        Assert.False(Observability.IsValidAzureMonitorConnectionString(value));
    }
}
