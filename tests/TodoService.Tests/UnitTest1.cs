using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TodoService.Tests;

public class TodoEndpointsTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public TodoEndpointsTests(WebApplicationFactory<Program> factory)
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
    public async Task CreateTodo_ThenGetById_ReturnsCreatedItem()
    {
        var createResponse = await _client.PostAsJsonAsync("/todos", new { title = "buy milk" });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<TodoResponse>();
        Assert.NotNull(created);

        var fetched = await _client.GetFromJsonAsync<TodoResponse>($"/todos/{created.Id}");

        Assert.Equal(created.Id, fetched?.Id);
        Assert.Equal("buy milk", fetched?.Title);
        Assert.False(fetched?.IsCompleted);
    }

    [Fact]
    public async Task PatchAndDeleteTodo_WorkAsExpected()
    {
        var createResponse = await _client.PostAsJsonAsync("/todos", new { title = "call mom" });
        var created = await createResponse.Content.ReadFromJsonAsync<TodoResponse>();
        Assert.NotNull(created);

        var patchResponse = await _client.PatchAsJsonAsync($"/todos/{created.Id}", new { isCompleted = true });
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);
        var patched = await patchResponse.Content.ReadFromJsonAsync<TodoResponse>();
        Assert.True(patched?.IsCompleted);

        var deleteResponse = await _client.DeleteAsync($"/todos/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var notFoundResponse = await _client.GetAsync($"/todos/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, notFoundResponse.StatusCode);
    }

    private sealed record HealthResponse(string Status, string Storage);
    private sealed record TodoResponse(Guid Id, string Title, bool IsCompleted, DateTimeOffset CreatedAtUtc);
}