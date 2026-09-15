using Superheroes.Application.Characters;

namespace Superheroes.Responses
{
    public static class CharacterResponseMapper
    {
        public static CharacterResponse ToResponse(this Character character) => character switch
        {
            Hero hero => new HeroResponse { Name = hero.Name, Score = hero.Score, Weakness = hero.Weakness },
            Villain villain => new VillainResponse { Name = villain.Name, Score = villain.Score },
            _ => throw new ArgumentOutOfRangeException(nameof(character), character, "Unknown character type.")
        };
    }
}
