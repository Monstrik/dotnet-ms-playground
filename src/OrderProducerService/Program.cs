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

builder.Services.AddSingleton<OrderProducerConfigStore>();
builder.Services.AddSingleton<ServiceControlState>();
builder.Services.AddSingleton<OrderProducerState>();
builder.Services.AddSingleton<RabbitMqClient>();
builder.Services.AddHostedService<OrderProducerWorker>();

var app = builder.Build();

app.UseCors("ClientOrigin");

app.MapGet("/", () => Results.Ok(new { service = "order-producer-service", status = "ok" }));

app.MapGet("/health", (ServiceControlState control) =>
	Results.Ok(new { status = "healthy", running = control.IsRunning }));

app.MapGet("/config", (OrderProducerConfigStore config) => Results.Ok(config.Get()));

app.MapPut("/config", (OrderProducerConfigUpdate request, OrderProducerConfigStore config) =>
{
	var result = config.TryUpdate(request);
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

app.MapGet("/stats", (OrderProducerState state, ServiceControlState control) =>
	Results.Ok(new
	{
		running = control.IsRunning,
		processingCount = 0,
		producedCount = state.ProducedCount,
		orders = state.GetOrders()
	}));

app.MapGet("/queues", (RabbitMqClient rabbitMq) =>
{
	var stats = rabbitMq.GetQueueStats();
	return Results.Ok(stats);
});

app.MapPost("/admin/reset", (RabbitMqClient rabbitMq, OrderProducerState state) =>
{
	rabbitMq.PurgeWorkflowQueues();
	state.Reset();
	return Results.Ok(new { cleared = true });
});

app.Run();

public sealed class OrderProducerWorker(
	OrderProducerConfigStore configStore,
	ServiceControlState controlState,
	OrderProducerState state,
	RabbitMqClient rabbitMq,
	ILogger<OrderProducerWorker> logger) : BackgroundService
{
	private static readonly string[] Menu =
	[
		"burger",
		"fries",
		"pizza",
		"sushi",
		"taco",
		"salad",
		"soda"
	];

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		double budget = 0;

		while (!stoppingToken.IsCancellationRequested)
		{
			if (!controlState.IsRunning)
			{
				await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
				continue;
			}

			var config = configStore.Get();
			budget += config.OrdersPerMinute / 60d;

			var ordersToProduce = (int)Math.Floor(budget);
			if (ordersToProduce < 1)
			{
				await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
				continue;
			}

			budget -= ordersToProduce;

			for (var index = 0; index < ordersToProduce; index++)
			{
				var order = CreateOrder();
				var payload = JsonSerializer.SerializeToUtf8Bytes(order);

				try
				{
					rabbitMq.Publish("orders.new", payload);
					state.MarkCreated(order);
				}
				catch (Exception exception)
				{
					logger.LogError(exception, "Failed to publish order {OrderId}", order.OrderId);
				}
			}

			await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
		}
	}

	private static NewOrderMessage CreateOrder()
	{
		var itemCount = Random.Shared.Next(1, 4);
		var items = new List<string>(itemCount);

		for (var index = 0; index < itemCount; index++)
		{
			items.Add(Menu[Random.Shared.Next(Menu.Length)]);
		}

		return new NewOrderMessage(Guid.NewGuid().ToString("N"), items, DateTimeOffset.UtcNow);
	}
}

public sealed class OrderProducerConfigStore(IConfiguration configuration)
{
	private readonly object _sync = new();
	private OrderProducerConfig _config = new(
		Math.Max(1, configuration.GetValue<int?>("ORDER_PRODUCER_ORDERS_PER_MINUTE") ?? 12));

	public OrderProducerConfig Get()
	{
		lock (_sync)
		{
			return _config;
		}
	}

	public ConfigUpdateResult<OrderProducerConfig> TryUpdate(OrderProducerConfigUpdate update)
	{
		if (update.OrdersPerMinute < 1 || update.OrdersPerMinute > 6000)
		{
			return ConfigUpdateResult<OrderProducerConfig>.Fail("ordersPerMinute must be between 1 and 6000");
		}

		lock (_sync)
		{
			_config = new OrderProducerConfig(update.OrdersPerMinute);
			return ConfigUpdateResult<OrderProducerConfig>.Ok(_config);
		}
	}
}

public sealed class ServiceControlState
{
	private int _running = 0; // Start in stopped state

	public bool IsRunning => Interlocked.CompareExchange(ref _running, 1, 1) == 1;
	public void Start() => Interlocked.Exchange(ref _running, 1);
	public void Stop() => Interlocked.Exchange(ref _running, 0);
}

public sealed class OrderProducerState
{
	private readonly ConcurrentDictionary<string, OrderStateItem> _orders = new();
	private long _producedCount;

	public long ProducedCount => Interlocked.Read(ref _producedCount);

	public void MarkCreated(NewOrderMessage order)
	{
		_orders[order.OrderId] = new OrderStateItem(order.OrderId, "created", order.CreatedAt, order.Items);
		Interlocked.Increment(ref _producedCount);

		if (_orders.Count > 1000)
		{
			var oldest = _orders.Values.OrderBy(item => item.Timestamp).Take(200).Select(item => item.OrderId).ToList();
			foreach (var orderId in oldest)
			{
				_orders.TryRemove(orderId, out _);
			}
		}
	}

	public IReadOnlyList<OrderStateItem> GetOrders() =>
		_orders.Values.OrderByDescending(item => item.Timestamp).Take(300).ToList();

	public void Reset()
	{
		_orders.Clear();
		Interlocked.Exchange(ref _producedCount, 0);
	}
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

	public QueueStatsSnapshot GetQueueStats()
	{
		lock (_sync)
		{
			var channel = GetChannel();
			return new QueueStatsSnapshot(
				GetQueueSnapshot(channel, "orders.new"),
				GetQueueSnapshot(channel, "orders.readyForDelivery"),
				GetQueueSnapshot(channel, "orders.delivered"));
		}
	}

	public void PurgeWorkflowQueues()
	{
		lock (_sync)
		{
			var channel = GetChannel();
			channel.QueuePurge("orders.new");
			channel.QueuePurge("orders.readyForDelivery");
			channel.QueuePurge("orders.delivered");
		}
	}

	private QueueStatItem GetQueueSnapshot(IModel channel, string queueName)
	{
		var state = channel.QueueDeclarePassive(queueName);
		return new QueueStatItem(queueName, state.MessageCount, state.ConsumerCount);
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

public record NewOrderMessage(string OrderId, IReadOnlyList<string> Items, DateTimeOffset CreatedAt);
public record OrderProducerConfig(int OrdersPerMinute);
public record OrderProducerConfigUpdate(int OrdersPerMinute);
public record OrderStateItem(string OrderId, string Status, DateTimeOffset Timestamp, IReadOnlyList<string> Items);
public record QueueStatItem(string Queue, uint Messages, uint Consumers);
public record QueueStatsSnapshot(QueueStatItem OrdersNew, QueueStatItem OrdersReadyForDelivery, QueueStatItem OrdersDelivered);

public sealed record ConfigUpdateResult<T>(bool Success, T? Value, string? Error)
{
	public static ConfigUpdateResult<T> Ok(T value) => new(true, value, null);
	public static ConfigUpdateResult<T> Fail(string error) => new(false, default, error);
}

public partial class Program;

