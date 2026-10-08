using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using TechCurse.Application.Interfaces;

namespace TechCurse.Infrastructure.ExternalServices;

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _redisConnection;
    private readonly ICurrentUserService _currentUserService;

    public RedisCacheService(IDistributedCache cache, IConnectionMultiplexer redisConnection, ICurrentUserService currentUserService)
    {
        _cache = cache;
        _redisConnection = redisConnection;
        _currentUserService = currentUserService;
    }

    private string GetUserSpecificKey(string key)
    {
        var userId = _currentUserService.GetUserId();
        return $"{userId}:{key}";
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var cachedData = await _cache.GetStringAsync(GetUserSpecificKey(key));
        if (cachedData == null) return default;

        return JsonSerializer.Deserialize<T>(cachedData);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expirationTime = null)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expirationTime ?? TimeSpan.FromHours(1)
        };

        var serializedData = JsonSerializer.Serialize(value);
        await _cache.SetStringAsync(GetUserSpecificKey(key), serializedData, options);
    }

    public async Task RemoveAsync(string key)
    {
        await _cache.RemoveAsync(GetUserSpecificKey(key));
    }

    public async Task RemoveByPrefixAsync(string prefixKey)
    {
        var endpoints = _redisConnection.GetEndPoints();
        var server = _redisConnection.GetServer(endpoints.First());

        var keys = server.Keys(pattern: $"TechCurseAPI_{GetUserSpecificKey(prefixKey)}*").ToArray();

        var db = _redisConnection.GetDatabase();
        await db.KeyDeleteAsync(keys);
    }
}
