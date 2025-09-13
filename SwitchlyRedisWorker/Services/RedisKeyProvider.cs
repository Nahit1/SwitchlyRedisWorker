using System.Security.Cryptography;
using System.Text;
using SwitchlyRedisWorker.Interfaces;

public class RedisKeyProvider : IRedisKeyProvider
{
    public string GetHashedKey(string clientKey, string flagKey, UserSegmentContextModel user)
    {
        // if (user.Traits.Count == 0)
        // {
        //   return $"{clientKey}:feature:{flagKey}";
        // }
        // // if (!hasSegmentRules)
        // //   return $"{clientKey}:feature:{flagKey}";
        //
        // var hash = GenerateContextHash(user);
        // return $"{clientKey}:feature:{flagKey}:env:{user.Env}:{hash}";
        return $"{clientKey}:feature:{flagKey}:env:{user.Env}";
    }

    private string GenerateContextHash(UserSegmentContextModel user)
    {
        var parts = new List<string>();

        if (user.Traits != null)
        {
            foreach (var kvp in user.Traits.OrderBy(k => k.Key))
                parts.Add($"{kvp.Key}={kvp.Value}");
        }

        var input = string.Join("&", parts);
        //return input;
        using var sha = SHA256.Create();
        var hashBytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hashBytes, 0, 6).Replace("-", "").ToLower(); // 12 karakter
    }
}