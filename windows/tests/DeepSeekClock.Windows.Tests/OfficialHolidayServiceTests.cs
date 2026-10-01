using System.IO;
using System.Net;
using System.Net.Http;
using DeepSeekClock.Core;
using DeepSeekClock.Windows;

namespace DeepSeekClock.Windows.Tests;

public sealed class OfficialHolidayServiceTests
{
    private const string Notice = """
        <h1>国务院办公厅关于2026年部分节假日安排的通知</h1>
        <p>元旦：1月1日至3日放假。</p><p>春节：2月15日至23日放假。</p>
        <p>清明节：4月4日至6日放假。</p><p>劳动节：5月1日至5日放假。</p>
        <p>端午节：6月19日至21日放假。</p><p>中秋节：9月25日至27日放假。</p>
        <p>国庆节：10月1日至7日放假。</p>
        """;

    [Fact]
    public async Task VerifiedNoticeIsCachedAndBadRefreshDoesNotReplaceIt()
    {
        var path = Path.Combine(Path.GetTempPath(), $"deepseek-holidays-{Guid.NewGuid():N}.json");
        try
        {
            using (var service = new OfficialHolidayService(path, new StubHandler(Notice)))
            {
                var snapshot = await service.RefreshAsync(CancellationToken.None);
                Assert.Equal(7, snapshot?.Calendar.DaysOff.Count);
            }
            using (var service = new OfficialHolidayService(path, new StubHandler("<html>changed</html>")))
            {
                Assert.Null(await service.RefreshAsync(CancellationToken.None));
                Assert.Equal(2026, service.ReadCache()?.Calendar.Year);
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
            Assert.Equal(ChinesePublicHolidays.SourceUrl, request.RequestUri?.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(html) });
        }
    }
}
