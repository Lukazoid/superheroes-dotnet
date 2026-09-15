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
    /// Pins the current, unrefined behaviour of BattleController's /battle endpoint,
    /// including its bugs, so upcoming changes (weaknesses, hero/villain validation,
    /// removing the static comparison fields) show up as intentional, reviewed diffs
    /// to this file rather than silent behaviour changes.
    ///
    /// See README.md for the intended behaviour this endpoint does NOT yet implement:
    /// weaknesses knocking a point off a hero's score, and hero-vs-villain validation.
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
            // Absorbs BattleTests.CanGetHeros. Batman (8.3) beats Joker (8.2) purely on raw
            // score - Batman's "weakness" to Joker (see characters.json) is not applied.
            // This contradicts README acceptance test #1, which expects Joker to win once
            // weaknesses are implemented.
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
            // README acceptance test #2. Currently correct, but only by coincidence of raw
            // score (9.6 > 8) - Superman's weakness to Lex Luthor is not actually applied.
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

        // ----- Absent hero/villain validation -----

        [Fact]
        public async Task HeroVersusHeroIsAccepted()
        {
            // Type is never inspected, so two heroes can "battle" each other.
            using var host = WithCharacters(Character("Batman", 8.3, "hero"), Character("Superman", 9.6, "hero"));

            var response = await host.Battle("?hero=Batman&villain=Superman");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await BodyAsJson(response);
            body.Value<string>("name").ShouldBe("Superman");
        }

        [Fact]
        public async Task VillainVersusVillainIsAccepted()
        {
            using var host = WithCharacters(Character("Joker", 8.2, "villain"), Character("Thanos", 9.9, "villain"));

            var response = await host.Battle("?hero=Joker&villain=Thanos");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await BodyAsJson(response);
            body.Value<string>("name").ShouldBe("Thanos");
        }

        [Fact]
        public async Task SwappedHeroAndVillainParametersAreAccepted()
        {
            // Passing the villain's name as "hero" and the hero's name as "villain" is
            // accepted without error - the winner still comes purely from comparing scores.
            using var host = WithCharacters(Character("Batman", 8.3, "hero"), Character("Joker", 8.2, "villain"));

            var response = await host.Battle("?hero=Joker&villain=Batman");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await BodyAsJson(response);
            body.Value<string>("name").ShouldBe("Batman");
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
        public async Task ResponseContainsOnlyNameScoreAndType()
        {
            // CharacterResponse has no Weakness property, so even though the source feed
            // carries a "weakness" field, it never reaches the response.
            using var host = WithCharacters(Character("Batman", 8.3, "hero"), Character("Joker", 8.2, "villain"));

            var response = await host.Battle("?hero=Batman&villain=Joker");
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
        public async Task CharacterNameMatchingIsCaseSensitive()
        {
            // Combines the case-sensitive "==" name match with the static-field leak: after
            // priming with Aquaman as the hero, a differently-cased "batman" does NOT match
            // the "Batman" entry, so the stale Aquaman value (score 3.5) is compared instead
            // and loses to Joker. A case-insensitive match would have picked up Batman
            // (8.3) and produced the opposite winner.
            using var host = WithCharacters(
                Character("Batman", 8.3, "hero"),
                Character("Joker", 8.2, "villain"),
                Character("Aquaman", 3.5, "hero"));

            await host.Battle("?hero=Aquaman&villain=Joker"); // primes _character1 = Aquaman (3.5)

            var response = await host.Battle("?hero=batman&villain=Joker");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await BodyAsJson(response);
            body.Value<string>("name").ShouldBe("Joker");
        }

        // ----- Feed quirks -----

        [Fact]
        public async Task DuplicateNamesInFeedLastOccurrenceWins()
        {
            // Both entries named "Joker" match the villain parameter; the loop keeps
            // overwriting the static field, so whichever occurs last in the feed wins.
            using var host = WithCharacters(
                Character("Batman", 8.3, "hero"),
                Character("Joker", 8.2, "villain"),
                Character("Joker", 9.9, "villain"));

            var response = await host.Battle("?hero=Batman&villain=Joker");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await BodyAsJson(response);
            body.Value<string>("name").ShouldBe("Joker");
            body.Value<double>("score").ShouldBe(9.9);
        }

        [Fact]
        public async Task SameNameAsHeroAndVillainReturnsThatCharacter()
        {
            // Both "if"s (not "else if") run for every item, so a single character matching
            // both hero and villain is assigned to both static fields, and the ">" comparison
            // against itself is false, so it comes back via the villain branch.
            using var host = WithCharacters(Character("Batman", 8.3, "hero"));

            var response = await host.Battle("?hero=Batman&villain=Batman");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await BodyAsJson(response);
            body.Value<string>("name").ShouldBe("Batman");
            body.Value<double>("score").ShouldBe(8.3);
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

        // ----- Static-field leak across requests -----

        [Fact]
        public async Task UnknownHeroReusesPreviousRequestsHero()
        {
            // _character1/_character2 are static, so an unrecognised name doesn't clear
            // the field or error - it silently leaves the previous request's character in
            // place, and that stale value takes part in the comparison.
            using var host = WithCharacters(Character("Superman", 9.6, "hero"), Character("Joker", 8.2, "villain"));

            var priming = await host.Battle("?hero=Superman&villain=Joker");
            (await BodyAsJson(priming)).Value<string>("name").ShouldBe("Superman"); // sanity check the prime

            var response = await host.Battle("?hero=NobodyKnown&villain=Joker");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await BodyAsJson(response);
            body.Value<string>("name").ShouldBe("Superman");
        }

        [Fact]
        public async Task UnknownVillainReusesPreviousRequestsVillain()
        {
            using var host = WithCharacters(Character("Batman", 8.3, "hero"), Character("Thanos", 9.9, "villain"));

            var priming = await host.Battle("?hero=Batman&villain=Thanos");
            (await BodyAsJson(priming)).Value<string>("name").ShouldBe("Thanos"); // sanity check the prime

            var response = await host.Battle("?hero=Batman&villain=NobodyKnown");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await BodyAsJson(response);
            body.Value<string>("name").ShouldBe("Thanos");
        }

        [Fact]
        public async Task MissingQueryParametersReuseThePreviousBattle()
        {
            // With no "hero"/"villain" query parameters at all (both bind to null), no feed
            // entry ever matches, so the endpoint just replays the previous comparison.
            using var host = WithCharacters(Character("Batman", 8.3, "hero"), Character("Joker", 8.2, "villain"));

            var priming = await host.Battle("?hero=Batman&villain=Joker");
            (await BodyAsJson(priming)).Value<string>("name").ShouldBe("Batman"); // sanity check the prime

            var response = await host.Battle();
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await BodyAsJson(response);
            body.Value<string>("name").ShouldBe("Batman");
        }

        [Fact]
        public async Task StateLeaksAcrossSeparateTestHosts()
        {
            // The comparison fields are static on the BattleController type, not scoped to a
            // WebApplicationFactory/DI container instance, so the leak survives disposing one
            // host and standing up a completely independent one with an unrelated feed.
            using (var hostA = WithCharacters(Character("Batman", 8.3, "hero"), Character("Joker", 8.2, "villain")))
            {
                var priming = await hostA.Battle("?hero=Batman&villain=Joker");
                (await BodyAsJson(priming)).Value<string>("name").ShouldBe("Batman"); // sanity check the prime
            }

            using var hostB = WithCharacters(Character("Superman", 9.6, "hero"), Character("Lex Luthor", 8, "villain"));

            var response = await hostB.Battle("?hero=NobodyKnown&villain=AlsoUnknown");
            response.StatusCode.ShouldBe(HttpStatusCode.OK);

            var body = await BodyAsJson(response);
            body.Value<string>("name").ShouldBe("Batman");
        }
    }
}
