var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseStaticFiles();

app.MapGet("/", () => Results.Ok(new { service = "echo-service", status = "ok" }));

app.MapGet("/client", () => Results.Redirect("/client/index.html"));

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapGet("/echo/{message}", (string message) => Results.Ok(new { message }));

app.MapPost("/echo", (EchoRequest request) => Results.Ok(new { message = request.Message }));

app.Run();

public record EchoRequest(string Message);

public partial class Program;

