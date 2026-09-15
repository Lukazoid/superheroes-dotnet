using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Superheroes.Application.Caching;
using Superheroes.Application.Characters;
using Superheroes.Application.Ports;
using Xunit;

namespace Superheroes.Application.Tests;

public class CachingCharacterLoaderTests
{
    private readonly ICharacterLoader _inner = Substitute.For<ICharacterLoader>();
    private readonly HybridCache _cache = NewCache();
    private TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

    private CachingCharacterLoader CreateSut() => new(
        _inner,
        _cache,
        Options.Create(new CharactersCacheOptions { CacheDuration = _cacheDuration }),
        NullLogger<CachingCharacterLoader>.Instance);

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
        var batman = new Hero("Batman", 8.3, null);
        _inner.GetCharacters().Returns(CharacterCatalogue.Create([batman]));
        var sut = CreateSut();

        var first = await sut.GetCharacters();
        var second = await sut.GetCharacters();

        _ = _inner.Received(1).GetCharacters();
        // HybridCache deserialises a fresh instance on every read (even from its local,
        // in-process tier) unless the cached type is sealed and [ImmutableObject(true)], so
        // reference equality isn't guaranteed - the call count and the values are what prove
        // the cache did its job.
        second.Heroes["Batman"].Name.ShouldBe(first.Heroes["Batman"].Name);
    }

    [Fact]
    public async Task LookupStaysCaseInsensitiveAfterACacheRoundTrip()
    {
        // Regression test: HybridCache round-trips cached values through serialization, which
        // does not preserve ImmutableDictionary's key comparer - a cache hit would otherwise
        // silently come back case-sensitive even though CharacterCatalogue.Create built it
        // with OrdinalIgnoreCase.
        var batman = new Hero("Batman", 8.3, null);
        _inner.GetCharacters().Returns(CharacterCatalogue.Create([batman]));
        var sut = CreateSut();

        await sut.GetCharacters(); // populates the cache
        var second = await sut.GetCharacters(); // served from the cache

        second.Heroes.ContainsKey("batman").ShouldBeTrue();
        second.Heroes["BATMAN"].Name.ShouldBe("Batman");
    }

    [Fact]
    public async Task RefetchesAfterDurationExpires()
    {
        var batman = new Hero("Batman", 8.3, null);
        _inner.GetCharacters().Returns(CharacterCatalogue.Create([batman]));
        _cacheDuration = TimeSpan.FromMilliseconds(50);
        var sut = CreateSut();

        await sut.GetCharacters();
        await Task.Delay(TimeSpan.FromMilliseconds(250));
        await sut.GetCharacters();

        _ = _inner.Received(2).GetCharacters();
    }

    [Fact]
    public async Task ConcurrentCallsOnColdCacheOnlyFetchOnce()
    {
        var batman = new Hero("Batman", 8.3, null);
        _inner.GetCharacters().Returns(CharacterCatalogue.Create([batman]));
        var sut = CreateSut();

        await Task.WhenAll(
            sut.GetCharacters(),
            sut.GetCharacters(),
            sut.GetCharacters(),
            sut.GetCharacters(),
            sut.GetCharacters());

        _ = _inner.Received(1).GetCharacters();
    }

    [Fact]
    public async Task PropagatesFailureAndDoesNotPoisonTheCache()
    {
        _inner.GetCharacters().Returns(Task.FromException<CharacterCatalogue>(new InvalidOperationException("S3 is unavailable")));
        var sut = CreateSut();

        await Should.ThrowAsync<InvalidOperationException>(() => sut.GetCharacters());

        var batman = new Hero("Batman", 8.3, null);
        _inner.GetCharacters().Returns(CharacterCatalogue.Create([batman]));
        _inner.ClearReceivedCalls();
        var recovered = CreateSut();

        var response = await recovered.GetCharacters();

        _ = _inner.Received(1).GetCharacters();
        response.ShouldNotBeNull();
    }

    [Fact]
    public async Task ZeroDurationDisablesCaching()
    {
        var batman = new Hero("Batman", 8.3, null);
        _inner.GetCharacters().Returns(CharacterCatalogue.Create([batman]));
        _cacheDuration = TimeSpan.Zero;
        var sut = CreateSut();

        await sut.GetCharacters();
        await sut.GetCharacters();

        _ = _inner.Received(2).GetCharacters();
    }
}
