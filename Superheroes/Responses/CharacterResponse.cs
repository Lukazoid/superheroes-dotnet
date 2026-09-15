using System.Text.Json.Serialization;

namespace Superheroes.Responses
{
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
    [JsonDerivedType(typeof(HeroResponse), "hero")]
    [JsonDerivedType(typeof(VillainResponse), "villain")]
    public abstract class CharacterResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }
    }
}
