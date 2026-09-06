using System;
using System.Net.Http;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using JobRadar.Api.Sources.Greenhouse;
using Xunit;

namespace JobRadar.Api.Tests.Sources.Greenhouse;

public sealed class GreenhouseHttpClientTests
{
    [Fact]
    public async Task Retries_transient_status_before_returning_jobs()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.ServiceUnavailable),
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"jobs\":[]}")
            });
        var client = new HttpClient(handler);
        var source = new JobSource("source-1", "company-1", "Example", "Careers", "GREENHOUSE_API", "https://boards.greenhouse.io/example", true, "never_run", "Never", 0, 0, null, "example");
        var jobs = await new GreenhouseJobSource(client, NullLogger<GreenhouseJobSource>.Instance).FetchAsync(source, "Example", CancellationToken.None);

        Assert.Empty(jobs);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task Rejects_malformed_json()
    {
        var handler = new SequenceHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json")
        });
        var client = new HttpClient(handler);
        var source = new JobSource("source-1", "company-1", "Example", "Careers", "GREENHOUSE_API", "https://boards.greenhouse.io/example", true, "never_run", "Never", 0, 0, null, "example");

        await Assert.ThrowsAsync<InvalidOperationException>(() => new GreenhouseJobSource(client, NullLogger<GreenhouseJobSource>.Instance).FetchAsync(source, "Example", CancellationToken.None));
    }

    [Fact]
    public async Task Does_not_retry_permanent_not_found_response()
    {
        var handler = new SequenceHandler(new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = new HttpClient(handler);
        var source = new JobSource("source-1", "company-1", "Example", "Careers", "GREENHOUSE_API", "https://boards.greenhouse.io/example", true, "never_run", "Never", 0, 0, null, "example");

        await Assert.ThrowsAsync<HttpRequestException>(() => new GreenhouseJobSource(client, NullLogger<GreenhouseJobSource>.Instance).FetchAsync(source, "Example", CancellationToken.None));
        Assert.Equal(1, handler.RequestCount);
    }

    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private int index;
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var response = responses[Math.Min(index++, responses.Length - 1)];
            return Task.FromResult(response);
        }
    }
}
