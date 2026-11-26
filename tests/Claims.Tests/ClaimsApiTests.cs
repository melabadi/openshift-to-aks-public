using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Claims.Tests;

public class ClaimsApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    private Task<HttpResponseMessage> Post(string json, string contentType = "application/json") =>
        _client.PostAsync("/claims/analyze", new StringContent(json, Encoding.UTF8, contentType));

    [Fact]
    public async Task AnalyzesClaimWithStableContract()
    {
        var response = await Post("""{"lossCents":900000,"deductibleCents":50000,"limitCents":500000,"daysToReport":31}""");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("""{"estimatedPayableCents":500000,"queue":"adjuster-review","reasons":["POLICY_LIMIT_APPLIED","LATE_REPORT"]}""",
            await response.Content.ReadAsStringAsync());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }

    [Theory]
    [InlineData("""{"lossCents":1.5,"deductibleCents":0,"limitCents":1,"daysToReport":1}""")]
    [InlineData("""{"lossCents":"1","deductibleCents":0,"limitCents":1,"daysToReport":1}""")]
    [InlineData("""{"lossCents":1,"lossCents":1,"limitCents":1,"daysToReport":1}""")]
    [InlineData("""{"lossCents":1,"deductibleCents":0,"limitCents":1,"daysToReport":1,"extra":1}""")]
    [InlineData("""[1,2,3,4]""")]
    public async Task RejectsInputOutsideContract(string json) =>
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(json)).StatusCode);

    [Fact]
    public async Task RejectsWrongMethodAndMediaType()
    {
        var get = await _client.GetAsync("/claims/analyze");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, get.StatusCode);
        Assert.Equal("POST", string.Join(",", get.Content.Headers.Allow));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await Post("{}", "text/plain")).StatusCode);
    }

    [Fact]
    public async Task ServesFrontendAtRoot()
    {
        var response = await _client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task UnknownPathsReturnJson404()
    {
        var response = await _client.GetAsync("/Web/index.html");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Not found", (await response.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["error"]);
    }
}
