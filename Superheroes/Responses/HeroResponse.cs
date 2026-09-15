using System.Text.Json.Serialization;

namespace Superheroes.Responses
{
    public sealed class HeroResponse : CharacterResponse
    {
        [JsonPropertyName("weakness")]
        public string Weakness { get; set; }
    }
}
