using System.IO;
using System.Net;
using System.Net.Http;
using DeepSeekClock.Core;
using DeepSeekClock.Windows;

namespace DeepSeekClock.Windows.Tests;

public sealed class OfficialPricingServiceTests
{
    private const string Table = """
        <table><tr><td>MODEL</td><td>deepseek-flash</td><td>deepseek-v4-pro</td></tr>
        <tr><td>1M INPUT TOKENS (CACHE HIT)</td><td>OFF-PEAK</td><td>$0.003</td><td>$0.022</td></tr>
        <tr><td>PEAK</td><td>$0.006</td><td>$0.044</td></tr>
        <tr><td>1M INPUT TOKENS (CACHE MISS)</td><td>OFF-PEAK</td><td>$0.15</td><td>$0.66</td></tr>
        <tr><td>PEAK</td><td>$0.3</td><td>$1.32</td></tr>
        <tr><td>1M OUTPUT TOKENS</td><td>OFF-PEAK</td><td>$0.6</td><td>$1.98</td></tr>
        <tr><td>PEAK</td><td>$1.2</td><td>$3.96</td></tr></table>
        """;

    [Fact]
    public async Task VerifiedPageIsSavedAndMalformedRefreshKeepsTheLastPrices()
    {
        var path = Path.Combine(Path.GetTempPath(), $"deepseek-prices-{Guid.NewGuid():N}.json");
        try
        {
            using (var service = new OfficialPricingService(path, new StubHandler(Table)))
            {
                var snapshot = await service.RefreshAsync(CancellationToken.None);
                Assert.Equal(PricingCatalog.Bundled, snapshot?.Catalog);
            }
            using (var service = new OfficialPricingService(path, new StubHandler("<html>changed</html>")))
            {
                Assert.Null(await service.RefreshAsync(CancellationToken.None));
                Assert.Equal(PricingCatalog.Bundled, service.ReadCache()?.Catalog);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class StubHandler(string html) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(OfficialPricingParser.SourceUrl, request.RequestUri?.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) });
        }
    }
}
