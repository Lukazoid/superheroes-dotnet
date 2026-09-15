using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using Superheroes.Application.Characters;
using Superheroes.Application.Ports;

namespace Superheroes.Tests
{
    /// <summary>
    /// Wraps the WebApplicationFactory + ICharacterLoader swap that BattleTests.cs used to
    /// inline per-test, so every characterization test shares the same host setup.
    /// </summary>
    public sealed class BattleTestHost : IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;

        public HttpClient Client { get; }

        private BattleTestHost(CharacterCatalogue characters)
        {
            var characterLoader = Substitute.For<ICharacterLoader>();
            characterLoader.GetCharacters().Returns(characters);

            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        // Removes the whole ICharacterLoader registration chain (the S3 source
                        // adapter and the CachingCharacterLoader decorated around it),
                        // replacing it with the substitute.
                        services.RemoveAll(typeof(ICharacterLoader));
                        services.AddSingleton(characterLoader);
                    });
                });

            Client = _factory.CreateClient();
        }

        public static BattleTestHost WithCharacters(params Character[] characters) =>
            new(CharacterCatalogue.Create(characters));

        public static BattleTestHost WithNullFeed() =>
            new(null);

        public static Character Character(string name, double score, string type, string weakness = null) => type switch
        {
            "hero" => new Hero(name, score, weakness),
            "villain" when weakness is null => new Villain(name, score),
            "villain" => throw new ArgumentException("Villains cannot have a weakness.", nameof(weakness)),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown character type.")
        };

        public Task<HttpResponseMessage> Battle(string queryString = "") =>
            Client.GetAsync("battle" + queryString);

        public Task<HttpResponseMessage> Send(HttpMethod method, string queryString = "") =>
            Client.SendAsync(new HttpRequestMessage(method, "battle" + queryString));

        public void Dispose()
        {
            Client.Dispose();
            _factory.Dispose();
        }
    }
}
