using Xunit;
using Shouldly;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using static Superheroes.Tests.BattleTestHost;

namespace Superheroes.Tests
{
    /// <summary>
    /// Mirrors Newtonsoft.Json's own JToken.Value&lt;T&gt; extension method so the switch away
    /// from JObject/JToken below didn't need to touch every assertion call site.
    /// </summary>
    internal static class JsonObjectExtensions
    {
        public static T Value<T>(this JsonObject obj, string propertyName) => obj[propertyName].GetValue<T>();
    }

    /// <summary>
    /// Pins the behaviour of BattleController's /battle endpoint - winner selection,
    /// weakness scoring, hero/villain type validation, and the response/routing
    /// contract - so future changes show up as intentional, reviewed diffs to this
    /// file rather than silent behaviour changes. See README.md for the feature this
    /// endpoint implements.
    /// </summary>
    public class BattleCharacterizationTests
    {
        private static async Task<JsonObject> BodyAsJson(HttpResponseMessage response)
        {
            var json = await response.Content.ReadAsStringAsync();
            return (JsonObject)JsonNode.Parse(json);
        }

        // ----- Winner selection -----

        [Fact]
        public async Task HigherScoringHeroWins()
        {
            // Absorbs BattleTests.CanGetHeros. Batman (8.3) beats Joker (8.2) on raw score.
            // This Batman has no configured weakness, so no penalty applies - see
            // WeaknessKnocksAPointOffTheHeroScore for the case where one does.
            using var host = WithCharacters(Character("Batman", 8.3, "hero"), Character("Joker", 8.2, "villain"));

            var response = await host.Battle("?hero=Batman&villain=Joker");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await BodyAsJson(response);
            body.Value<string>("name").ShouldBe("Batman");
        }

        [Fact]
        public async Task HigherScoringVillainWins()
        {
            using var host = WithCharacters(Character("Gamora", 8.4, "hero"), Character("Thanos", 9.9, "villain"));

            var response = await host.Battle("?hero=Gamora&villain=Thanos");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await BodyAsJson(response);
            body.Value<string>("name").ShouldBe("Thanos");
        }

        [Fact]
        public async Task SupermanBeatsLexLuthor()
        {
            // README acceptance test #2, using a Superman with no configured weakness - so
            // this passes on raw score alone (9.6 > 8), same as it would even without the
            // weakness rule. See WeaknessKnocksAPointOffTheHeroScore for the weakness path.
            using var host = WithCharacters(Character("Superman", 9.6, "hero"), Character("Lex Luthor", 8, "villain"));

            var response = await host.Battle("?hero=Superman&villain=Lex%20Luthor");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await BodyAsJson(response);
            body.Value<string>("name").ShouldBe("Superman");
        }

        [Fact]
        public async Task EqualScoresReturnTheVillain()
        {
            // The comparison is a strict ">", so a tie falls through to the villain.
            using var host = WithCharacters(Character("Batman", 8.0, "hero"), Character("Joker", 8.0, "villain"));

            var response = await host.Battle("?hero=Batman&villain=Joker");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await BodyAsJson(response);
            body.Value<string>("name").ShouldBe("Joker");
        }

        // ----- Weakness scoring -----

        [Fact]
        public async Task WeaknessKnocksAPointOffTheHeroScore()
        {
            // Batman's weakness is Joker: 8.3 - 1 = 7.3, which now loses to Joker's 8.2.
            // Confirms README acceptance test #1 via the weakness rule itself, rather than
            // by coincidence of raw score as in HigherScoringHeroWins.
            using var host = WithCharacters(
                Character("Batman", 8.3, "hero", weakness: "Joker"),
                Character("Joker", 8.2, "villain"));

            var response = await host.Battle("?hero=Batman&villain=Joker");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await BodyAsJson(response);
            body.Value<string>("name").ShouldBe("Joker");
        }

        [Fact]
        public async Task WeaknessOnlyAppliesAgainstTheNamedVillain()
        {
            // Batman's weakness is Joker, but he isn't fighting Joker here, so no penalty
            // applies and his raw score (8.3) still beats Thanos's 8.2.
            using var host = WithCharacters(
                Character("Batman", 8.3, "hero", weakness: "Joker"),
                Character("Thanos", 8.2, "villain"));

            var response = await host.Battle("?hero=Batman&villain=Thanos");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await BodyAsJson(response);
            body.Value<string>("name").ShouldBe("Batman");
        }

        [Fact]
        public async Task ReportedScoreIsNotAdjustedForWeakness()
        {
            // The -1 penalty affects only the winner comparison; the response still reports
            // the hero's raw score, not the weakness-adjusted one.
            using var host = WithCharacters(
                Character("Superman", 9.6, "hero", weakness: "Lex Luthor"),
                Character("Lex Luthor", 8, "villain"));

            var response = await host.Battle("?hero=Superman&villain=Lex%20Luthor");
            var body = await BodyAsJson(response);

            body.Value<string>("name").ShouldBe("Superman");
            body.Value<double>("score").ShouldBe(9.6);
        }

        // ----- Hero/villain type validation -----

        [Fact]
        public async Task HeroVersusHeroIsNotAccepted()
        {
            using var host = WithCharacters(Character("Batman", 8.3, "hero"), Character("Superman", 9.6, "hero"));

            var response = await host.Battle("?hero=Batman&villain=Superman");
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task VillainVersusVillainIsNotAccepted()
        {
            using var host = WithCharacters(Character("Joker", 8.2, "villain"), Character("Thanos", 9.9, "villain"));

            var response = await host.Battle("?hero=Joker&villain=Thanos");
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task SameNameAsHeroAndVillainIsNotAccepted()
        {
            // A single character has one Type, so using the same name for both hero and
            // villain can never satisfy both checks at once.
            using var host = WithCharacters(Character("Batman", 8.3, "hero"));

            var response = await host.Battle("?hero=Batman&villain=Batman");
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        // ----- Response contract -----

        [Fact]
        public async Task ReturnsJsonContentType()
        {
            using var host = WithCharacters(Character("Batman", 8.3, "hero"), Character("Joker", 8.2, "villain"));

            var response = await host.Battle("?hero=Batman&villain=Joker");

            response.Content.Headers.ContentType.ToString().ShouldBe("application/json; charset=utf-8");
        }

        [Fact]
        public async Task ResponseUsesCamelCaseKeys()
        {
            // Serialized with the default System.Text.Json formatter (AddNewtonsoftJson is
            // never called), so property names are camelCase, not PascalCase.
            using var host = WithCharacters(Character("Batman", 8.3, "hero"), Character("Joker", 8.2, "villain"));

            var response = await host.Battle("?hero=Batman&villain=Joker");
            var body = await BodyAsJson(response);

            body.ContainsKey("name").ShouldBeTrue();
            body.ContainsKey("Name").ShouldBeFalse();
        }

        [Fact]
        public async Task ResponseForHeroContainsNameScoreTypeAndWeakness()
        {
            using var host = WithCharacters(Character("Batman", 8.3, "hero"), Character("Joker", 8.2, "villain"));

            var response = await host.Battle("?hero=Batman&villain=Joker");
            var body = await BodyAsJson(response);

            body.Select(p => p.Key).ShouldBe(new[] { "name", "score", "type", "weakness" }, ignoreOrder: true);
        }

        [Fact]
        public async Task ResponseForVillainContainsNameAndScoreAndTypeButNoWeakness()
        {
            // VillainResponse has no Weakness property, so a villain winner's JSON omits the
            // key entirely rather than reporting it as null the way a hero's does.
            using var host = WithCharacters(Character("Gamora", 8.4, "hero"), Character("Thanos", 9.9, "villain"));

            var response = await host.Battle("?hero=Gamora&villain=Thanos");
            var body = await BodyAsJson(response);

            body.Select(p => p.Key).ShouldBe(new[] { "name", "score", "type" }, ignoreOrder: true);
        }

        [Fact]
        public async Task ScoreIsSerialisedAsJsonNumber()
        {
            using var host = WithCharacters(Character("Batman", 8.3, "hero"), Character("Joker", 8.2, "villain"));

            var response = await host.Battle("?hero=Batman&villain=Joker");
            var body = await BodyAsJson(response);

            // System.Text.Json, unlike Newtonsoft's JTokenType (which distinguishes Integer from
            // Float), reports a single Number kind for both - so this only pins that "score" is
            // encoded as a raw JSON number rather than a quoted string.
            body["score"].GetValueKind().ShouldBe(JsonValueKind.Number);
        }

        // ----- Routing / binding -----

        [Theory]
        [InlineData("GET")]
        [InlineData("POST")]
        [InlineData("PUT")]
        [InlineData("DELETE")]
        public async Task EndpointRespondsToAnyHttpVerb(string verb)
        {
            // The action has no [HttpGet] (or any other verb) attribute, so attribute
            // routing matches it against every HTTP method.
            using var host = WithCharacters(Character("Batman", 8.3, "hero"), Character("Joker", 8.2, "villain"));

            var response = await host.Send(new HttpMethod(verb), "?hero=Batman&villain=Joker");

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        [Fact]
        public async Task QueryParameterNamesAreCaseInsensitive()
        {
            // ASP.NET Core query-string model binding matches parameter names
            // case-insensitively regardless of the action signature's casing.
            using var host = WithCharacters(Character("Batman", 8.3, "hero"), Character("Joker", 8.2, "villain"));

            var response = await host.Battle("?Hero=Batman&Villain=Joker");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await BodyAsJson(response);
            body.Value<string>("name").ShouldBe("Batman");
        }

        [Fact]
        public async Task CharacterNameMatchingIsCaseInsensitive()
        {
            // Names are matched with StringComparison.InvariantCultureIgnoreCase, so a
            // differently-cased "batman" still matches the "Batman" entry.
            using var host = WithCharacters(Character("Batman", 8.3, "hero"), Character("Joker", 8.2, "villain"));

            var response = await host.Battle("?hero=batman&villain=Joker");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await BodyAsJson(response);
            body.Value<string>("name").ShouldBe("Batman");
        }

        // ----- Feed quirks -----

        [Fact]
        public async Task DuplicateNamesInFeedFirstOccurrenceWins()
        {
            // Both entries named "Joker" match the villain parameter, but the loop breaks
            // as soon as both hero and villain are found, so the first match in the feed
            // wins and the second "Joker" entry is never reached.
            using var host = WithCharacters(
                Character("Batman", 8.3, "hero"),
                Character("Joker", 8.6, "villain"),
                Character("Joker", 9.9, "villain"));

            var response = await host.Battle("?hero=Batman&villain=Joker");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await BodyAsJson(response);
            body.Value<string>("name").ShouldBe("Joker");
            body.Value<double>("score").ShouldBe(8.6);
        }

        // ----- Error paths -----

        [Fact]
        public async Task NullFeedFails()
        {
            // ICharactersProvider.GetCharacters() returning null (e.g. a failed/undeserializable
            // S3 response) crashes with a NullReferenceException on "characters.Items".
            using var host = WithNullFeed();

            var response = await host.Battle("?hero=Batman&villain=Joker");

            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task NullItemsFails()
        {
            // A non-null CharactersResponse with a null Items array crashes the same way,
            // since "foreach" over a null array throws.
            using var host = WithNullItems();

            var response = await host.Battle("?hero=Batman&villain=Joker");

            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        }

        [Fact]
        public async Task UnknownHeroReturnsBadRequest()
        {
            using var host = WithCharacters(Character("Superman", 9.6, "hero"), Character("Joker", 8.2, "villain"));

            var response = await host.Battle("?hero=NobodyKnown&villain=Joker");
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task UnknownVillainReturnsBadRequest()
        {
            using var host = WithCharacters(Character("Superman", 9.6, "hero"), Character("Joker", 8.2, "villain"));

            var response = await host.Battle("?hero=Superman&villain=NobodyKnown");
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task MissingQueryParametersReturnsBadRequest()
        {
            // With no "hero"/"villain" query parameters at all (both bind to null), no feed
            // entry ever matches, so both fail validation.
            using var host = WithCharacters(Character("Batman", 8.3, "hero"), Character("Joker", 8.2, "villain"));

            var response = await host.Battle();
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }
    }
}
