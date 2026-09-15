using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Superheroes.Tests
{
    /// <summary>
    /// Wraps the WebApplicationFactory + ICharactersProvider swap that BattleTests.cs used to
    /// inline per-test, so every characterization test shares the same host setup.
    /// </summary>
    public sealed class BattleTestHost : IDisposable
    {
        private readonly WebApplicationFactory<Program> _factory;

        public HttpClient Client { get; }

        private BattleTestHost(CharactersResponse response)
        {
            var charactersProvider = new FakeCharactersProvider();
            charactersProvider.FakeResponse(response);

            _factory = new WebApplicationFactory<Program>()
                .WithWebHostBuilder(builder =>
                {
                    builder.ConfigureServices(services =>
                    {
                        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(ICharactersProvider));
                        if (descriptor != null)
                        {
                            services.Remove(descriptor);
                        }
                        services.AddSingleton<ICharactersProvider>(charactersProvider);
                    });
                });

            Client = _factory.CreateClient();
        }

        public static BattleTestHost WithCharacters(params CharacterResponse[] characters) =>
            new(new CharactersResponse { Items = characters });

        public static BattleTestHost WithNullFeed() =>
            new(null);

        public static CharacterResponse Character(string name, double score, string type, string weakness = null) => type switch
        {
            "hero" => new HeroResponse { Name = name, Score = score, Weakness = weakness },
            "villain" when weakness is null => new VillainResponse { Name = name, Score = score },
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
