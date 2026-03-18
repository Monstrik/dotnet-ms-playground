using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace EchoService.Tests;

public class EchoEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public EchoEndpointsTests(WebApplicationFactory<Program> factory)
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
    public async Task EchoGet_ReturnsSameMessage()
    {
        const string message = "hello-from-test";

        var payload = await _client.GetFromJsonAsync<EchoResponse>($"/echo/{message}");

        Assert.Equal(message, payload?.Message);
    }

    [Fact]
    public async Task EchoPost_ReturnsSameMessage()
    {
        const string message = "posted-message";

        var response = await _client.PostAsJsonAsync("/echo", new { message });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<EchoResponse>();
        Assert.Equal(message, payload?.Message);
    }

    private sealed record HealthResponse(string Status);
    private sealed record EchoResponse(string Message);
}