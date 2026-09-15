using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Superheroes.Tests
{
    /// <summary>
    /// Wraps the WebApplicationFactory + ICharactersProvider swap that BattleTests.cs used to
    /// inline per-test, so every characterization test shares the same host setup.
    ///
    /// Note: BattleController's comparison fields are static, so they persist across every
    /// BattleTestHost instance created in the same test process, even after Dispose(). Tests
    /// that rely on a specific starting state must prime it themselves.
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
                        // Removes both the keyed source registration and the CachingCharactersProvider
                        // decorator registered around it, replacing the whole chain with the fake.
                        services.RemoveAll(typeof(ICharactersProvider));
                        services.AddSingleton<ICharactersProvider>(charactersProvider);
                    });
                });

            Client = _factory.CreateClient();
        }

        public static BattleTestHost WithCharacters(params CharacterResponse[] characters) =>
            new(new CharactersResponse { Items = characters });

        public static BattleTestHost WithNullFeed() =>
            new(null);

        public static BattleTestHost WithNullItems() =>
            new(new CharactersResponse { Items = null });

        public static CharacterResponse Character(string name, double score, string type) =>
            new() { Name = name, Score = score, Type = type };

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
