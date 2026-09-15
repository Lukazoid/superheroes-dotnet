using Xunit;
using Shouldly;
using System.Net;
using System.Text.Json.Nodes;
using NSubstitute;
using Superheroes.Application.Characters;
using Superheroes.Application.Ports;

namespace Superheroes.Tests;

/// <summary>
/// Pins that the generated OpenAPI document actually describes the /battle endpoint - this
/// is easy to silently lose, since an MVC action needs an explicit HTTP method attribute
/// (e.g. [HttpGet]) and its controller needs [ApiController] before ASP.NET Core's API
/// Explorer will pick it up for OpenAPI generation at all.
/// </summary>
public class OpenApiDocumentTests : IDisposable
{
    private readonly ICharacterLoader _characterLoader = Substitute.For<ICharacterLoader>();
    private readonly BattleTestHost _host;

    public OpenApiDocumentTests() => _host = new BattleTestHost(_characterLoader);

    public void Dispose() => _host.Dispose();

    [Fact]
    public async Task DocumentDescribesBothBattleResponseCodes()
    {
        var batman = new Hero("Batman", 8.3, null);
        var joker = new Villain("Joker", 8.2);
        _characterLoader.GetCharacters().Returns(CharacterCatalogue.Create([batman, joker]));

        var response = await _host.Client.GetAsync("openapi/v1.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var responses = document["paths"]!["/battle"]!["get"]!["responses"]!;

        responses["200"].ShouldNotBeNull();
        responses["400"].ShouldNotBeNull();
    }

    [Fact]
    public async Task DocumentDescribesHeroAndVillainAsDiscriminatedVariantsOfCharacterResponse()
    {
        var batman = new Hero("Batman", 8.3, null);
        var joker = new Villain("Joker", 8.2);
        _characterLoader.GetCharacters().Returns(CharacterCatalogue.Create([batman, joker]));

        var response = await _host.Client.GetAsync("openapi/v1.json");
        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        var discriminator = document["components"]!["schemas"]!["CharacterResponse"]!["discriminator"]!;
        discriminator["propertyName"]!.GetValue<string>().ShouldBe("type");
        discriminator["mapping"]!["hero"].ShouldNotBeNull();
        discriminator["mapping"]!["villain"].ShouldNotBeNull();
    }
}
