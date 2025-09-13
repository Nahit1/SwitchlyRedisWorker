namespace SwitchlyRedisWorker.Interfaces;

public interface IRedisKeyProvider
{
    string GetHashedKey(string clientKey, string flagKey, UserSegmentContextModel userContext);
}