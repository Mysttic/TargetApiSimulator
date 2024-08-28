using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.MapPost("/api/target", async (HttpContext context) =>
{
	var requestBody = await new StreamReader(context.Request.Body).ReadToEndAsync();
	Console.WriteLine("Request received: " + requestBody);
	// Próba walidacji JSON z u¿yciem try-catch
	if (IsValidJson(requestBody))
	{
		context.Response.StatusCode = 200;
		await context.Response.WriteAsync("true");
	}
	else
	{
		context.Response.StatusCode = 400;
		await context.Response.WriteAsync("{\"ErrorMessage\": \"This is not JSON\"}");
	}
});

app.Run();

bool IsValidJson(string jsonString)
{
	if (string.IsNullOrWhiteSpace(jsonString))
	{
		return false;
	}

	try
	{
		using (JsonDocument.Parse(jsonString))
		{
			return true;
		}
	}
	catch (JsonException)
	{
		return false;
	}
}
