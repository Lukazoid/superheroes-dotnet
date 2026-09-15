using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Superheroes
{
    public class CachingCharactersProvider : ICharactersProvider
    {
        public const string SourceProviderKey = "characters-source";

        private const string CacheKey = "characters";

        private readonly ICharactersProvider _inner;
        private readonly IMemoryCache _cache;
        private readonly TimeSpan _cacheDuration;
        private readonly ILogger<CachingCharactersProvider> _logger;
        private readonly SemaphoreSlim _refreshLock = new SemaphoreSlim(1, 1);

        public CachingCharactersProvider(
            [FromKeyedServices(SourceProviderKey)] ICharactersProvider inner,
            IMemoryCache cache,
            IOptions<CharactersCacheOptions> options,
            ILogger<CachingCharactersProvider> logger)
        {
            _inner = inner;
            _cache = cache;
            _cacheDuration = options.Value.CacheDuration;
            _logger = logger;
        }

        public async Task<CharactersResponse> GetCharacters()
        {
            if (_cacheDuration <= TimeSpan.Zero)
            {
                return await _inner.GetCharacters();
            }

            if (_cache.TryGetValue(CacheKey, out CharactersResponse cached))
            {
                return cached;
            }

            await _refreshLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(CacheKey, out cached))
                {
                    return cached;
                }

                var response = await _inner.GetCharacters();

                _cache.Set(CacheKey, response, _cacheDuration);
                _logger.LogInformation("Refreshed characters from source");

                return response;
            }
            finally
            {
                _refreshLock.Release();
            }
        }
    }
}
