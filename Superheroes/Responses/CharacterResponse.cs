using System.Text.Json.Serialization;

namespace Superheroes.Responses;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(HeroResponse), "hero")]
[JsonDerivedType(typeof(VillainResponse), "villain")]
public abstract record CharacterResponse(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("score")] double Score);
