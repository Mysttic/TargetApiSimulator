namespace TargetApiSimulator;

/// <summary>
/// Bound from the "Simulator" configuration section.
/// </summary>
public sealed class SimulatorOptions
{
    public const string SectionName = "Simulator";

    /// <summary>
    /// Opt-in for the X-Sim-* request headers. Off by default so that adding the feature cannot
    /// change how an existing caller is served.
    /// </summary>
    public bool EnableControlHeaders { get; set; }

    /// <summary>
    /// Upper bound for X-Sim-Delay-Ms. Without it a caller could pin a connection open for as
    /// long as it likes.
    /// </summary>
    public int MaxDelayMs { get; set; } = 60_000;
}
