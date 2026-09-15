namespace Superheroes.Responses
{
    public sealed record VillainResponse(string Name, double Score) : CharacterResponse(Name, Score);
}
