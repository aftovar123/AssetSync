using AssetSync.Api;
using Microsoft.AspNetCore.Http;

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

    [Theory]
    [InlineData("/")]
    [InlineData("/health")]
    [InlineData("/health/ready")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/scalar")]
    [InlineData("/robots933456.txt")]
    public void IsIgnoredPath_ProbesDocsAndRoot_ReturnsTrue(string path)
    {
        Assert.True(Observability.IsIgnoredPath(new PathString(path)));
    }

    [Theory]
    [InlineData("/assets")]
    [InlineData("/work-orders/5/complete")]
    [InlineData("/healthy")] // only whole segments match
    [InlineData("/auth/token")]
    public void IsIgnoredPath_ApiEndpoints_ReturnsFalse(string path)
    {
        Assert.False(Observability.IsIgnoredPath(new PathString(path)));
    }
}
