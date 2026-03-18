var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
	options.AddPolicy("ClientOrigin", policy =>
	{
		policy.WithOrigins(
			"http://localhost:8080",
			"http://localhost:5050",
			"http://localhost:5158",
			"https://localhost:7034")
			.AllowAnyHeader()
			.AllowAnyMethod();
	});
});

var app = builder.Build();

app.UseCors("ClientOrigin");

app.MapGet("/", () => Results.Ok(new { service = "echo-service", status = "ok" }));


app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapGet("/echo/{message}", (string message) => Results.Ok(new { message }));

app.MapPost("/echo", (EchoRequest request) => Results.Ok(new { message = request.Message }));

app.Run();

public record EchoRequest(string Message);

public partial class Program;

