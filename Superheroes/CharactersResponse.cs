using System.Text.Json.Serialization;

namespace Superheroes
{
    public class CharactersResponse
    {
        [JsonPropertyName("items")]
        public CharacterResponse[] Items { get; set; }
    }
}
