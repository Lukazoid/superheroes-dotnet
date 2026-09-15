using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Superheroes.Tests
{
    public class CachingCharactersProviderTests
    {
        private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

        private static IOptions<CharactersCacheOptions> OptionsFor(TimeSpan duration) =>
            Options.Create(new CharactersCacheOptions { CacheDuration = duration });

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

        // A fresh HybridCache per test - it only coordinates concurrent callers and tracks
        // expiry within a single instance, so each test needs its own to stay isolated.
        //
        // There's no supported way to fake HybridCache's clock (unlike the previous
        // IMemoryCache + ISystemClock setup - see https://github.com/dotnet/extensions/issues/5763),
        // so expiry below is tested with a short real duration and a real delay instead.
        private static HybridCache NewCache()
        {
            var services = new ServiceCollection();
            services.AddHybridCache();
            var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<HybridCache>();
        }

        [Fact]
        public async Task ReturnsCachedResponseWithinDuration()
        {
            var cache = NewCache();
            var inner = new FakeCharactersProvider();
            inner.FakeResponse(SomeResponse());
            var sut = new CachingCharactersProvider(inner, cache, OptionsFor(CacheDuration), NullLogger<CachingCharactersProvider>.Instance);

            var first = await sut.GetCharacters();
            var second = await sut.GetCharacters();

            // HybridCache deserialises a fresh instance on every read (even from its local,
            // in-process tier) unless the cached type is sealed and [ImmutableObject(true)], so
            // reference equality isn't guaranteed - the call count and the values are what prove
            // the cache did its job.
            inner.CallCount.ShouldBe(1);
            second.Items.Single().Name.ShouldBe(first.Items.Single().Name);
        }

        [Fact]
        public async Task RefetchesAfterDurationExpires()
        {
            var cache = NewCache();
            var inner = new FakeCharactersProvider();
            inner.FakeResponse(SomeResponse());
            var shortDuration = TimeSpan.FromMilliseconds(50);
            var sut = new CachingCharactersProvider(inner, cache, OptionsFor(shortDuration), NullLogger<CachingCharactersProvider>.Instance);

            await sut.GetCharacters();
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            await sut.GetCharacters();

            inner.CallCount.ShouldBe(2);
        }

        [Fact]
        public async Task ConcurrentCallsOnColdCacheOnlyFetchOnce()
        {
            var cache = NewCache();
            var inner = new FakeCharactersProvider();
            inner.FakeResponse(SomeResponse());
            var sut = new CachingCharactersProvider(inner, cache, OptionsFor(CacheDuration), NullLogger<CachingCharactersProvider>.Instance);

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
            var cache = NewCache();
            var throwing = new ThrowingCharactersProvider();
            var sut = new CachingCharactersProvider(throwing, cache, OptionsFor(CacheDuration), NullLogger<CachingCharactersProvider>.Instance);

            await Should.ThrowAsync<InvalidOperationException>(() => sut.GetCharacters());

            var inner = new FakeCharactersProvider();
            inner.FakeResponse(SomeResponse());
            var recovered = new CachingCharactersProvider(inner, cache, OptionsFor(CacheDuration), NullLogger<CachingCharactersProvider>.Instance);

            var response = await recovered.GetCharacters();

            inner.CallCount.ShouldBe(1);
            response.ShouldNotBeNull();
        }

        [Fact]
        public async Task ZeroDurationDisablesCaching()
        {
            var cache = NewCache();
            var inner = new FakeCharactersProvider();
            inner.FakeResponse(SomeResponse());
            var sut = new CachingCharactersProvider(inner, cache, OptionsFor(TimeSpan.Zero), NullLogger<CachingCharactersProvider>.Instance);

            await sut.GetCharacters();
            await sut.GetCharacters();

            inner.CallCount.ShouldBe(2);
        }
    }
}
