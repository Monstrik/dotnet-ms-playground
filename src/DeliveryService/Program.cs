using System.Collections.Concurrent;
using System.Text.Json;
using RabbitMQ.Client;

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

builder.Services.AddSingleton<DeliveryConfigStore>();
builder.Services.AddSingleton<ServiceControlState>();
builder.Services.AddSingleton<DeliveryState>();
builder.Services.AddSingleton<RabbitMqClient>();
builder.Services.AddHostedService<DeliveryWorker>();

var app = builder.Build();

app.UseCors("ClientOrigin");

app.MapGet("/", () => Results.Ok(new { service = "delivery-service", status = "ok" }));

app.MapGet("/health", (ServiceControlState control) =>
	Results.Ok(new { status = "healthy", running = control.IsRunning }));

app.MapGet("/config", (DeliveryConfigStore configStore) => Results.Ok(configStore.Get()));

app.MapPut("/config", (DeliveryConfigUpdate request, DeliveryConfigStore configStore) =>
{
	var result = configStore.TryUpdate(request);
	return result.Success
		? Results.Ok(result.Value)
		: Results.BadRequest(new { error = result.Error });
});

app.MapPost("/control/start", (ServiceControlState control) =>
{
	control.Start();
	return Results.Ok(new { running = true });
});

app.MapPost("/control/stop", (ServiceControlState control) =>
{
	control.Stop();
	return Results.Ok(new { running = false });
});

app.MapPost("/admin/reset-state", (DeliveryState state) =>
{
	state.Reset();
	return Results.Ok(new { cleared = true });
});

app.MapGet("/stats", (DeliveryState state, ServiceControlState control) =>
	Results.Ok(new
	{
		running = control.IsRunning,
		processingCount = state.ProcessingCount,
		deliveredCount = state.DeliveredCount,
		beingDelivered = state.GetDeliveringOrders(),
		delivered = state.GetDeliveredOrders()
	}));

app.Run();

public sealed class DeliveryWorker(
	DeliveryConfigStore configStore,
	ServiceControlState controlState,
	DeliveryState state,
	RabbitMqClient rabbitMq,
	ILogger<DeliveryWorker> logger) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			if (!controlState.IsRunning)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
				continue;
			}

			RabbitDelivery? delivery;
			try
			{
				delivery = rabbitMq.TryGet("orders.readyForDelivery");
			}
			catch (Exception exception)
			{
				logger.LogError(exception, "Failed to consume from orders.readyForDelivery");
				await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
				continue;
			}

			if (delivery is null)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(300), stoppingToken);
				continue;
			}

			await ProcessOrderAsync(delivery, stoppingToken);
		}
	}

	private async Task ProcessOrderAsync(RabbitDelivery delivery, CancellationToken cancellationToken)
	{
		try
		{
			var order = JsonSerializer.Deserialize<ReadyForDeliveryMessage>(delivery.Body);
			if (order is null)
			{
				rabbitMq.Ack(delivery.DeliveryTag);
				return;
			}

			state.StartDelivering(order.OrderId);

			var config = configStore.Get();
			var duration = Random.Shared.Next(config.MinDeliverySeconds, config.MaxDeliverySeconds + 1);
			await Task.Delay(TimeSpan.FromSeconds(duration), cancellationToken);

			var delivered = new DeliveredMessage(order.OrderId, DateTimeOffset.UtcNow);
			rabbitMq.Publish("orders.delivered", JsonSerializer.SerializeToUtf8Bytes(delivered));

			state.MarkDelivered(order.OrderId);
			rabbitMq.Ack(delivery.DeliveryTag);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			rabbitMq.Nack(delivery.DeliveryTag, true);
		}
		catch (Exception exception)
		{
			logger.LogError(exception, "Order delivery failed");
			RetryOrDrop(delivery, "orders.readyForDelivery");
		}
	}

	private void RetryOrDrop(RabbitDelivery delivery, string queueName)
	{
		const int maxRetries = 3;
		var currentRetries = RabbitMqHeaders.GetRetryCount(delivery.Headers);

		if (currentRetries < maxRetries)
		{
			var headers = new Dictionary<string, object>
			{
				["x-retry-count"] = currentRetries + 1
			};
			rabbitMq.Publish(queueName, delivery.Body, headers);
		}
		else
		{
			logger.LogWarning("Dropping message after {RetryCount} retries", currentRetries);
		}

		rabbitMq.Ack(delivery.DeliveryTag);
	}
}

public sealed class DeliveryConfigStore(IConfiguration configuration)
{
	private readonly object _sync = new();
	private DeliveryConfig _config = new(
		Math.Max(1, configuration.GetValue<int?>("DELIVERY_MIN_SECONDS") ?? 1),
		Math.Max(2, configuration.GetValue<int?>("DELIVERY_MAX_SECONDS") ?? 5));

	public DeliveryConfig Get()
	{
		lock (_sync)
		{
			if (_config.MaxDeliverySeconds < _config.MinDeliverySeconds)
			{
				_config = _config with { MaxDeliverySeconds = _config.MinDeliverySeconds };
			}

			return _config;
		}
	}

	public ConfigUpdateResult<DeliveryConfig> TryUpdate(DeliveryConfigUpdate update)
	{
		if (update.MinDeliverySeconds < 1 || update.MaxDeliverySeconds < update.MinDeliverySeconds)
		{
			return ConfigUpdateResult<DeliveryConfig>.Fail("minDeliverySeconds must be >= 1 and <= maxDeliverySeconds");
		}

		if (update.MaxDeliverySeconds > 1800)
		{
			return ConfigUpdateResult<DeliveryConfig>.Fail("maxDeliverySeconds must be <= 1800");
		}

		lock (_sync)
		{
			_config = new DeliveryConfig(update.MinDeliverySeconds, update.MaxDeliverySeconds);
			return ConfigUpdateResult<DeliveryConfig>.Ok(_config);
		}
	}
}

public sealed class DeliveryState
{
	private readonly ConcurrentDictionary<string, DeliveryOrderStatus> _delivering = new();
	private readonly ConcurrentDictionary<string, DeliveryOrderStatus> _delivered = new();
	private long _processingCount;
	private long _deliveredCount;

	public long ProcessingCount => Interlocked.Read(ref _processingCount);
	public long DeliveredCount => Interlocked.Read(ref _deliveredCount);

	public void StartDelivering(string orderId)
	{
		_delivering[orderId] = new DeliveryOrderStatus(orderId, "being delivered", DateTimeOffset.UtcNow);
		Interlocked.Increment(ref _processingCount);
	}

	public void MarkDelivered(string orderId)
	{
		if (_delivering.TryRemove(orderId, out _))
		{
			_delivered[orderId] = new DeliveryOrderStatus(orderId, "delivered", DateTimeOffset.UtcNow);
		}
		else
		{
			_delivered[orderId] = new DeliveryOrderStatus(orderId, "delivered", DateTimeOffset.UtcNow);
		}

		Interlocked.Decrement(ref _processingCount);
		Interlocked.Increment(ref _deliveredCount);

		if (_delivered.Count > 1000)
		{
			var oldest = _delivered.Values.OrderBy(item => item.Timestamp).Take(200).Select(item => item.OrderId).ToList();
			foreach (var item in oldest)
			{
				_delivered.TryRemove(item, out _);
			}
		}
	}

	public IReadOnlyList<DeliveryOrderStatus> GetDeliveringOrders() =>
		_delivering.Values.OrderByDescending(item => item.Timestamp).Take(300).ToList();

	public IReadOnlyList<DeliveryOrderStatus> GetDeliveredOrders() =>
		_delivered.Values.OrderByDescending(item => item.Timestamp).Take(300).ToList();

	public void Reset()
	{
		_delivering.Clear();
		_delivered.Clear();
		Interlocked.Exchange(ref _processingCount, 0);
		Interlocked.Exchange(ref _deliveredCount, 0);
	}
}

public sealed class ServiceControlState
{
	private int _running = 0; // Start in stopped state

	public bool IsRunning => Interlocked.CompareExchange(ref _running, 1, 1) == 1;
	public void Start() => Interlocked.Exchange(ref _running, 1);
	public void Stop() => Interlocked.Exchange(ref _running, 0);
}

public sealed class RabbitMqClient : IDisposable
{
	private readonly IConfiguration _configuration;
	private readonly ILogger<RabbitMqClient> _logger;
	private readonly object _sync = new();
	private IConnection? _connection;
	private IModel? _channel;
	private bool _disposed;

	public RabbitMqClient(IConfiguration configuration, ILogger<RabbitMqClient> logger)
	{
		_configuration = configuration;
		_logger = logger;
	}

	public RabbitDelivery? TryGet(string queueName)
	{
		lock (_sync)
		{
			var channel = GetChannel();
			var result = channel.BasicGet(queueName, autoAck: false);
			if (result is null)
			{
				return null;
			}

			return new RabbitDelivery(result.DeliveryTag, result.Body.ToArray(), result.BasicProperties?.Headers);
		}
	}

	public void Ack(ulong deliveryTag)
	{
		lock (_sync)
		{
			GetChannel().BasicAck(deliveryTag, false);
		}
	}

	public void Nack(ulong deliveryTag, bool requeue)
	{
		lock (_sync)
		{
			GetChannel().BasicNack(deliveryTag, false, requeue);
		}
	}

	public void Publish(string queueName, ReadOnlySpan<byte> payload, IDictionary<string, object>? headers = null)
	{
		lock (_sync)
		{
			var channel = GetChannel();
			var properties = channel.CreateBasicProperties();
			properties.Persistent = true;
			properties.ContentType = "application/json";
			properties.Headers = headers;
			channel.BasicPublish("", queueName, false, properties, payload.ToArray());
		}
	}

	private IModel GetChannel()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(RabbitMqClient));
		}

		if (_connection is { IsOpen: true } && _channel is { IsOpen: true })
		{
			return _channel;
		}

		_channel?.Dispose();
		_connection?.Dispose();

		var host = _configuration["RABBITMQ_HOST"] ?? "localhost";
		var port = _configuration.GetValue<int?>("RABBITMQ_PORT") ?? 5672;
		var user = _configuration["RABBITMQ_USER"] ?? "app";
		var password = _configuration["RABBITMQ_PASS"] ?? "app";

		var factory = new ConnectionFactory
		{
			HostName = host,
			Port = port,
			UserName = user,
			Password = password,
			AutomaticRecoveryEnabled = true,
			NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
		};

		_connection = factory.CreateConnection();
		_channel = _connection.CreateModel();
		_channel.BasicQos(0, 1, false);
		_channel.QueueDeclare("orders.new", durable: true, exclusive: false, autoDelete: false, arguments: null);
		_channel.QueueDeclare("orders.readyForDelivery", durable: true, exclusive: false, autoDelete: false, arguments: null);
		_channel.QueueDeclare("orders.delivered", durable: true, exclusive: false, autoDelete: false, arguments: null);
		_logger.LogInformation("Connected to RabbitMQ at {Host}:{Port}", host, port);

		return _channel;
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		lock (_sync)
		{
			if (_disposed)
			{
				return;
			}

			_channel?.Dispose();
			_connection?.Dispose();
			_disposed = true;
		}
	}
}

public static class RabbitMqHeaders
{
	public static int GetRetryCount(IDictionary<string, object>? headers)
	{
		if (headers is null || !headers.TryGetValue("x-retry-count", out var value))
		{
			return 0;
		}

		return value switch
		{
			byte retryByte => retryByte,
			sbyte signedRetryByte => signedRetryByte,
			short retryShort => retryShort,
			int retryInt => retryInt,
			long retryLong => (int)retryLong,
			byte[] raw when int.TryParse(System.Text.Encoding.UTF8.GetString(raw), out var parsed) => parsed,
			_ => 0
		};
	}
}

public record RabbitDelivery(ulong DeliveryTag, byte[] Body, IDictionary<string, object>? Headers);
public record ReadyForDeliveryMessage(string OrderId, DateTimeOffset PreparedAt);
public record DeliveredMessage(string OrderId, DateTimeOffset DeliveredAt);
public record DeliveryConfig(int MinDeliverySeconds, int MaxDeliverySeconds);
public record DeliveryConfigUpdate(int MinDeliverySeconds, int MaxDeliverySeconds);
public record DeliveryOrderStatus(string OrderId, string Status, DateTimeOffset Timestamp);

public sealed record ConfigUpdateResult<T>(bool Success, T? Value, string? Error)
{
	public static ConfigUpdateResult<T> Ok(T value) => new(true, value, null);
	public static ConfigUpdateResult<T> Fail(string error) => new(false, default, error);
}

public partial class Program;

