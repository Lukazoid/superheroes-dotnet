using System.Text.Json.Serialization;

namespace Superheroes.Responses
{
    public sealed record HeroResponse(
        string Name,
        double Score,
        [property: JsonPropertyName("weakness")] string? Weakness) : CharacterResponse(Name, Score);
}
