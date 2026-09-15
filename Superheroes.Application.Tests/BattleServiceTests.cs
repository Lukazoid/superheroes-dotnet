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
/// detection this suite used to pin via its fake catalogue setup.
/// </summary>
public class BattleServiceTests
{
    private readonly ICharacterLoader _characterLoader = Substitute.For<ICharacterLoader>();
    private readonly BattleService _sut;

    public BattleServiceTests() => _sut = new BattleService(_characterLoader);

    // ----- Winner selection -----

    [Fact]
    public async Task HigherScoringHeroWins()
    {
        // This Batman has no configured weakness, so no penalty applies - see
        // WeaknessKnocksAPointOffTheHeroScore for the case where one does.
        var batman = new Hero("Batman", 8.3, null);
        var joker = new Villain("Joker", 8.2);
        _characterLoader.GetCharacters().Returns(CharacterCatalogue.Create([batman, joker]));

        var result = await _sut.Battle("Batman", "Joker");

        result.Success.ShouldBeTrue();
        result.Winner!.Name.ShouldBe(batman.Name);
    }

    [Fact]
    public async Task HigherScoringVillainWins()
    {
        var gamora = new Hero("Gamora", 8.4, null);
        var thanos = new Villain("Thanos", 9.9);
        _characterLoader.GetCharacters().Returns(CharacterCatalogue.Create([gamora, thanos]));

        var result = await _sut.Battle("Gamora", "Thanos");

        result.Winner!.Name.ShouldBe(thanos.Name);
    }

    [Fact]
    public async Task EqualScoresReturnTheVillain()
    {
        // The comparison is a strict ">", so a tie falls through to the villain.
        var batman = new Hero("Batman", 8.0, null);
        var joker = new Villain("Joker", 8.0);
        _characterLoader.GetCharacters().Returns(CharacterCatalogue.Create([batman, joker]));

        var result = await _sut.Battle("Batman", "Joker");

        result.Winner!.Name.ShouldBe(joker.Name);
    }

    // ----- Weakness scoring -----

    [Fact]
    public async Task WeaknessKnocksAPointOffTheHeroScore()
    {
        // Batman's weakness is Joker: 8.3 - 1 = 7.3, which now loses to Joker's 8.2.
        // Confirms README acceptance test #1 via the weakness rule itself, rather than
        // by coincidence of raw score as in HigherScoringHeroWins.
        var batman = new Hero("Batman", 8.3, "Joker");
        var joker = new Villain("Joker", 8.2);
        _characterLoader.GetCharacters().Returns(CharacterCatalogue.Create([batman, joker]));

        var result = await _sut.Battle("Batman", "Joker");

        result.Winner!.Name.ShouldBe(joker.Name);
    }

    [Fact]
    public async Task WeaknessMatchingIsCaseInsensitive()
    {
        // Batman's weakness is stored as "Joker", but the feed's villain entry is
        // differently-cased ("JOKER") - the penalty should still apply, consistent with
        // the case-insensitive hero/villain name matching above.
        var batman = new Hero("Batman", 8.3, "Joker");
        var joker = new Villain("JOKER", 8.2);
        _characterLoader.GetCharacters().Returns(CharacterCatalogue.Create([batman, joker]));

        var result = await _sut.Battle("Batman", "JOKER");

        result.Winner!.Name.ShouldBe(joker.Name);
    }

    [Fact]
    public async Task WeaknessOnlyAppliesAgainstTheNamedVillain()
    {
        // Batman's weakness is Joker, but he isn't fighting Joker here, so no penalty
        // applies and his raw score (8.3) still beats Thanos's 8.2.
        var batman = new Hero("Batman", 8.3, "Joker");
        var thanos = new Villain("Thanos", 8.2);
        _characterLoader.GetCharacters().Returns(CharacterCatalogue.Create([batman, thanos]));

        var result = await _sut.Battle("Batman", "Thanos");

        result.Winner!.Name.ShouldBe(batman.Name);
    }

    [Fact]
    public async Task ReturnedWinnerScoreIsNotAdjustedForWeakness()
    {
        // The -1 penalty affects only the winner comparison; the returned character
        // still carries its raw score, not the weakness-adjusted one.
        var superman = new Hero("Superman", 9.6, "Lex Luthor");
        var lexLuthor = new Villain("Lex Luthor", 8);
        _characterLoader.GetCharacters().Returns(CharacterCatalogue.Create([superman, lexLuthor]));

        var result = await _sut.Battle("Superman", "Lex Luthor");

        result.Winner!.Name.ShouldBe(superman.Name);
        result.Winner!.Score.ShouldBe(superman.Score);
    }

    // ----- Hero/villain type validation -----

    [Fact]
    public async Task HeroVersusHeroIsInvalid()
    {
        var batman = new Hero("Batman", 8.3, null);
        var superman = new Hero("Superman", 9.6, null);
        _characterLoader.GetCharacters().Returns(CharacterCatalogue.Create([batman, superman]));

        var result = await _sut.Battle("Batman", "Superman");

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContainKeyAndValue("villain", "Villain is required");
    }

    [Fact]
    public async Task VillainVersusVillainIsInvalid()
    {
        var joker = new Villain("Joker", 8.2);
        var thanos = new Villain("Thanos", 9.9);
        _characterLoader.GetCharacters().Returns(CharacterCatalogue.Create([joker, thanos]));

        var result = await _sut.Battle("Joker", "Thanos");

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContainKeyAndValue("hero", "Hero is required");
    }

    [Fact]
    public async Task SameNameAsHeroAndVillainIsInvalid()
    {
        // A single character has one Type, so using the same name for both hero and
        // villain can never satisfy both checks at once.
        var batman = new Hero("Batman", 8.3, null);
        _characterLoader.GetCharacters().Returns(CharacterCatalogue.Create([batman]));

        var result = await _sut.Battle("Batman", "Batman");

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContainKeyAndValue("villain", "Villain is required");
    }

    [Fact]
    public async Task UnknownHeroIsInvalid()
    {
        var superman = new Hero("Superman", 9.6, null);
        var joker = new Villain("Joker", 8.2);
        _characterLoader.GetCharacters().Returns(CharacterCatalogue.Create([superman, joker]));

        var result = await _sut.Battle("NobodyKnown", "Joker");

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContainKeyAndValue("hero", "Hero is required");
    }

    [Fact]
    public async Task UnknownVillainIsInvalid()
    {
        var superman = new Hero("Superman", 9.6, null);
        var joker = new Villain("Joker", 8.2);
        _characterLoader.GetCharacters().Returns(CharacterCatalogue.Create([superman, joker]));

        var result = await _sut.Battle("Superman", "NobodyKnown");

        result.Success.ShouldBeFalse();
        result.Errors.ShouldContainKeyAndValue("villain", "Villain is required");
    }

    [Fact]
    public async Task BothNamesMissingReportsBothErrors()
    {
        var batman = new Hero("Batman", 8.3, null);
        var joker = new Villain("Joker", 8.2);
        _characterLoader.GetCharacters().Returns(CharacterCatalogue.Create([batman, joker]));

        var result = await _sut.Battle(null, null);

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
        var batman = new Hero("Batman", 8.3, null);
        var joker = new Villain("Joker", 8.2);
        _characterLoader.GetCharacters().Returns(CharacterCatalogue.Create([batman, joker]));

        var result = await _sut.Battle("batman", "Joker");

        result.Success.ShouldBeTrue();
        result.Winner!.Name.ShouldBe(batman.Name);
    }

    // ----- Error paths -----

    [Fact]
    public async Task NullFeedThrows()
    {
        // ICharacterLoader.GetCharacters() returning null (e.g. a failed/undeserializable
        // source response) crashes with a NullReferenceException on "catalogue.Heroes".
        _characterLoader.GetCharacters().Returns((CharacterCatalogue)null!);

        await Should.ThrowAsync<NullReferenceException>(() => _sut.Battle("Batman", "Joker"));
    }
}
