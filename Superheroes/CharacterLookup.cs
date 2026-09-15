using System.Collections.Immutable;

namespace Superheroes
{
    /// <summary>
    /// Builds the name -> character lookup that BattleController resolves hero/villain names
    /// against. Folds the feed in order using the dictionary builder's indexer (which overwrites),
    /// so a duplicate name's last occurrence wins - matching the foreach loop this replaced in
    /// BattleController. A null response, or a response with a null Items array, throws
    /// NullReferenceException, also matching that loop's behaviour: the feed is expected to
    /// always provide one.
    /// </summary>
    public static class CharacterLookup
    {
        public static ImmutableDictionary<string, CharacterResponse> Build(CharactersResponse response)
        {
            var builder = ImmutableDictionary.CreateBuilder<string, CharacterResponse>();

            foreach (var character in response.Items)
            {
                builder[character.Name] = character;
            }

            return builder.ToImmutable();
        }
    }
}
