using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Superheroes.Application.Characters;
using Superheroes.Application.Ports;

namespace Superheroes.Application.Caching
{
    /// <summary>
    /// Decorates an ICharacterLoader with a HybridCache in front of it. Lives in Application
    /// (not an adapter project) because caching decorates a port rather than crossing an
    /// external boundary - it works with any ICharacterLoader, not just the S3 one.
    /// </summary>
    public class CachingCharacterLoader(
        ICharacterLoader inner,
        HybridCache cache,
        IOptions<CharactersCacheOptions> options,
        ILogger<CachingCharacterLoader> logger) : ICharacterLoader
    {
        private const string CacheKey = "characters";

        private readonly TimeSpan _cacheDuration = options.Value.CacheDuration;

        public async Task<CharacterCatalogue> GetCharacters()
        {
            if (_cacheDuration <= TimeSpan.Zero)
            {
                return await inner.GetCharacters();
            }

            // HybridCache.GetOrCreateAsync guarantees only one concurrent caller per key runs
            // this factory; every other caller waits for that result instead of also hitting the
            // source loader.
            var catalogue = await cache.GetOrCreateAsync(
                CacheKey,
                async cancellationToken =>
                {
                    var response = await inner.GetCharacters();
                    logger.LogInformation("Refreshed characters from source");
                    return response;
                },
                new HybridCacheEntryOptions
                {
                    Expiration = _cacheDuration,
                    LocalCacheExpiration = _cacheDuration
                });

            // HybridCache round-trips cached values through serialization (even for its local,
            // in-process tier - see CachingCharacterLoaderTests for where this was proven), and
            // that round trip does not preserve ImmutableDictionary's key comparer: a cache hit
            // would otherwise silently come back case-sensitive. Reapplying it here is cheap and
            // makes the guarantee independent of whatever HybridCache does internally.
            return new CharacterCatalogue(
                catalogue.Heroes.WithComparers(StringComparer.OrdinalIgnoreCase),
                catalogue.Villains.WithComparers(StringComparer.OrdinalIgnoreCase));
        }
    }
}
