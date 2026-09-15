using System;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Superheroes
{
    public class CachingCharactersProvider : ICharactersProvider
    {
        private const string CacheKey = "characters";

        private readonly ICharactersProvider _inner;
        private readonly HybridCache _cache;
        private readonly TimeSpan _cacheDuration;
        private readonly ILogger<CachingCharactersProvider> _logger;

        public CachingCharactersProvider(
            ICharactersProvider inner,
            HybridCache cache,
            IOptions<CharactersCacheOptions> options,
            ILogger<CachingCharactersProvider> logger)
        {
            _inner = inner;
            _cache = cache;
            _cacheDuration = options.Value.CacheDuration;
            _logger = logger;
        }

        public async Task<ImmutableDictionary<string, CharacterResponse>> GetCharacters()
        {
            if (_cacheDuration <= TimeSpan.Zero)
            {
                return await _inner.GetCharacters();
            }

            // HybridCache.GetOrCreateAsync guarantees only one concurrent caller per key runs
            // this factory; every other caller waits for that result instead of also hitting S3.
            var characters = await _cache.GetOrCreateAsync(
                CacheKey,
                async cancellationToken =>
                {
                    var response = await _inner.GetCharacters();
                    _logger.LogInformation("Refreshed characters from source");
                    return response;
                },
                new HybridCacheEntryOptions
                {
                    Expiration = _cacheDuration,
                    LocalCacheExpiration = _cacheDuration
                });

            // HybridCache round-trips cached values through serialization (even for its local,
            // in-process tier - see CachingCharactersProviderTests for where this was proven), and
            // that round trip does not preserve ImmutableDictionary's key comparer: a cache hit
            // would otherwise silently come back case-sensitive. Reapplying it here is cheap and
            // makes the guarantee independent of whatever HybridCache does internally.
            return characters.WithComparers(StringComparer.OrdinalIgnoreCase);
        }
    }
}
