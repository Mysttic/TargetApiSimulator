using System.Globalization;

namespace TargetApiSimulator;

/// <summary>
/// Parsing for the X-Sim-* control headers. Kept separate from the middleware so the rules are
/// unit-testable without an HTTP round trip.
/// </summary>
public static class SimulatorControlHeaders
{
    public const string Delay = "X-Sim-Delay-Ms";
    public const string Status = "X-Sim-Status";

    /// <summary>
    /// Echoed back when a control header took effect, so a test can tell "the stub injected
    /// this" apart from "my own code produced this".
    /// </summary>
    public const string Applied = "X-Sim-Applied";

    /// <summary>
    /// Parses a delay in milliseconds, clamped to <paramref name="maxDelayMs"/>.
    /// Negative, non-numeric and empty values are rejected rather than coerced.
    /// </summary>
    public static bool TryParseDelay(string? value, int maxDelayMs, out int delayMs)
    {
        delayMs = 0;

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed < 0)
        {
            return false;
        }

        delayMs = Math.Min(parsed, Math.Max(maxDelayMs, 0));
        return true;
    }

    /// <summary>
    /// Parses a status code. Only 100-599 is accepted; anything else would make Kestrel throw
    /// when the response is written.
    /// </summary>
    public static bool TryParseStatus(string? value, out int statusCode)
    {
        statusCode = 0;

        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        if (parsed is < 100 or > 599)
        {
            return false;
        }

        statusCode = parsed;
        return true;
    }
}
