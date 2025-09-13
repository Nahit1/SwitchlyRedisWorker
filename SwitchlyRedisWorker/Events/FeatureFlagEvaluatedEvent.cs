namespace SwitchlyRedisWorker.Events;

public record FeatureFlagEvaluatedEvent(
    Guid FlagId,
    string FlagKey,
    UserSegmentContextModel UserContextModel,
    bool IsEnabled,
    DateTime EvaluatedAt,
    string ClientKey,
    string RedisKeys // her zaman hashli olarak gönderilecek
);