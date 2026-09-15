using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Superheroes.Tests
{
    public class CachingCharactersProviderTests
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        private class FakeSystemClock : ISystemClock
        {
            public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.UtcNow;
        }

        private class ThrowingCharactersProvider : ICharactersProvider
        {
            public Task<CharactersResponse> GetCharacters()
            {
                throw new InvalidOperationException("S3 is unavailable");
            }
        }

        private static CharactersResponse SomeResponse() => new CharactersResponse
        {
            Items = new[]
            {
                new CharacterResponse { Name = "Batman", Score = 8.3, Type = "hero" }
            }
        };

        private static (MemoryCache Cache, FakeSystemClock Clock) NewCache()
        {
            var clock = new FakeSystemClock();
            var cache = new MemoryCache(new MemoryCacheOptions { Clock = clock });
            return (cache, clock);
        }

        [Fact]
        public async Task ReturnsCachedResponseWithinDuration()
        {
            var (cache, _) = NewCache();
            var inner = new FakeCharactersProvider();
            inner.FakeResponse(SomeResponse());
            var sut = new CachingCharactersProvider(inner, cache, CacheDuration, NullLogger<CachingCharactersProvider>.Instance);

            var first = await sut.GetCharacters();
            var second = await sut.GetCharacters();

            inner.CallCount.ShouldBe(1);
            first.ShouldBeSameAs(second);
        }

        [Fact]
        public async Task RefetchesAfterDurationExpires()
        {
            var (cache, clock) = NewCache();
            var inner = new FakeCharactersProvider();
            inner.FakeResponse(SomeResponse());
            var sut = new CachingCharactersProvider(inner, cache, CacheDuration, NullLogger<CachingCharactersProvider>.Instance);

            await sut.GetCharacters();
            clock.UtcNow += CacheDuration + TimeSpan.FromSeconds(1);
            await sut.GetCharacters();

            inner.CallCount.ShouldBe(2);
        }

        [Fact]
        public async Task ConcurrentCallsOnColdCacheOnlyFetchOnce()
        {
            var (cache, _) = NewCache();
            var inner = new FakeCharactersProvider();
            inner.FakeResponse(SomeResponse());
            var sut = new CachingCharactersProvider(inner, cache, CacheDuration, NullLogger<CachingCharactersProvider>.Instance);

            await Task.WhenAll(
                sut.GetCharacters(),
                sut.GetCharacters(),
                sut.GetCharacters(),
                sut.GetCharacters(),
                sut.GetCharacters());

            inner.CallCount.ShouldBe(1);
        }

        [Fact]
        public async Task PropagatesFailureAndDoesNotPoisonTheCache()
        {
            var (cache, _) = NewCache();
            var throwing = new ThrowingCharactersProvider();
            var sut = new CachingCharactersProvider(throwing, cache, CacheDuration, NullLogger<CachingCharactersProvider>.Instance);

            await Should.ThrowAsync<InvalidOperationException>(() => sut.GetCharacters());

            var inner = new FakeCharactersProvider();
            inner.FakeResponse(SomeResponse());
            var recovered = new CachingCharactersProvider(inner, cache, CacheDuration, NullLogger<CachingCharactersProvider>.Instance);

            var response = await recovered.GetCharacters();

            inner.CallCount.ShouldBe(1);
            response.ShouldNotBeNull();
        }

        [Fact]
        public async Task ZeroDurationDisablesCaching()
        {
            var (cache, _) = NewCache();
            var inner = new FakeCharactersProvider();
            inner.FakeResponse(SomeResponse());
            var sut = new CachingCharactersProvider(inner, cache, TimeSpan.Zero, NullLogger<CachingCharactersProvider>.Instance);

            await sut.GetCharacters();
            await sut.GetCharacters();

            inner.CallCount.ShouldBe(2);
        }
    }
}
