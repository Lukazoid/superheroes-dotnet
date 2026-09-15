using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Superheroes
{
    public interface ICharactersProvider
    {
        Task<ImmutableDictionary<string, CharacterResponse>> GetCharacters();
    }
}
