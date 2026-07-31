using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;

namespace TargetApiSimulator.Tests.Integration;

/// <summary>
/// Characterisation tests: every assertion here pins the behaviour the service has TODAY,
/// so that any later refactor shows up as a changed assertion in the diff rather than as a
/// silent regression. Rows marked "Currently" encode a known defect, not a desired contract.
/// </summary>
public class TargetEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Route = "/api/target";
    private const string ErrorBody = """{"ErrorMessage": "This is not JSON"}""";

    private readonly HttpClient _client;

    public TargetEndpointTests(WebApplicationFactory<Program> factory) => _client = factory.CreateClient();

    private Task<HttpResponseMessage> PostAsync(string body, string contentType = "application/json")
        => PostAsync(Encoding.UTF8.GetBytes(body), contentType);

    private Task<HttpResponseMessage> PostAsync(byte[] body, string? contentType = "application/json")
    {
        var content = new ByteArrayContent(body);
        content.Headers.ContentType = contentType is null ? null : MediaTypeHeaderValue.Parse(contentType);
        return _client.PostAsync(Route, content);
    }

    [Theory]
    [InlineData("""{"a":1}""")]
    [InlineData("""{"orderId":42,"customer":"ACME"}""")]
    [InlineData("[1,2,3]")]
    [InlineData("{}")]
    [InlineData("[]")]
    public async Task Post_WithWellFormedJson_Returns200AndLiteralTrue(string body)
    {
        var response = await PostAsync(body);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("true");
    }

    // A bare scalar is a complete JSON document per RFC 8259 section 2. Accepting it is a
    // deliberate contract decision, not an oversight.
    [Theory]
    [InlineData("123")]
    [InlineData("-0.5e10")]
    [InlineData("\"str\"")]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("null")]
    public async Task Post_WithBareScalar_Returns200(string body)
    {
        var response = await PostAsync(body);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("true");
    }

    [Fact]
    public async Task Post_WithDuplicateKeys_Returns200()
    {
        var response = await PostAsync("""{"a":1,"a":2}""");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("", "empty body")]
    [InlineData("   \t\r\n ", "whitespace only")]
    [InlineData("""{"a":}""", "malformed value")]
    [InlineData("""{"a":1,}""", "trailing comma")]
    [InlineData("""{"a":1} // trailing comment""", "comment")]
    [InlineData("""{'a':1}""", "single quotes")]
    [InlineData("This is not JSON", "bare word")]
    [InlineData("undefined", "javascript literal")]
    [InlineData("NaN", "non-finite number")]
    [InlineData("{", "unterminated object")]
    [InlineData("""{"a":1}{"b":2}""", "two concatenated documents")]
    public async Task Post_WithInvalidJson_Returns400AndErrorContract(string body, string because)
    {
        var response = await PostAsync(body);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, because);
        (await response.Content.ReadAsStringAsync()).ShouldBe(ErrorBody, because);
    }

    // JsonDocument defaults to a maximum depth of 64. The resulting exception derives from
    // JsonException, so it lands in the existing catch and produces 400, never 500.
    [Theory]
    [InlineData(64, HttpStatusCode.OK)]
    [InlineData(65, HttpStatusCode.BadRequest)]
    [InlineData(500, HttpStatusCode.BadRequest)]
    public async Task Post_WithDeeplyNestedJson_RespectsMaxDepth64(int depth, HttpStatusCode expected)
    {
        var response = await PostAsync(new string('[', depth) + new string(']', depth));

        response.StatusCode.ShouldBe(expected);
    }

    // The handler never inspects Content-Type. Pinning this stops a future "let's validate the
    // header" change from breaking callers that post JSON as text/plain.
    [Theory]
    [InlineData("application/json")]
    [InlineData("text/plain")]
    [InlineData("application/xml")]
    [InlineData("application/octet-stream")]
    [InlineData(null)]
    public async Task Post_IgnoresContentTypeEntirely(string? contentType)
    {
        var response = await PostAsync(Encoding.UTF8.GetBytes("""{"a":1}"""), contentType);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_WithUtf8BomPrefix_Returns200()
    {
        var body = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("""{"a":1}""")).ToArray();

        var response = await PostAsync(body);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // StreamReader is constructed with detectEncodingFromByteOrderMarks enabled by default,
    // so a UTF-16 payload that carries its preamble is decoded rather than rejected.
    [Fact]
    public async Task Post_WithUtf16BodyAndPreamble_Returns200()
    {
        var body = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("""{"a":1}""")).ToArray();

        var response = await PostAsync(body);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("OPTIONS")]
    public async Task Request_WithMethodOtherThanPost_Returns405WithAllowHeader(string method)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), Route);

        var response = await _client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
        response.Content.Headers.Allow.ShouldContain("POST");
    }

    [Theory]
    [InlineData("/unknown")]
    [InlineData("/")]
    [InlineData("/api")]
    [InlineData("/api/target/extra")]
    public async Task Post_ToUnmappedPath_Returns404(string path)
    {
        var response = await _client.PostAsync(path, new StringContent("""{"a":1}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("/API/TARGET")]
    [InlineData("/Api/Target")]
    [InlineData("/api/target/")]
    public async Task Post_ToRouteVariant_Returns200(string path)
    {
        var response = await _client.PostAsync(path, new StringContent("""{"a":1}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Post_BothBranches_DeclareJsonContentType()
    {
        var ok = await PostAsync("""{"a":1}""");
        var bad = await PostAsync("nope");

        ok.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        ok.Content.Headers.ContentType?.CharSet.ShouldBe("utf-8");
        bad.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        bad.Content.Headers.ContentType?.CharSet.ShouldBe("utf-8");
    }

    [Fact]
    public async Task GetHealthz_Returns200AndOkStatus()
    {
        var response = await _client.GetAsync("/healthz");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"status":"ok"}""");
    }

    [Fact]
    public async Task GetVersion_Returns200AndNonEmptyVersion()
    {
        var response = await _client.GetAsync("/version");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldStartWith("{\"version\":\"");
        body.ShouldNotContain("\"version\":\"\"");
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/version")]
    public async Task Post_ToReadOnlyEndpoint_Returns405(string path)
    {
        var response = await _client.PostAsync(path, new StringContent("""{"a":1}"""));

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }
}
