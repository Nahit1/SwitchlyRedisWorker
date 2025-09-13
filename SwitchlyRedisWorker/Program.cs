using MassTransit;
using RedisWorker.Consumers;
using StackExchange.Redis;
using SwitchlyRedisWorker;
using SwitchlyRedisWorker.Interfaces;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = Host.CreateDefaultBuilder(args)
  .ConfigureServices((context, services) =>
  {
    var config = context.Configuration;

    services.AddSingleton<IRedisKeyProvider, RedisKeyProvider>();

    // ---------------- Redis ----------------
    // ENV / appsettings key: Cache__Redis__Connection
    // Örn (local compose): "redis:6379,abortConnect=false"
    // Örn (Upstash/Prod): "<host>:6379,password=<pass>,ssl=true,abortConnect=false"
    var redisConn = config["Cache:Redis:Connection"] ?? "redis:6379,abortConnect=false";
    services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));

    // ---------------- MassTransit / RabbitMQ ----------------
    // İki kullanım desteklenir:
    // 1) URI (MessageBus__RabbitMq__Host = amqp://user:pass@host:5672/)
    // 2) Ayrı alanlar (RabbitMQ__Host, RabbitMQ__User, RabbitMQ__Pass)
    var rabbitUri  = config["RabbitMq:Url"]; // amqp://... (opsiyonel)
    var rabbitHost = config["RabbitMQ:Host"] ?? "rabbitmq";
    var rabbitUser = config["RabbitMQ:User"] ?? "guest";
    var rabbitPass = config["RabbitMQ:Pass"] ?? "guest";

    services.AddMassTransit(x =>
    {
      // Consumer'ı kaydet
      x.AddConsumer<FeatureFlagEvaluatedConsumer>();

      x.UsingRabbitMq((ctx, cfg) =>
      {
        if (!string.IsNullOrWhiteSpace(rabbitUri) && rabbitUri.StartsWith("amqp", StringComparison.OrdinalIgnoreCase))
        {
          // URI ile konfig (CloudAMQP/Prod için ideal)
          cfg.Host(new Uri(rabbitUri));
        }
        else
        {
          // Host/User/Pass ile konfig (local compose için ideal)
          cfg.Host(rabbitHost, "/", h =>
          {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
          });
        }

        // Bağlantı/işleme retry (kuyruk geç açılırsa düşmesin)
        cfg.UseMessageRetry(r => r.Interval(5, TimeSpan.FromSeconds(5)));

        // Queue/endpoint → consumer eşlemesi
        cfg.ReceiveEndpoint("feature-flag-evaluated", e =>
        {
          e.ConfigureConsumer<FeatureFlagEvaluatedConsumer>(ctx);
        });
      });
    });

    // Hosted service (Worker)
    services.AddHostedService<Worker>();
  })
  .Build();
host.Run();