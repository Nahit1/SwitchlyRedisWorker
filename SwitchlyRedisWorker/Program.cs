using System.Security.Authentication;
using MassTransit;
using RedisWorker.Consumers;
using StackExchange.Redis;
using Switchly.Application.Common.Interfaces;
using SwitchlyRedisWorker;
using Switchly.Shared.Events;
using Switchly.Application.Common.Interfaces;


var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = Host.CreateDefaultBuilder(args)
  .ConfigureServices((context, services) =>
  {
    var config = context.Configuration;

    var host = "redis-17749.c322.us-east-1-2.ec2.redns.redis-cloud.com";
    var port = 17749;
    var user = "default";
    var pass = "dd3nbbZHmrODZP4TOk8mwuU17KJ8Yjgy"; // panelden rotate etmeni öneririm

    var opts = new ConfigurationOptions {
      EndPoints = { { host, port } },
      User = user,
      Password = pass,
      Ssl = false,                  // 🔴 TLS KAPALI (çünkü redis:// ile PONG aldın)
      AbortOnConnectFail = false,
      ResolveDns = true,
      AllowAdmin = true,            // SCAN/KEYS gibi komutlar için işine yarar
      ConnectRetry = 5,
      ConnectTimeout = 15000,
      SyncTimeout = 15000
    };
    
    Console.WriteLine($"[REDIS] Connecting TLS… host={host} user={user}");
    var mux = ConnectionMultiplexer.Connect(opts);
    services.AddSingleton<IConnectionMultiplexer>(mux);

    // Diğer bağımlılıklar
    services.AddSingleton<IRedisKeyProvider, RedisKeyProvider>();

    // ---------------- MassTransit / RabbitMQ ----------------
    // İki kullanım desteklenir:
    // 1) URI (MessageBus__RabbitMq__Host = amqp://user:pass@host:5672/)
    // 2) Ayrı alanlar (RabbitMQ__Host, RabbitMQ__User, RabbitMQ__Pass)
    var rabbitUri  = config["RabbitMq:Url"]; // amqp://... (opsiyonel)
    var rabbitHost = config["RabbitMQ:Host"] ?? "localhost";
    var rabbitUser = config["RabbitMQ:User"] ?? "guest";
    var rabbitPass = config["RabbitMQ:Pass"] ?? "guest";

    services.AddMassTransit(x =>
    {
      // Consumer'ı kaydet
      x.AddConsumer<FeatureFlagEvaluatedConsumer>();

      x.UsingRabbitMq((ctx, cfg) =>
      {
        // cfg.Host(rabbitHost, "/", h =>
        // {
        //   h.Username(rabbitUser);
        //   h.Password(rabbitPass);
        // });
        
        cfg.Host(new Uri(rabbitUri));
        
       

        // Bağlantı/işleme retry (kuyruk geç açılırsa düşmesin)
        cfg.UseMessageRetry(r => r.Interval(5, TimeSpan.FromSeconds(5)));

        // Queue/endpoint → consumer eşlemesi
        cfg.ReceiveEndpoint("feature-flag-evaluated", e =>
        {
          e.ConfigureConsumer<FeatureFlagEvaluatedConsumer>(ctx);
          e.Bind<FeatureFlagEvaluatedEvent>();
        });
      });
    });

    // Hosted service (Worker)
    services.AddHostedService<Worker>();
  })
  .Build();
host.Run();