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

app.MapGet("/", () => Results.Ok(new { service = "weather-service", status = "ok" }));

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapGet("/weather", (string? city) => Results.Ok(BuildWeatherForecast(city ?? "Seattle")));

app.MapGet("/weather/{city}", (string city) => Results.Ok(BuildWeatherForecast(city)));

app.Run();

static WeatherForecastResponse BuildWeatherForecast(string city)
{
	var normalizedCity = string.IsNullOrWhiteSpace(city) ? "Seattle" : city.Trim();
	var summaries = new[]
	{
		"Sunny",
		"Cloudy",
		"Windy",
		"Rainy",
		"Cool",
		"Mild"
	};

	var seed = normalizedCity.Sum(character => character);
	var temperatureC = seed % 36 - 5;
	var summary = summaries[seed % summaries.Length];

	return new WeatherForecastResponse(
		normalizedCity,
		DateOnly.FromDateTime(DateTime.UtcNow),
		temperatureC,
		32 + (int)(temperatureC / 0.5556),
		summary);
}

public record WeatherForecastResponse(string City, DateOnly Date, int TemperatureC, int TemperatureF, string Summary);

public partial class Program;

