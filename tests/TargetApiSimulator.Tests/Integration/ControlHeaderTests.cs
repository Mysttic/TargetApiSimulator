using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace TargetApiSimulator.Tests.Integration;

/// <summary>
/// The X-Sim-* headers are opt-in. These tests run against a factory with the feature switched
/// on; <see cref="ControlHeadersDisabledTests"/> covers the shipped default.
/// </summary>
public class ControlHeaderTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Route = "/api/target";
    private readonly HttpClient _client;

    public ControlHeaderTests(WebApplicationFactory<Program> factory)
        => _client = factory
            .WithWebHostBuilder(b =>
            {
                b.UseSetting("Simulator:EnableControlHeaders", "true");
                b.UseSetting("Simulator:MaxDelayMs", "2000");
            })
            .CreateClient();

    private static HttpRequestMessage Request(string? status = null, string? delay = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, Route)
        {
            Content = new StringContent("""{"a":1}""", Encoding.UTF8, "application/json")
        };

        if (status is not null)
        {
            request.Headers.Add(SimulatorControlHeaders.Status, status);
        }

        if (delay is not null)
        {
            request.Headers.Add(SimulatorControlHeaders.Delay, delay);
        }

        return request;
    }

    [Theory]
    [InlineData("503", HttpStatusCode.ServiceUnavailable)]
    [InlineData("500", HttpStatusCode.InternalServerError)]
    [InlineData("429", HttpStatusCode.TooManyRequests)]
    [InlineData("404", HttpStatusCode.NotFound)]
    [InlineData("201", HttpStatusCode.Created)]
    public async Task Post_WithForcedStatus_ReturnsThatStatus(string header, HttpStatusCode expected)
    {
        var response = await _client.SendAsync(Request(status: header));

        response.StatusCode.ShouldBe(expected);
    }

    [Fact]
    public async Task Post_WithForcedStatus_EchoesWhatWasApplied()
    {
        var response = await _client.SendAsync(Request(status: "503"));

        response.Headers.GetValues(SimulatorControlHeaders.Applied).ShouldContain("status=503");
    }

    // The forced status short-circuits before the endpoint runs, so no validation happens and
    // no body is produced.
    [Fact]
    public async Task Post_WithForcedStatus_DoesNotRunTheEndpoint()
    {
        var response = await _client.SendAsync(Request(status: "503"));

        (await response.Content.ReadAsStringAsync()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("600")]
    [InlineData("99")]
    [InlineData("abc")]
    [InlineData("")]
    public async Task Post_WithUnusableStatusHeader_IsIgnored(string header)
    {
        var response = await _client.SendAsync(Request(status: header));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("true");
        response.Headers.Contains(SimulatorControlHeaders.Applied).ShouldBeFalse();
    }

    [Fact]
    public async Task Post_WithDelay_TakesAtLeastThatLong()
    {
        var stopwatch = Stopwatch.StartNew();

        var response = await _client.SendAsync(Request(delay: "400"));

        stopwatch.Stop();
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        stopwatch.ElapsedMilliseconds.ShouldBeGreaterThanOrEqualTo(350);
        response.Headers.GetValues(SimulatorControlHeaders.Applied).ShouldContain("delay=400");
    }

    // MaxDelayMs is 2000 for this fixture, so a larger request is clamped rather than honoured.
    [Fact]
    public async Task Post_WithDelayAboveMaximum_IsClampedAndReported()
    {
        var response = await _client.SendAsync(Request(delay: "999999"));

        response.Headers.GetValues(SimulatorControlHeaders.Applied).ShouldContain("delay=2000");
    }

    [Fact]
    public async Task Post_WithBothHeaders_AppliesBoth()
    {
        var response = await _client.SendAsync(Request(status: "503", delay: "10"));

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var applied = response.Headers.GetValues(SimulatorControlHeaders.Applied).Single();
        applied.ShouldBe("delay=10,status=503");
    }

    [Fact]
    public async Task Post_WithoutControlHeaders_BehavesNormally()
    {
        var response = await _client.SendAsync(Request());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("true");
        response.Headers.Contains(SimulatorControlHeaders.Applied).ShouldBeFalse();
    }

    [Fact]
    public async Task GetHealthz_IsAlsoAffected()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/healthz");
        request.Headers.Add(SimulatorControlHeaders.Status, "503");

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
    }
}

/// <summary>
/// Pins the shipped default: without opting in, the X-Sim-* headers are inert.
/// </summary>
public class ControlHeadersDisabledTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ControlHeadersDisabledTests(WebApplicationFactory<Program> factory)
        => _client = factory.CreateClient();

    [Fact]
    public async Task Post_WithControlHeaders_IgnoresThemByDefault()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/target")
        {
            Content = new StringContent("""{"a":1}""", Encoding.UTF8, "application/json")
        };
        request.Headers.Add(SimulatorControlHeaders.Status, "503");
        request.Headers.Add(SimulatorControlHeaders.Delay, "5000");

        var stopwatch = Stopwatch.StartNew();
        var response = await _client.SendAsync(request);
        stopwatch.Stop();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("true");
        response.Headers.Contains(SimulatorControlHeaders.Applied).ShouldBeFalse();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000);
    }
}
