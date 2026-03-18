using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace WeatherService.Tests;

public class WeatherEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
  private readonly HttpClient _client;

  public WeatherEndpointsTests(WebApplicationFactory<Program> factory)
  {
    _client = factory.CreateClient();
  }

  [Fact]
  public async Task Health_ReturnsHealthy()
  {
    var response = await _client.GetAsync("/health");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var payload = await response.Content.ReadFromJsonAsync<HealthResponse>();
    Assert.Equal("healthy", payload?.Status);
  }

  [Fact]
  public async Task WeatherByCity_ReturnsForecastForRequestedCity()
  {
    var payload = await _client.GetFromJsonAsync<WeatherResponse>("/weather/Tokyo");

    Assert.Equal("Tokyo", payload?.City);
    Assert.False(string.IsNullOrWhiteSpace(payload?.Summary));
  }

  [Fact]
  public async Task WeatherWithoutCity_UsesDefaultCity()
  {
    var payload = await _client.GetFromJsonAsync<WeatherResponse>("/weather");

    Assert.Equal("Seattle", payload?.City);
  }

  private sealed record HealthResponse(string Status);
  private sealed record WeatherResponse(string City, string Date, int TemperatureC, int TemperatureF, string Summary);
}