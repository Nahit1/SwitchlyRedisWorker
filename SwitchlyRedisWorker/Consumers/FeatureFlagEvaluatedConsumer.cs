using System.Text.Json;
using MassTransit;
using StackExchange.Redis;
using Switchly.Application.Common.Interfaces;
using Switchly.Shared.Events;


namespace RedisWorker.Consumers;

public class FeatureFlagEvaluatedConsumer:IConsumer<FeatureFlagEvaluatedEvent>
{
  private readonly IConnectionMultiplexer _mux;
  private readonly IDatabase _redis;
  private readonly ILogger<FeatureFlagEvaluatedConsumer> _logger;
  private readonly IRedisKeyProvider _keyProvider;
  private readonly IConnectionMultiplexer _connection;

  public FeatureFlagEvaluatedConsumer(IConnectionMultiplexer mux,IConnectionMultiplexer redis,IConnectionMultiplexer connection, ILogger<FeatureFlagEvaluatedConsumer> logger, IRedisKeyProvider keyProvider)
  {
    _mux = mux;
    // _redis = redis.GetDatabase();
    _redis = mux.GetDatabase();
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

    try
    {
      var ok = await _redis.StringSetAsync(
        msg.RedisKeys,
        jsonData,
        TimeSpan.FromHours(12),
        when: When.Always,
        flags: CommandFlags.DemandMaster);

      Console.WriteLine($"[REDIS] SET {msg.RedisKeys} => {(ok ? "OK" : "FAIL")}");
    }
    catch (Exception ex)
    {
      Console.WriteLine("[REDIS] SET EX: " + ex);
      throw; // MassTransit retry görsün istiyorsan bırakabilirsin
    }
    
    

  }

  public async Task DeleteKeysByPatternAsync(string pattern)
  {
    // NOTE:
    // - Program.cs tarafında ConnectionOptions.AllowAdmin = true olmalı
    // - Redis Cloud cluster ise tüm primary endpoint'leri dolaş
    // - Keys() aslında SCAN; pageSize ile taramayı sınırlıyoruz

    var endpoints = _mux.GetEndPoints();
    var toDelete = new List<RedisKey>();

    foreach (var ep in endpoints)
    {
      var server = _mux.GetServer(ep);
      if (!server.IsConnected || server.IsReplica)
        continue;

      // kontrollü tarama
      foreach (var key in server.Keys(pattern: pattern, pageSize: 1000))
        toDelete.Add(key);
    }

    if (toDelete.Count == 0)
    {
      _logger.LogInformation("[Redis] Silinecek key yok → pattern: {pattern}", pattern);
      return;
    }

    await _redis.KeyDeleteAsync(toDelete.ToArray());
    _logger.LogInformation("[Redis] {count} key silindi → pattern: {pattern}", toDelete.Count, pattern);
    // var db = _connection.GetDatabase();
    //
    // var server = _connection.GetServer(_connection.GetEndPoints().First());
    //
    // if (!server.IsConnected)
    // {
    //   Console.WriteLine($"[Redis] Sunucuya bağlanılamadı → {server.EndPoint}");
    //   return;
    // }
    //
    // if (server.IsReplica)
    // {
    //   Console.WriteLine($"[Redis] Replica node, işlem yapılmadı → {server.EndPoint}");
    //   return;
    // }
    //
    // var keys = server.Keys(pattern: pattern).ToArray();
    //
    // if (keys.Length == 0)
    // {
    //   Console.WriteLine($"[Redis] Silinecek key bulunamadı → pattern: {pattern}");
    //   return;
    // }
    //
    // await db.KeyDeleteAsync(keys);
    //
    // Console.WriteLine($"[Redis] {keys.Length} key silindi → pattern: {pattern}");
  }

}
