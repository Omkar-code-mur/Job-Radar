using System.Text.Json;
using System;
using System.IO;

namespace JobRadar.Api.Tests.Sources.Greenhouse;

internal static class GreenhouseTestFixture
{
    public static T Load<T>(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName);
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException($"Fixture '{fileName}' is empty or invalid.");
    }
}
