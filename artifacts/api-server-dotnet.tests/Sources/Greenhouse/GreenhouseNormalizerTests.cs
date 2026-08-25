using System;
using JobRadar.Api.Sources.Greenhouse;
using Xunit;

namespace JobRadar.Api.Tests.Sources.Greenhouse;

public sealed class GreenhouseNormalizerTests
{
    [Fact]
    public void Maps_required_and_optional_fields()
    {
        var raw = new GreenhouseJob(1001, "Full Stack Engineer", "<p>Build <strong>React</strong> services.</p>",
            "https://boards.greenhouse.io/example/jobs/1001", DateTimeOffset.Parse("2026-08-25T08:00:00Z"),
            new("Remote - India"), [new("Engineering")], []);

        var result = GreenhouseNormalizer.Normalize(raw, "company-1", "source-1", "Example",
            "https://boards.greenhouse.io/example", DateTimeOffset.UtcNow);

        Assert.NotNull(result);
        Assert.Equal("job-greenhouse-1001", result!.Id);
        Assert.Equal("Full Stack Engineer", result.Title);
        Assert.Contains("Build React services.", result.Description);
        Assert.Equal("Remote", result.WorkplaceType);
        Assert.Equal("Engineering", result.Department);
    }

    [Fact]
    public void Skips_records_without_required_fields()
    {
        var raw = new GreenhouseJob(0, "", null, null, null, null, null, null);

        var result = GreenhouseNormalizer.Normalize(raw, "company-1", "source-1", "Example", "https://example.com", DateTimeOffset.UtcNow);

        Assert.Null(result);
    }
}
