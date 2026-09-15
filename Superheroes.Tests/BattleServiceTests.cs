using Xunit;
using Shouldly;
using static Superheroes.Tests.BattleTestHost;

namespace Superheroes.Tests
{
    /// <summary>
    /// Unit tests for BattleService's business logic - winner selection, weakness
    /// scoring, hero/villain type validation, and feed-matching quirks - exercised
    /// directly against the service with no HTTP host involved. See
    /// BattleCharacterizationTests.cs for the remaining HTTP/controller-level
    /// concerns (routing, response contract, the 400/500 translation) that this
    /// suite intentionally doesn't re-cover.
    /// </summary>
    public class BattleServiceTests
    {
        private static BattleService ServiceFor(params CharacterResponse[] characters)
        {
            var charactersProvider = new FakeCharactersProvider();
            charactersProvider.FakeResponse(new CharactersResponse { Items = characters });
            return new BattleService(charactersProvider);
        }

        // ----- Winner selection -----

        [Fact]
        public async Task HigherScoringHeroWins()
        {
            // This Batman has no configured weakness, so no penalty applies - see
            // WeaknessKnocksAPointOffTheHeroScore for the case where one does.
            var service = ServiceFor(Character("Batman", 8.3, "hero"), Character("Joker", 8.2, "villain"));

            var result = await service.Battle("Batman", "Joker");

            result.Success.ShouldBeTrue();
            result.Winner.Name.ShouldBe("Batman");
        }

        [Fact]
        public async Task HigherScoringVillainWins()
        {
            var service = ServiceFor(Character("Gamora", 8.4, "hero"), Character("Thanos", 9.9, "villain"));

            var result = await service.Battle("Gamora", "Thanos");

            result.Winner.Name.ShouldBe("Thanos");
        }

        [Fact]
        public async Task EqualScoresReturnTheVillain()
        {
            // The comparison is a strict ">", so a tie falls through to the villain.
            var service = ServiceFor(Character("Batman", 8.0, "hero"), Character("Joker", 8.0, "villain"));

            var result = await service.Battle("Batman", "Joker");

            result.Winner.Name.ShouldBe("Joker");
        }

        // ----- Weakness scoring -----

        [Fact]
        public async Task WeaknessKnocksAPointOffTheHeroScore()
        {
            // Batman's weakness is Joker: 8.3 - 1 = 7.3, which now loses to Joker's 8.2.
            // Confirms README acceptance test #1 via the weakness rule itself, rather than
            // by coincidence of raw score as in HigherScoringHeroWins.
            var service = ServiceFor(
                Character("Batman", 8.3, "hero", weakness: "Joker"),
                Character("Joker", 8.2, "villain"));

            var result = await service.Battle("Batman", "Joker");

            result.Winner.Name.ShouldBe("Joker");
        }

        [Fact]
        public async Task WeaknessMatchingIsCaseInsensitive()
        {
            // Batman's weakness is stored as "Joker", but the feed's villain entry is
            // differently-cased ("JOKER") - the penalty should still apply, consistent with
            // the case-insensitive hero/villain name matching above.
            var service = ServiceFor(
                Character("Batman", 8.3, "hero", weakness: "Joker"),
                Character("JOKER", 8.2, "villain"));

            var result = await service.Battle("Batman", "JOKER");

            result.Winner.Name.ShouldBe("JOKER");
        }

        [Fact]
        public async Task WeaknessOnlyAppliesAgainstTheNamedVillain()
        {
            // Batman's weakness is Joker, but he isn't fighting Joker here, so no penalty
            // applies and his raw score (8.3) still beats Thanos's 8.2.
            var service = ServiceFor(
                Character("Batman", 8.3, "hero", weakness: "Joker"),
                Character("Thanos", 8.2, "villain"));

            var result = await service.Battle("Batman", "Thanos");

            result.Winner.Name.ShouldBe("Batman");
        }

        [Fact]
        public async Task ReturnedWinnerScoreIsNotAdjustedForWeakness()
        {
            // The -1 penalty affects only the winner comparison; the returned character
            // still carries its raw score, not the weakness-adjusted one.
            var service = ServiceFor(
                Character("Superman", 9.6, "hero", weakness: "Lex Luthor"),
                Character("Lex Luthor", 8, "villain"));

            var result = await service.Battle("Superman", "Lex Luthor");

            result.Winner.Name.ShouldBe("Superman");
            result.Winner.Score.ShouldBe(9.6);
        }

        // ----- Hero/villain type validation -----

        [Fact]
        public async Task HeroVersusHeroIsInvalid()
        {
            var service = ServiceFor(Character("Batman", 8.3, "hero"), Character("Superman", 9.6, "hero"));

            var result = await service.Battle("Batman", "Superman");

            result.Success.ShouldBeFalse();
            result.Errors.ShouldContainKeyAndValue("villain", "Villain is required");
        }

        [Fact]
        public async Task VillainVersusVillainIsInvalid()
        {
            var service = ServiceFor(Character("Joker", 8.2, "villain"), Character("Thanos", 9.9, "villain"));

            var result = await service.Battle("Joker", "Thanos");

            result.Success.ShouldBeFalse();
            result.Errors.ShouldContainKeyAndValue("hero", "Hero is required");
        }

        [Fact]
        public async Task SameNameAsHeroAndVillainIsInvalid()
        {
            // A single character has one Type, so using the same name for both hero and
            // villain can never satisfy both checks at once.
            var service = ServiceFor(Character("Batman", 8.3, "hero"));

            var result = await service.Battle("Batman", "Batman");

            result.Success.ShouldBeFalse();
            result.Errors.ShouldContainKeyAndValue("villain", "Villain is required");
        }

        [Fact]
        public async Task UnknownHeroIsInvalid()
        {
            var service = ServiceFor(Character("Superman", 9.6, "hero"), Character("Joker", 8.2, "villain"));

            var result = await service.Battle("NobodyKnown", "Joker");

            result.Success.ShouldBeFalse();
            result.Errors.ShouldContainKeyAndValue("hero", "Hero is required");
        }

        [Fact]
        public async Task UnknownVillainIsInvalid()
        {
            var service = ServiceFor(Character("Superman", 9.6, "hero"), Character("Joker", 8.2, "villain"));

            var result = await service.Battle("Superman", "NobodyKnown");

            result.Success.ShouldBeFalse();
            result.Errors.ShouldContainKeyAndValue("villain", "Villain is required");
        }

        [Fact]
        public async Task BothNamesMissingReportsBothErrors()
        {
            var service = ServiceFor(Character("Batman", 8.3, "hero"), Character("Joker", 8.2, "villain"));

            var result = await service.Battle(null, null);

            result.Success.ShouldBeFalse();
            result.Errors.ShouldContainKeyAndValue("hero", "Hero is required");
            result.Errors.ShouldContainKeyAndValue("villain", "Villain is required");
        }

        // ----- Feed-matching quirks -----

        [Fact]
        public async Task CharacterNameMatchingIsCaseInsensitive()
        {
            // Names are matched with StringComparison.InvariantCultureIgnoreCase, so a
            // differently-cased "batman" still matches the "Batman" entry.
            var service = ServiceFor(Character("Batman", 8.3, "hero"), Character("Joker", 8.2, "villain"));

            var result = await service.Battle("batman", "Joker");

            result.Success.ShouldBeTrue();
            result.Winner.Name.ShouldBe("Batman");
        }

        [Fact]
        public async Task DuplicateNamesInFeedFirstOccurrenceWins()
        {
            // Both entries named "Joker" match the villain parameter, but the loop breaks
            // as soon as both hero and villain are found, so the first match in the feed
            // wins and the second "Joker" entry is never reached.
            var service = ServiceFor(
                Character("Batman", 8.3, "hero"),
                Character("Joker", 8.6, "villain"),
                Character("Joker", 9.9, "villain"));

            var result = await service.Battle("Batman", "Joker");

            result.Winner.Name.ShouldBe("Joker");
            result.Winner.Score.ShouldBe(8.6);
        }

        // ----- Error paths -----

        [Fact]
        public async Task NullFeedThrows()
        {
            // ICharactersProvider.GetCharacters() returning null (e.g. a failed/undeserializable
            // S3 response) crashes with a NullReferenceException on "characters.Items".
            var charactersProvider = new FakeCharactersProvider();
            charactersProvider.FakeResponse(null);
            var service = new BattleService(charactersProvider);

            await Should.ThrowAsync<NullReferenceException>(() => service.Battle("Batman", "Joker"));
        }

        [Fact]
        public async Task NullItemsThrows()
        {
            // A non-null CharactersResponse with a null Items array crashes the same way,
            // since "foreach" over a null array throws.
            var charactersProvider = new FakeCharactersProvider();
            charactersProvider.FakeResponse(new CharactersResponse { Items = null });
            var service = new BattleService(charactersProvider);

            await Should.ThrowAsync<NullReferenceException>(() => service.Battle("Batman", "Joker"));
        }
    }
}
