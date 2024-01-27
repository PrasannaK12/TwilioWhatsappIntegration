using StackExchange.Redis;

namespace WhatsappIntegration.caching
{
    public class rediscache
    {
        private static readonly string redisConnString = "caching-eastus.redis.cache.windows.net:6380,password=4zHAHXelaXOJ6UBZxHUGrekPyqAlkqukAAzCaDREtFU=,ssl=True,abortConnect=False";
        private static readonly ConnectionMultiplexer redis = ConnectionMultiplexer.Connect(redisConnString);

        public static string getValue (string key)
        {
            IDatabase db = redis.GetDatabase();
            if (db.KeyExists(key))
                return db.StringGet(key);
            else
                return null;
        }
        public static void setValue (string key, string value) 
        {
            IDatabase db = redis.GetDatabase();
            db.StringSet(key, value);
        }
    }
}
