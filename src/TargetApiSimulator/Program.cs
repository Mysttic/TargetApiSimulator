using System.Reflection;
using Microsoft.Extensions.Options;
using TargetApiSimulator;

var builder = WebApplication.CreateBuilder(args);

// Set in code, not only in appsettings.json, so the standalone executable behaves the same when
// it runs on its own. Anything under Logging: in configuration still overrides this.
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff ";
    options.UseUtcTimestamp = true;
});
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);

// The stub handles payloads in the kilobyte range. Kestrel's default is 30 MB, which is three
// thousand times more than needed and the only real memory multiplier in this process.
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = 1024 * 1024);

builder.Services.Configure<SimulatorOptions>(
    builder.Configuration.GetSection(SimulatorOptions.SectionName));

var app = builder.Build();
var log = app.Logger;

var simulator = app.Services.GetRequiredService<IOptions<SimulatorOptions>>().Value;

// Registered only when enabled, so the disabled case costs nothing per request.
if (simulator.EnableControlHeaders)
{
    log.LogInformation(
        "X-Sim-* control headers are enabled (max delay {MaxDelayMs} ms).", simulator.MaxDelayMs);

    app.Use(async (context, next) =>
    {
        var applied = new List<string>(capacity: 2);

        if (SimulatorControlHeaders.TryParseDelay(
                context.Request.Headers[SimulatorControlHeaders.Delay], simulator.MaxDelayMs, out var delayMs))
        {
            applied.Add($"delay={delayMs}");

            try
            {
                await Task.Delay(delayMs, context.RequestAborted);
            }
            catch (OperationCanceledException)
            {
                // Expected: the caller gave up during a delay it asked for.
                log.LogInformation("Client aborted during a simulated delay of {DelayMs} ms.", delayMs);
                return;
            }
        }

        var forcedStatus = SimulatorControlHeaders.TryParseStatus(
            context.Request.Headers[SimulatorControlHeaders.Status], out var statusCode);

        if (forcedStatus)
        {
            applied.Add($"status={statusCode}");
        }

        if (applied.Count > 0)
        {
            context.Response.Headers[SimulatorControlHeaders.Applied] = string.Join(',', applied);
        }

        if (forcedStatus)
        {
            context.Response.StatusCode = statusCode;
            return;
        }

        await next(context);
    });
}

var version = Assembly.GetEntryAssembly()?
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
    .InformationalVersion ?? "unknown";

app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapGet("/version", () => Results.Ok(new { version }));

app.MapPost("/api/target", async (HttpContext context) =>
{
    string requestBody;

    try
    {
        using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
        requestBody = await reader.ReadToEndAsync(context.RequestAborted);
    }
    catch (BadHttpRequestException ex)
    {
        // Body over the limit, or the client disconnected mid-send. Kestrel already picked the
        // status code; without this catch every occurrence logs a full stack trace.
        log.LogWarning("Request rejected at transport level: {Reason}", ex.Message);
        context.Response.StatusCode = ex.StatusCode;
        return;
    }
    catch (OperationCanceledException)
    {
        log.LogInformation("Client aborted the request before the body was read.");
        return;
    }

    var isValid = JsonValidator.IsValidJson(requestBody);

    log.LogInformation(
        "Request received: {Bytes} bytes, valid={IsValid}, body={Body}",
        context.Request.ContentLength ?? -1,
        isValid,
        Truncate(requestBody, 512));

    context.Response.ContentType = "application/json; charset=utf-8";

    if (isValid)
    {
        context.Response.StatusCode = StatusCodes.Status200OK;
        await context.Response.WriteAsync("true", context.RequestAborted);
    }
    else
    {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync(
            "{\"ErrorMessage\": \"This is not JSON\"}", context.RequestAborted);
    }
});

app.Run();

static string Truncate(string value, int maxLength) =>
    value.Length <= maxLength
        ? value
        : string.Concat(value.AsSpan(0, maxLength), "[truncated]");

/// <summary>
/// Exposes the compiler-generated Program type to WebApplicationFactory&lt;Program&gt;.
/// Top-level statements emit it as internal, which the test project cannot reach (CS0122).
/// Must stay below every top-level statement, otherwise CS8803.
/// </summary>
public partial class Program { }
