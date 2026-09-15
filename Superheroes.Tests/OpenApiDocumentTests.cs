using Xunit;
using Shouldly;
using System.Net;
using System.Text.Json.Nodes;
using static Superheroes.Tests.BattleTestHost;

namespace Superheroes.Tests;

/// <summary>
/// Pins that the generated OpenAPI document actually describes the /battle endpoint - this
/// is easy to silently lose, since an MVC action needs an explicit HTTP method attribute
/// (e.g. [HttpGet]) and its controller needs [ApiController] before ASP.NET Core's API
/// Explorer will pick it up for OpenAPI generation at all.
/// </summary>
public class OpenApiDocumentTests
{
    [Fact]
    public async Task DocumentDescribesBothBattleResponseCodes()
    {
        using var host = WithCharacters(Character("Batman", 8.3, "hero"), Character("Joker", 8.2, "villain"));

        var response = await host.Client.GetAsync("openapi/v1.json");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var responses = document["paths"]!["/battle"]!["get"]!["responses"]!;

        responses["200"].ShouldNotBeNull();
        responses["400"].ShouldNotBeNull();
    }

    [Fact]
    public async Task DocumentDescribesHeroAndVillainAsDiscriminatedVariantsOfCharacterResponse()
    {
        using var host = WithCharacters(Character("Batman", 8.3, "hero"), Character("Joker", 8.2, "villain"));

        var response = await host.Client.GetAsync("openapi/v1.json");
        var document = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        var discriminator = document["components"]!["schemas"]!["CharacterResponse"]!["discriminator"]!;
        discriminator["propertyName"]!.GetValue<string>().ShouldBe("type");
        discriminator["mapping"]!["hero"].ShouldNotBeNull();
        discriminator["mapping"]!["villain"].ShouldNotBeNull();
    }
}
