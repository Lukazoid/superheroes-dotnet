using System.Text.Json.Serialization;

namespace Superheroes
{
    public sealed class HeroResponse : CharacterResponse
    {
        [JsonPropertyName("weakness")]
        public string Weakness { get; set; }
    }
}
