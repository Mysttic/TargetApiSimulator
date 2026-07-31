using Shouldly;

namespace TargetApiSimulator.Tests.Unit;

public class JsonValidatorTests
{
    [Theory]
    [InlineData("""{"a":1}""")]
    [InlineData("""{"orderId":42,"customer":"ACME"}""")]
    [InlineData("[1,2,3]")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("123")]
    [InlineData("-0.5e10")]
    [InlineData("\"str\"")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("null")]
    [InlineData("""{"a":1,"a":2}""")]
    [InlineData("""{"a":{"b":{"c":[1,2,{"d":null}]}}}""")]
    public void IsValidJson_WithWellFormedJson_ReturnsTrue(string input)
        => JsonValidator.IsValidJson(input).ShouldBeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    [InlineData("{")]
    [InlineData("""{"a":}""")]
    [InlineData("""{"a":1,}""")]
    [InlineData("""{'a':1}""")]
    [InlineData("""{"a":1} // comment""")]
    [InlineData("""{"a":1}{"b":2}""")]
    [InlineData("undefined")]
    [InlineData("NaN")]
    [InlineData("This is not JSON")]
    public void IsValidJson_WithMalformedInput_ReturnsFalse(string? input)
        => JsonValidator.IsValidJson(input).ShouldBeFalse();

    // Worth remembering: a U+FEFF character handed straight to the validator is rejected, but
    // BOM bytes arriving over the wire never reach it - StreamReader strips them first. That is
    // why the BOM case also has a separate integration test.
    [Fact]
    public void IsValidJson_WithBomCharacterPrefix_ReturnsFalse()
        => JsonValidator.IsValidJson("﻿{\"a\":1}").ShouldBeFalse();

    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    [InlineData(500, false)]
    public void IsValidJson_RespectsMaxDepth64(int depth, bool expected)
        => JsonValidator.IsValidJson(new string('[', depth) + new string(']', depth))
            .ShouldBe(expected);

    // The options are pinned explicitly in JsonValidator, so these stay false even if a future
    // .NET release changes the JsonDocumentOptions defaults.
    [Theory]
    [InlineData("""{"a":1,}""")]
    [InlineData("""[1,2,]""")]
    public void IsValidJson_RejectsTrailingCommas(string input)
        => JsonValidator.IsValidJson(input).ShouldBeFalse();

    [Theory]
    [InlineData("""{"a":1} // line comment""")]
    [InlineData("""{"a":1} /* block comment */""")]
    [InlineData("""{/* inline */"a":1}""")]
    public void IsValidJson_RejectsComments(string input)
        => JsonValidator.IsValidJson(input).ShouldBeFalse();
}
