using System;
using System.Threading.Tasks;
using NSubstitute;
using Shouldly;
using Superheroes.Application.Battles;
using Superheroes.Application.Characters;
using Superheroes.Application.Ports;
using Xunit;

namespace Superheroes.Application.Tests;

/// <summary>
/// Unit tests for BattleService's business logic - winner selection, weakness
/// scoring, hero/villain type validation, and feed-matching quirks - exercised
/// directly against the service with no HTTP host involved. See
/// BattleCharacterizationTests.cs (Superheroes.Tests) for the remaining HTTP/controller-level
/// concerns (routing, response contract, the 400/500 translation) that this
/// suite intentionally doesn't re-cover. See CharacterCatalogueTests for the duplicate-name
/// detection this suite used to pin via its fake provider setup.
/// </summary>
public class BattleServiceTests
{
    private static Character Character(string name, double score, CharacterType type, string? weakness = null) => type switch
    {
        CharacterType.Hero => new Hero(name, score, weakness),
        CharacterType.Villain when weakness is null => new Villain(name, score),
        CharacterType.Villain => throw new ArgumentException("Villains cannot have a weakness.", nameof(weakness)),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown character type.")
    };

    private static BattleService ServiceFor(params Character[] characters)
    {
        var characterLoader = Substitute.For<ICharacterLoader>();
        characterLoader.GetCharacters().Returns(CharacterCatalogue.Create(characters));
        return new BattleService(characterLoader);
    }

    // ----- Winner selection -----

    [Fact]
    public async Task HigherScoringHeroWins()
    {
        // This Batman has no configured weakness, so no penalty applies - see
        // WeaknessKnocksAPointOffTheHeroScore for the case where one does.
        var service = ServiceFor(Character("Batman", 8.3, CharacterType.Hero), Character("Joker", 8.2, CharacterType.Villain));

        var result = await service.Battle("Batman", "Joker");

        result.Success.ShouldBeTrue();
        result.Winner!.Name.ShouldBe("Batman");
    }

    [Fact]
    public async Task HigherScoringVillainWins()
    {
        var service = ServiceFor(Character("Gamora", 8.4, CharacterType.Hero), Character("Thanos", 9.9, CharacterType.Villain));

        var result = await service.Battle("Gamora", "Thanos");

        result.Winner!.Name.ShouldBe("Thanos");
    }

    [Fact]
    public async Task EqualScoresReturnTheVillain()
    {
        // The comparison is a strict ">", so a tie falls through to the villain.
        var service = ServiceFor(Character("Batman", 8.0, CharacterType.Hero), Character("Joker", 8.0, CharacterType.Villain));

        var result = await service.Battle("Batman", "Joker");

        result.Winner!.Name.ShouldBe("Joker");
    }

    // ----- Weakness scoring -----

    [Fact]
    public async Task WeaknessKnocksAPointOffTheHeroScore()
    {
        // Batman's weakness is Joker: 8.3 - 1 = 7.3, which now loses to Joker's 8.2.
        // Confirms README acceptance test #1 via the weakness rule itself, rather than
        // by coincidence of raw score as in HigherScoringHeroWins.
        var service = ServiceFor(
            Character("Batman", 8.3, CharacterType.Hero, weakness: "Joker"),
            Character("Joker", 8.2, CharacterType.Villain));

        var result = await service.Battle("Batman", "Joker");

        result.Winner!.Name.ShouldBe("Joker");
    }

    [Fact]
    public async Task WeaknessMatchingIsCaseInsensitive()
    {
        // Batman's weakness is stored as "Joker", but the feed's villain entry is
        // differently-cased ("JOKER") - the penalty should still apply, consistent with
        // the case-insensitive hero/villain name matching above.
        var service = ServiceFor(
            Character("Batman", 8.3, CharacterType.Hero, weakness: "Joker"),
            Character("JOKER", 8.2, CharacterType.Villain));

        var result = await service.Battle("Batman", "JOKER");

        result.Winner!.Name.ShouldBe("JOKER");
    }

    [Fact]
    public async Task WeaknessOnlyAppliesAgainstTheNamedVillain()
    {
        // Batman's weakness is Joker, but he isn't fighting Joker here, so no penalty
        // applies and his raw score (8.3) still beats Thanos's 8.2.
        var service = ServiceFor(
            Character("Batman", 8.3, CharacterType.Hero, weakness: "Joker"),
            Character("Thanos", 8.2, CharacterType.Villain));

        var result = await service.Battle("Batman", "Thanos");

        result.Winner!.Name.ShouldBe("Batman");
    }

    [Fact]
    public async Task ReturnedWinnerScoreIsNotAdjustedForWeakness()
    {
        // The -1 penalty affects only the winner comparison; the returned character
        // still carries its raw score, not the weakness-adjusted one.
        var service = ServiceFor(
            Character("Superman", 9.6, CharacterType.Hero, weakness: "Lex Luthor"),
            Character("Lex Luthor", 8, CharacterType.Villain));

        var result = await service.Battle("Superman", "Lex Luthor");

        result.Winner!.Name.ShouldBe("Superman");
        result.Winner!.Score.ShouldBe(9.6);
    }

    // ----- Hero/villain type validation -----

    [Fact]
    public async Task HeroVersusHeroIsInvalid()
    {
        var service = ServiceFor(Character("Batman", 8.3, CharacterType.Hero), Character("Superman", 9.6, CharacterType.Hero));

        var result = await service.Battle("Batman", "Superman");

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContainKeyAndValue("villain", "Villain is required");
    }

    [Fact]
    public async Task VillainVersusVillainIsInvalid()
    {
        var service = ServiceFor(Character("Joker", 8.2, CharacterType.Villain), Character("Thanos", 9.9, CharacterType.Villain));

        var result = await service.Battle("Joker", "Thanos");

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContainKeyAndValue("hero", "Hero is required");
    }

    [Fact]
    public async Task SameNameAsHeroAndVillainIsInvalid()
    {
        // A single character has one Type, so using the same name for both hero and
        // villain can never satisfy both checks at once.
        var service = ServiceFor(Character("Batman", 8.3, CharacterType.Hero));

        var result = await service.Battle("Batman", "Batman");

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContainKeyAndValue("villain", "Villain is required");
    }

    [Fact]
    public async Task UnknownHeroIsInvalid()
    {
        var service = ServiceFor(Character("Superman", 9.6, CharacterType.Hero), Character("Joker", 8.2, CharacterType.Villain));

        var result = await service.Battle("NobodyKnown", "Joker");

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContainKeyAndValue("hero", "Hero is required");
    }

    [Fact]
    public async Task UnknownVillainIsInvalid()
    {
        var service = ServiceFor(Character("Superman", 9.6, CharacterType.Hero), Character("Joker", 8.2, CharacterType.Villain));

        var result = await service.Battle("Superman", "NobodyKnown");

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContainKeyAndValue("villain", "Villain is required");
    }

    [Fact]
    public async Task BothNamesMissingReportsBothErrors()
    {
        var service = ServiceFor(Character("Batman", 8.3, CharacterType.Hero), Character("Joker", 8.2, CharacterType.Villain));

        var result = await service.Battle(null, null);

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContainKeyAndValue("hero", "Hero is required");
        result.Errors.ShouldContainKeyAndValue("villain", "Villain is required");
    }

    // ----- Feed-matching quirks -----

    [Fact]
    public async Task CharacterNameMatchingIsCaseInsensitive()
    {
        // The catalogue's dictionaries are keyed with OrdinalIgnoreCase, so a
        // differently-cased "batman" still matches the "Batman" entry.
        var service = ServiceFor(Character("Batman", 8.3, CharacterType.Hero), Character("Joker", 8.2, CharacterType.Villain));

        var result = await service.Battle("batman", "Joker");

        result.Success.ShouldBeTrue();
        result.Winner!.Name.ShouldBe("Batman");
    }

    // ----- Error paths -----

    [Fact]
    public async Task NullFeedThrows()
    {
        // ICharacterLoader.GetCharacters() returning null (e.g. a failed/undeserializable
        // source response) crashes with a NullReferenceException on "catalogue.Heroes".
        var characterLoader = Substitute.For<ICharacterLoader>();
        characterLoader.GetCharacters().Returns((CharacterCatalogue)null!);
        var service = new BattleService(characterLoader);

        await Should.ThrowAsync<NullReferenceException>(() => service.Battle("Batman", "Joker"));
    }
}
