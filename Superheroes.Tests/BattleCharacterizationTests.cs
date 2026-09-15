using Xunit;
using Shouldly;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Superheroes.Application.Characters;
using static Superheroes.Tests.BattleTestHost;

namespace Superheroes.Tests;

/// <summary>
/// Mirrors Newtonsoft.Json's own JToken.Value&lt;T&gt; extension method so the switch away
/// from JObject/JToken below didn't need to touch every assertion call site.
/// </summary>
internal static class JsonObjectExtensions
{
    public static T Value<T>(this JsonObject obj, string propertyName) => obj[propertyName]!.GetValue<T>();
}

/// <summary>
/// Pins the HTTP/controller-level behaviour of the /battle endpoint that sits above
/// BattleService: routing, query-string binding, the JSON response contract, and the
/// translation of a BattleResult into a 400/200 response (or an unhandled exception
/// into a 500). The winner-selection/weakness/validation business logic itself is
/// exercised directly and far more thoroughly in BattleServiceTests.cs, so it is
/// intentionally not duplicated here beyond one end-to-end happy path.
/// </summary>
public class BattleCharacterizationTests
{
    private static async Task<JsonObject> BodyAsJson(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return (JsonObject)JsonNode.Parse(json)!;
    }

    [Fact]
    public async Task HappyPathReturnsTheWinner()
    {
        // End-to-end sanity check that DI resolves IBattleService and the controller
        // wires its result through correctly - the winner-selection rules themselves
        // are BattleServiceTests' job.
        using var host = WithCharacters(Character("Batman", 8.3, CharacterType.Hero), Character("Joker", 8.2, CharacterType.Villain));

        var response = await host.Battle("?hero=Batman&villain=Joker");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await BodyAsJson(response);
        body.Value<string>("name").ShouldBe("Batman");
    }

    // ----- Response contract -----

    [Fact]
    public async Task ReturnsJsonContentType()
    {
        using var host = WithCharacters(Character("Batman", 8.3, CharacterType.Hero), Character("Joker", 8.2, CharacterType.Villain));

        var response = await host.Battle("?hero=Batman&villain=Joker");

        response.Content.Headers.ContentType!.ToString().ShouldBe("application/json; charset=utf-8");
    }

    [Fact]
    public async Task ResponseUsesCamelCaseKeys()
    {
        // Serialized with the default System.Text.Json formatter (AddNewtonsoftJson is
        // never called), so property names are camelCase, not PascalCase.
        using var host = WithCharacters(Character("Batman", 8.3, CharacterType.Hero), Character("Joker", 8.2, CharacterType.Villain));

        var response = await host.Battle("?hero=Batman&villain=Joker");
        var body = await BodyAsJson(response);

        body.ContainsKey("name").ShouldBeTrue();
        body.ContainsKey("Name").ShouldBeFalse();
    }

    [Fact]
    public async Task ResponseForHeroContainsNameScoreTypeAndWeakness()
    {
        using var host = WithCharacters(Character("Batman", 8.3, CharacterType.Hero), Character("Joker", 8.2, CharacterType.Villain));

        var response = await host.Battle("?hero=Batman&villain=Joker");
        var body = await BodyAsJson(response);

        body.Select(p => p.Key).ShouldBe(new[] { "name", "score", "type", "weakness" }, ignoreOrder: true);
    }

    [Fact]
    public async Task ResponseForVillainContainsNameAndScoreAndTypeButNoWeakness()
    {
        // VillainResponse has no Weakness property, so a villain winner's JSON omits the
        // key entirely rather than reporting it as null the way a hero's does.
        using var host = WithCharacters(Character("Gamora", 8.4, CharacterType.Hero), Character("Thanos", 9.9, CharacterType.Villain));

        var response = await host.Battle("?hero=Gamora&villain=Thanos");
        var body = await BodyAsJson(response);

        body.Select(p => p.Key).ShouldBe(new[] { "name", "score", "type" }, ignoreOrder: true);
    }

    [Fact]
    public async Task ScoreIsSerialisedAsJsonNumber()
    {
        using var host = WithCharacters(Character("Batman", 8.3, CharacterType.Hero), Character("Joker", 8.2, CharacterType.Villain));

        var response = await host.Battle("?hero=Batman&villain=Joker");
        var body = await BodyAsJson(response);

        // System.Text.Json, unlike Newtonsoft's JTokenType (which distinguishes Integer from
        // Float), reports a single Number kind for both - so this only pins that "score" is
        // encoded as a raw JSON number rather than a quoted string.
        body["score"]!.GetValueKind().ShouldBe(JsonValueKind.Number);
    }

    // ----- Routing / binding -----

    [Fact]
    public async Task EndpointRespondsToGet()
    {
        using var host = WithCharacters(Character("Batman", 8.3, CharacterType.Hero), Character("Joker", 8.2, CharacterType.Villain));

        var response = await host.Send(HttpMethod.Get, "?hero=Batman&villain=Joker");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task EndpointRejectsOtherHttpVerbs(string verb)
    {
        // The action is decorated with [HttpGet] so it can be described in the OpenAPI
        // document (which has no way to express "matches any verb"), so attribute
        // routing now matches the path but rejects every other HTTP method.
        using var host = WithCharacters(Character("Batman", 8.3, CharacterType.Hero), Character("Joker", 8.2, CharacterType.Villain));

        var response = await host.Send(new HttpMethod(verb), "?hero=Batman&villain=Joker");

        response.StatusCode.ShouldBe(HttpStatusCode.MethodNotAllowed);
    }

    [Fact]
    public async Task QueryParameterNamesAreCaseInsensitive()
    {
        // ASP.NET Core query-string model binding matches parameter names
        // case-insensitively regardless of the action signature's casing. This is MVC
        // model binding, distinct from BattleService's own case-insensitive character
        // name matching (see BattleServiceTests.CharacterNameMatchingIsCaseInsensitive).
        using var host = WithCharacters(Character("Batman", 8.3, CharacterType.Hero), Character("Joker", 8.2, CharacterType.Villain));

        var response = await host.Battle("?Hero=Batman&Villain=Joker");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await BodyAsJson(response);
        body.Value<string>("name").ShouldBe("Batman");
    }

    // ----- Result translation -----

    [Fact]
    public async Task ValidationFailureReturnsBadRequestWithFieldErrors()
    {
        // One representative case proving the controller turns a BattleResult's Errors
        // into a 400 with a ModelState-shaped JSON body. The full validation matrix
        // (hero-vs-hero, unknown names, etc.) lives in BattleServiceTests.cs.
        using var host = WithCharacters(Character("Batman", 8.3, CharacterType.Hero), Character("Superman", 9.6, CharacterType.Hero));

        var response = await host.Battle("?hero=Batman&villain=Superman");
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        var body = await BodyAsJson(response);
        body["villain"]![0]!.GetValue<string>().ShouldBe("Villain is required");
    }

    [Fact]
    public async Task UnhandledServiceExceptionReturns500()
    {
        // A null feed crashes BattleService with a NullReferenceException (pinned
        // directly in BattleServiceTests.NullFeedThrows); this confirms the app's
        // hosting pipeline still turns that into a 500 rather than leaking a raw
        // exception to the client.
        using var host = WithNullFeed();

        var response = await host.Battle("?hero=Batman&villain=Joker");

        response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
    }
}
