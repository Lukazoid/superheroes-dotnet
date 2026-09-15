using Superheroes.Application.Characters;

namespace Superheroes.Responses;

public static class CharacterResponseMapper
{
    public static CharacterResponse ToResponse(this Character character) => character switch
    {
        Hero hero => new HeroResponse(hero.Name, hero.Score, hero.Weakness),
        Villain villain => new VillainResponse(villain.Name, villain.Score),
        _ => throw new ArgumentOutOfRangeException(nameof(character), character, "Unknown character type.")
    };
}
