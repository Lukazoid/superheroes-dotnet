using System.Collections.Immutable;

namespace Superheroes.Application.Characters;

/// <summary>
/// The full roster a character source (an ICharacterLoader) hands back, split by role so
/// BattleService can look a name up as "the hero" or "the villain" directly, and so nothing
/// polymorphic needs to cross a cache boundary - both dictionaries' value types are concrete,
/// sealed records, which System.Text.Json serializes by declared type with no discriminator.
/// </summary>
public sealed record CharacterCatalogue(
    ImmutableDictionary<string, Hero> Heroes,
    ImmutableDictionary<string, Villain> Villains)
{
    /// <summary>
    /// Builds a catalogue from a flat set of characters, matching names case-insensitively.
    /// Throws if any two characters share a name - regardless of role - rather than silently
    /// picking a winner: a hero and a villain named "Joker" would be just as ambiguous as two
    /// villains named "Joker".
    /// </summary>
    public static CharacterCatalogue Create(IEnumerable<Character> characters)
    {
        ArgumentNullException.ThrowIfNull(characters);

        var byName = characters.ToImmutableDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);

        var heroes = byName.Values.OfType<Hero>().ToImmutableDictionary(h => h.Name, StringComparer.OrdinalIgnoreCase);
        var villains = byName.Values.OfType<Villain>().ToImmutableDictionary(v => v.Name, StringComparer.OrdinalIgnoreCase);

        return new CharacterCatalogue(heroes, villains);
    }
}
