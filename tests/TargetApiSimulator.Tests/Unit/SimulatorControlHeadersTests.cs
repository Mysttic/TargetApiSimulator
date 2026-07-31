using Shouldly;

namespace TargetApiSimulator.Tests.Unit;

public class SimulatorControlHeadersTests
{
    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("2500", 2500)]
    [InlineData("60000", 60000)]
    public void TryParseDelay_WithValidValue_ReturnsIt(string input, int expected)
    {
        SimulatorControlHeaders.TryParseDelay(input, 60_000, out var delay).ShouldBeTrue();
        delay.ShouldBe(expected);
    }

    [Fact]
    public void TryParseDelay_AboveMaximum_ClampsToMaximum()
    {
        SimulatorControlHeaders.TryParseDelay("999999", 60_000, out var delay).ShouldBeTrue();
        delay.ShouldBe(60_000);
    }

    // A negative maximum would otherwise produce a negative delay and make Task.Delay throw.
    [Fact]
    public void TryParseDelay_WithNegativeMaximum_ClampsToZero()
    {
        SimulatorControlHeaders.TryParseDelay("500", -1, out var delay).ShouldBeTrue();
        delay.ShouldBe(0);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("-1")]
    [InlineData("2.5")]
    [InlineData("2500ms")]
    [InlineData("abc")]
    [InlineData("+500")]
    [InlineData("99999999999999999999")]
    public void TryParseDelay_WithUnusableValue_ReturnsFalse(string? input)
    {
        SimulatorControlHeaders.TryParseDelay(input, 60_000, out var delay).ShouldBeFalse();
        delay.ShouldBe(0);
    }

    [Theory]
    [InlineData("100", 100)]
    [InlineData("200", 200)]
    [InlineData("418", 418)]
    [InlineData("503", 503)]
    [InlineData("599", 599)]
    public void TryParseStatus_WithValidCode_ReturnsIt(string input, int expected)
    {
        SimulatorControlHeaders.TryParseStatus(input, out var status).ShouldBeTrue();
        status.ShouldBe(expected);
    }

    // Anything outside 100-599 would make Kestrel throw when the response is written, so it is
    // rejected here rather than turned into a 500.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("99")]
    [InlineData("600")]
    [InlineData("1000")]
    [InlineData("-503")]
    [InlineData("50.3")]
    [InlineData("OK")]
    public void TryParseStatus_WithUnusableValue_ReturnsFalse(string? input)
    {
        SimulatorControlHeaders.TryParseStatus(input, out var status).ShouldBeFalse();
        status.ShouldBe(0);
    }
}
