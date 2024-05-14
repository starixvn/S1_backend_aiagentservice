using AIAgentService.Core.Models.Enums;
using StackExchange.Redis;

namespace AIAgentService.Core.Library;

public interface ICacheHelper
{
    TItem TryGetOrCreate<TItem>(string key, TimeSpan? timeSpan, Func<TItem> factory, RedisDataBaseEnum redisDataBase = RedisDataBaseEnum.Admin);
    Task<TItem> TryGetOrCreateAsync<TItem>(string key, TimeSpan? timeSpan, Func<Task<TItem>> factory, RedisDataBaseEnum redisDataBase = RedisDataBaseEnum.Admin);
    Task<bool> StringSetAsync(string key, RedisValue value, TimeSpan? timeSpan = null, RedisDataBaseEnum redisDataBase = RedisDataBaseEnum.Admin);
    bool KeyExists(string key, RedisDataBaseEnum redisDataBase = RedisDataBaseEnum.Admin);
    Task<bool> KeyExistsAsync(string key, RedisDataBaseEnum redisDataBase = RedisDataBaseEnum.Admin);
    Task<TItem> StringGetAsync<TItem>(string key, RedisDataBaseEnum redisDataBase = RedisDataBaseEnum.Admin);
    Task<string> StringGetAsync(string key, RedisDataBaseEnum redisDataBase = RedisDataBaseEnum.Admin);
    IEnumerable<TItem> GetCacheList<TItem>(IEnumerable<string> keys, RedisDataBaseEnum redisDataBase = RedisDataBaseEnum.Admin);
    bool SetCacheList<TItem>(Dictionary<string, TItem> dataList, TimeSpan? timeSpan, RedisDataBaseEnum redisDataBase = RedisDataBaseEnum.Admin);
    bool InsertList<TItem>(string key, TimeSpan? timeSpan, IEnumerable<TItem> list, RedisDataBaseEnum redisDataBase = RedisDataBaseEnum.Admin);
    IEnumerable<TItem> GetListByRange<TItem>(string key, long startIndex, long endIndex, RedisDataBaseEnum redisDataBase = RedisDataBaseEnum.Admin);
    Task<IEnumerable<TItem>> GetListByRangeAsync<TItem>(string key, long startIndex, long endIndex, RedisDataBaseEnum redisDataBase = RedisDataBaseEnum.Admin);
    long GetListLength(string key, RedisDataBaseEnum redisDataBase = RedisDataBaseEnum.Admin);
    bool KeyDelete(string key, RedisDataBaseEnum redisDataBase = RedisDataBaseEnum.Admin);
    long KeyDeleteByPattern(string keyPattern, RedisDataBaseEnum redisDataBase = RedisDataBaseEnum.Admin);
    Task<long> KeyDeleteByPatternAsync(string keyPattern, RedisDataBaseEnum redisDataBase = RedisDataBaseEnum.Admin);
    Task<bool> TryCreateLockAsync(RedisKey lockKey, RedisValue lockValue, TimeSpan? expiry, RedisDataBaseEnum redisDataBase = RedisDataBaseEnum.Admin);
    Task<bool> UnLockAsync(RedisKey lockKey, RedisValue lockValue, RedisDataBaseEnum redisDataBase = RedisDataBaseEnum.Admin);
}