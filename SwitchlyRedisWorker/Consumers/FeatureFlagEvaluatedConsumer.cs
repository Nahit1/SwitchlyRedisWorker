using System.Text.Json;
using MassTransit;
using StackExchange.Redis;
using Switchly.Application.Common.Interfaces;
using Switchly.Shared.Events;

namespace RedisWorker.Consumers;

public class FeatureFlagEvaluatedConsumer:IConsumer<FeatureFlagEvaluatedEvent>
{
  private readonly IDatabase _redis;
  private readonly ILogger<FeatureFlagEvaluatedConsumer> _logger;
  private readonly IRedisKeyProvider _keyProvider;
  private readonly IConnectionMultiplexer _connection;

  public FeatureFlagEvaluatedConsumer(IConnectionMultiplexer redis,IConnectionMultiplexer connection, ILogger<FeatureFlagEvaluatedConsumer> logger, IRedisKeyProvider keyProvider)
  {
    _redis = redis.GetDatabase();
    _logger = logger;
    _keyProvider = keyProvider;
    _connection = connection;
  }



  public async Task Consume(ConsumeContext<FeatureFlagEvaluatedEvent> context)
  {
    var pattern = $"{context.Message.RedisKeys}";
    Console.WriteLine(pattern);
    Console.WriteLine($"[Redis] → pattern: {pattern}");
    await DeleteKeysByPatternAsync(pattern);

    // var keysToDelete = new List<RedisKey>();
    //
    //
    // var endpoints = _connection.GetEndPoints();
    // foreach (var endpoint in endpoints)
    // {
    //   var server = _connection.GetServer(endpoint);
    //
    //   // Sadece bağlantısı olan ve replica olmayan (primary) node’larda işlem yap
    //   if (!server.IsConnected || server.IsReplica)
    //     continue;
    //
    //   var serverKeys = server.Keys(pattern: pattern);
    //   keysToDelete.AddRange(serverKeys);
    // }
    //
    //
    //
    // if (keysToDelete.Count > 0)
    // {
    //   await _redis.KeyDeleteAsync(keysToDelete.ToArray());
    //   //Console.WriteLine($"[Redis] {keysToDelete.Count} key silindi → pattern: {pattern}");
    // }
    // else
    // {
    //   Console.WriteLine($"[Redis] Silinecek key bulunamadı → pattern: {pattern}");
    // }
    var msg = context.Message;



    var result = new
    {
      FlagKey = msg.FlagKey,
      IsEnabled = msg.IsEnabled,
      Traits = msg.UserContextModel.Traits,
      Environment = msg.UserContextModel.Env,
    };

    var jsonData = JsonSerializer.Serialize(result);

    await _redis.StringSetAsync(
      msg.RedisKeys,
      jsonData,
      TimeSpan.FromHours(12)
    );

  }

  public async Task DeleteKeysByPatternAsync(string pattern)
  {
    var db = _connection.GetDatabase();

    // Tek bir aktif sunucuya bağlan
    var server = _connection.GetServer(_connection.GetEndPoints().First());

    if (!server.IsConnected)
    {
      Console.WriteLine($"[Redis] Sunucuya bağlanılamadı → {server.EndPoint}");
      return;
    }

    if (server.IsReplica)
    {
      Console.WriteLine($"[Redis] Replica node, işlem yapılmadı → {server.EndPoint}");
      return;
    }

    var keys = server.Keys(pattern: pattern).ToArray();

    if (keys.Length == 0)
    {
      Console.WriteLine($"[Redis] Silinecek key bulunamadı → pattern: {pattern}");
      return;
    }

    await db.KeyDeleteAsync(keys);

    Console.WriteLine($"[Redis] {keys.Length} key silindi → pattern: {pattern}");
  }

}
