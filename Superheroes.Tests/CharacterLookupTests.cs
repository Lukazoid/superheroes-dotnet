using System;
using Shouldly;
using Xunit;

namespace Superheroes.Tests
{
    /// <summary>
    /// Characterizes CharacterLookup.Build, the step that turns the raw characters feed into the
    /// name -> character dictionary BattleController looks up against. Its "last occurrence wins"
    /// and "crashes on a missing Items array" behaviours match the foreach loop it replaced in
    /// BattleController - BattleCharacterizationTests.DuplicateNamesInFeedLastOccurrenceWins and
    /// NullFeedFails pin the same behaviours end-to-end through the /battle endpoint.
    /// </summary>
    public class CharacterLookupTests
    {
        [Fact]
        public void BuildsANameKeyedLookup()
        {
            var response = new CharactersResponse
            {
                Items = new[]
                {
                    new CharacterResponse { Name = "Batman", Score = 8.3, Type = "hero" },
                    new CharacterResponse { Name = "Joker", Score = 8.2, Type = "villain" }
                }
            };

            var lookup = CharacterLookup.Build(response);

            lookup["Batman"].Score.ShouldBe(8.3);
            lookup["Joker"].Score.ShouldBe(8.2);
        }

        [Fact]
        public void DuplicateNamesLastOccurrenceWins()
        {
            var response = new CharactersResponse
            {
                Items = new[]
                {
                    new CharacterResponse { Name = "Joker", Score = 8.2, Type = "villain" },
                    new CharacterResponse { Name = "Joker", Score = 9.9, Type = "villain" }
                }
            };

            var lookup = CharacterLookup.Build(response);

            lookup["Joker"].Score.ShouldBe(9.9);
        }

        [Fact]
        public void NameLookupIsCaseSensitive()
        {
            var response = new CharactersResponse
            {
                Items = new[] { new CharacterResponse { Name = "Batman", Score = 8.3, Type = "hero" } }
            };

            var lookup = CharacterLookup.Build(response);

            lookup.ContainsKey("batman").ShouldBeFalse();
        }

        [Fact]
        public void EmptyItemsArrayProducesEmptyLookup()
        {
            var response = new CharactersResponse { Items = Array.Empty<CharacterResponse>() };

            var lookup = CharacterLookup.Build(response);

            lookup.ShouldBeEmpty();
        }

        [Fact]
        public void NullItemsThrows()
        {
            // Matches the foreach loop this replaced in BattleController: the feed is expected to
            // always provide an Items array, so a null one crashes rather than being treated as empty.
            var response = new CharactersResponse { Items = null };

            Should.Throw<NullReferenceException>(() => CharacterLookup.Build(response));
        }

        [Fact]
        public void NullResponseThrows()
        {
            Should.Throw<NullReferenceException>(() => CharacterLookup.Build(null));
        }
    }
}
