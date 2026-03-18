using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
	options.AddPolicy("ClientOrigin", policy =>
	{
		policy.WithOrigins(
			"http://localhost:8080",
			"http://localhost:8086",
			"http://localhost:8087",
			"http://localhost:5050",
			"http://localhost:5158",
			"https://localhost:7034")
			.AllowAnyHeader()
			.AllowAnyMethod();
	});
});

var connectionString = builder.Configuration["TODO_DB_CONNECTION"]
	?? builder.Configuration.GetConnectionString("TodoDb");

if (string.IsNullOrWhiteSpace(connectionString))
{
	builder.Services.AddSingleton<ITodoRepository, InMemoryTodoRepository>();
}
else
{
	builder.Services.AddSingleton<ITodoRepository>(_ => new PostgresTodoRepository(connectionString));
}

var app = builder.Build();

app.UseCors("ClientOrigin");

app.MapGet("/", () => Results.Ok(new { service = "todo-service", status = "ok" }));

app.MapGet("/health", (ITodoRepository repository) =>
	Results.Ok(new { status = "healthy", storage = repository.StorageName }));

app.MapGet("/todos", async (ITodoRepository repository, CancellationToken cancellationToken) =>
{
	var items = await repository.GetAllAsync(cancellationToken);
	return Results.Ok(items);
});

app.MapGet("/todos/{id:guid}", async (Guid id, ITodoRepository repository, CancellationToken cancellationToken) =>
{
	var item = await repository.GetByIdAsync(id, cancellationToken);
	return item is null ? Results.NotFound() : Results.Ok(item);
});

app.MapPost("/todos", async (CreateTodoRequest request, ITodoRepository repository, CancellationToken cancellationToken) =>
{
	if (string.IsNullOrWhiteSpace(request.Title))
	{
		return Results.BadRequest(new { error = "title is required" });
	}

	var item = await repository.CreateAsync(request.Title.Trim(), cancellationToken);
	return Results.Created($"/todos/{item.Id}", item);
});

app.MapPatch("/todos/{id:guid}", async (Guid id, UpdateTodoRequest request, ITodoRepository repository, CancellationToken cancellationToken) =>
{
	var updated = await repository.SetCompletedAsync(id, request.IsCompleted, cancellationToken);
	return updated is null ? Results.NotFound() : Results.Ok(updated);
});

app.MapDelete("/todos/{id:guid}", async (Guid id, ITodoRepository repository, CancellationToken cancellationToken) =>
{
	var deleted = await repository.DeleteAsync(id, cancellationToken);
	return deleted ? Results.NoContent() : Results.NotFound();
});

var todoRepository = app.Services.GetRequiredService<ITodoRepository>();
await todoRepository.EnsureInitializedAsync();

app.Run();

public record TodoItem(Guid Id, string Title, bool IsCompleted, DateTimeOffset CreatedAtUtc);
public record CreateTodoRequest(string Title);
public record UpdateTodoRequest(bool IsCompleted);

public interface ITodoRepository
{
	string StorageName { get; }
	Task EnsureInitializedAsync(CancellationToken cancellationToken = default);
	Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<TodoItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
	Task<TodoItem> CreateAsync(string title, CancellationToken cancellationToken = default);
	Task<TodoItem?> SetCompletedAsync(Guid id, bool isCompleted, CancellationToken cancellationToken = default);
	Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class InMemoryTodoRepository : ITodoRepository
{
	private readonly List<TodoItem> _items = [];
	private readonly object _sync = new();

	public string StorageName => "memory";

	public Task EnsureInitializedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

	public Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		lock (_sync)
		{
			return Task.FromResult<IReadOnlyList<TodoItem>>(_items.OrderBy(item => item.CreatedAtUtc).ToList());
		}
	}

	public Task<TodoItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		lock (_sync)
		{
			return Task.FromResult(_items.FirstOrDefault(item => item.Id == id));
		}
	}

	public Task<TodoItem> CreateAsync(string title, CancellationToken cancellationToken = default)
	{
		var item = new TodoItem(Guid.NewGuid(), title, false, DateTimeOffset.UtcNow);

		lock (_sync)
		{
			_items.Add(item);
		}

		return Task.FromResult(item);
	}

	public Task<TodoItem?> SetCompletedAsync(Guid id, bool isCompleted, CancellationToken cancellationToken = default)
	{
		lock (_sync)
		{
			var existingIndex = _items.FindIndex(item => item.Id == id);
			if (existingIndex < 0)
			{
				return Task.FromResult<TodoItem?>(null);
			}

			var existing = _items[existingIndex];
			var updated = existing with { IsCompleted = isCompleted };
			_items[existingIndex] = updated;
			return Task.FromResult<TodoItem?>(updated);
		}
	}

	public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		lock (_sync)
		{
			var removed = _items.RemoveAll(item => item.Id == id);
			return Task.FromResult(removed > 0);
		}
	}
}

public sealed class PostgresTodoRepository(string connectionString) : ITodoRepository
{
	public string StorageName => "postgres";

	public async Task EnsureInitializedAsync(CancellationToken cancellationToken = default)
	{
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken);

		await using var command = new NpgsqlCommand(
			"""
			CREATE TABLE IF NOT EXISTS todos (
				id UUID PRIMARY KEY,
				title TEXT NOT NULL,
				is_completed BOOLEAN NOT NULL,
				created_at_utc TIMESTAMPTZ NOT NULL
			);
			""",
			connection);

		await command.ExecuteNonQueryAsync(cancellationToken);
	}

	public async Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken cancellationToken = default)
	{
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken);

		await using var command = new NpgsqlCommand(
			"SELECT id, title, is_completed, created_at_utc FROM todos ORDER BY created_at_utc",
			connection);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		var items = new List<TodoItem>();

		while (await reader.ReadAsync(cancellationToken))
		{
			items.Add(new TodoItem(
				reader.GetGuid(0),
				reader.GetString(1),
				reader.GetBoolean(2),
				reader.GetFieldValue<DateTime>(3)));
		}

		return items;
	}

	public async Task<TodoItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
	{
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken);

		await using var command = new NpgsqlCommand(
			"SELECT id, title, is_completed, created_at_utc FROM todos WHERE id = @id",
			connection);
		command.Parameters.AddWithValue("id", id);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		if (!await reader.ReadAsync(cancellationToken))
		{
			return null;
		}

		return new TodoItem(
			reader.GetGuid(0),
			reader.GetString(1),
			reader.GetBoolean(2),
			reader.GetFieldValue<DateTime>(3));
	}

	public async Task<TodoItem> CreateAsync(string title, CancellationToken cancellationToken = default)
	{
		var newItem = new TodoItem(Guid.NewGuid(), title, false, DateTimeOffset.UtcNow);

		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken);

		await using var command = new NpgsqlCommand(
			"INSERT INTO todos (id, title, is_completed, created_at_utc) VALUES (@id, @title, @isCompleted, @createdAtUtc)",
			connection);
		command.Parameters.AddWithValue("id", newItem.Id);
		command.Parameters.AddWithValue("title", newItem.Title);
		command.Parameters.AddWithValue("isCompleted", newItem.IsCompleted);
		command.Parameters.AddWithValue("createdAtUtc", newItem.CreatedAtUtc.UtcDateTime);

		await command.ExecuteNonQueryAsync(cancellationToken);
		return newItem;
	}

	public async Task<TodoItem?> SetCompletedAsync(Guid id, bool isCompleted, CancellationToken cancellationToken = default)
	{
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken);

		await using var command = new NpgsqlCommand(
			"UPDATE todos SET is_completed = @isCompleted WHERE id = @id RETURNING id, title, is_completed, created_at_utc",
			connection);
		command.Parameters.AddWithValue("id", id);
		command.Parameters.AddWithValue("isCompleted", isCompleted);

		await using var reader = await command.ExecuteReaderAsync(cancellationToken);
		if (!await reader.ReadAsync(cancellationToken))
		{
			return null;
		}

		return new TodoItem(
			reader.GetGuid(0),
			reader.GetString(1),
			reader.GetBoolean(2),
			reader.GetFieldValue<DateTime>(3));
	}

	public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
	{
		await using var connection = new NpgsqlConnection(connectionString);
		await connection.OpenAsync(cancellationToken);

		await using var command = new NpgsqlCommand("DELETE FROM todos WHERE id = @id", connection);
		command.Parameters.AddWithValue("id", id);

		var affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);
		return affectedRows > 0;
	}
}

public partial class Program;

