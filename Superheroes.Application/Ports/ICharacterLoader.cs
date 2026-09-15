using Superheroes.Application.Characters;

namespace Superheroes.Application.Ports
{
    /// <summary>
    /// A source of characters for battles. The port hex architecture separates from its
    /// implementation - see Superheroes.Adapters.S3's S3CharacterLoader for the adapter that
    /// reads the live feed, and CachingCharacterLoader for the decorator that sits in front of it.
    /// </summary>
    public interface ICharacterLoader
    {
        Task<CharacterCatalogue> GetCharacters();
    }
}
