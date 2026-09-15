using System;
using Shouldly;
using Superheroes.Application.Characters;
using Xunit;

namespace Superheroes.Application.Tests;

/// <summary>
/// CharacterCatalogue.Create is the sole place duplicate-name detection happens, so every
/// ICharacterLoader (currently just the S3 adapter) gets it for free rather than each having
/// to reimplement it. Previously this behaviour was only pinned indirectly, via the test
/// helper BattleServiceTests.ServiceFor built its fake provider's response with; it's real
/// production code now.
/// </summary>
public class CharacterCatalogueTests
{
    [Fact]
    public void PartitionsCharactersByRole()
    {
        var catalogue = CharacterCatalogue.Create(new Character[]
        {
            new Hero("Batman", 8.3, null),
            new Villain("Joker", 8.2)
        });

        catalogue.Heroes.ContainsKey("Batman").ShouldBeTrue();
        catalogue.Villains.ContainsKey("Joker").ShouldBeTrue();
        catalogue.Heroes.ContainsKey("Joker").ShouldBeFalse();
        catalogue.Villains.ContainsKey("Batman").ShouldBeFalse();
    }

    [Fact]
    public void NameLookupIsCaseInsensitive()
    {
        var catalogue = CharacterCatalogue.Create(new Character[] { new Hero("Batman", 8.3, null) });

        catalogue.Heroes.ContainsKey("batman").ShouldBeTrue();
    }

    [Fact]
    public void DuplicateNamesInFeedThrows()
    {
        // The feed is expected to have at most one entry per name (matched
        // case-insensitively) - Create throws rather than silently picking a winner between
        // the two "Joker" entries.
        Should.Throw<ArgumentException>(() =>
            CharacterCatalogue.Create(new Character[]
            {
                new Hero("Batman", 8.3, null),
                new Villain("Joker", 8.6),
                new Villain("Joker", 9.9)
            }));
    }

    [Fact]
    public void DuplicateNamesDifferingOnlyByCaseInFeedThrows()
    {
        Should.Throw<ArgumentException>(() =>
            CharacterCatalogue.Create(new Character[]
            {
                new Villain("Joker", 8.2),
                new Villain("JOKER", 9.9)
            }));
    }

    [Fact]
    public void DuplicateNamesAcrossRolesThrows()
    {
        // A hero and a villain sharing a name is just as ambiguous as two villains sharing
        // one, even though they'd land in different dictionaries - Create rejects it rather
        // than letting one role's entry silently mask the other's.
        Should.Throw<ArgumentException>(() =>
            CharacterCatalogue.Create(new Character[]
            {
                new Hero("Loki", 7.0, null),
                new Villain("Loki", 9.0)
            }));
    }
}
