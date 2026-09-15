using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Superheroes.Application.Ports;

namespace Superheroes.Tests;

/// <summary>
/// Wraps the WebApplicationFactory + ICharacterLoader swap that BattleTests.cs used to
/// inline per-test, so every characterization test shares the same host setup. The caller
/// owns the ICharacterLoader substitute and configures its GetCharacters() response, which
/// lets each test set up its own data with the substitute visible at the call site.
/// </summary>
public sealed class BattleTestHost : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    public HttpClient Client { get; }

    public BattleTestHost(ICharacterLoader characterLoader)
    {
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

        // Registered as the singleton instance above, so it's the same object the caller
        // holds - a GetCharacters().Returns(...) configured after this constructor runs
        // still takes effect, since nothing calls it until a request comes in.
        Client = _factory.CreateClient();
    }

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
