using System.Text.Json.Serialization;

namespace Superheroes
{
    public class CharacterResponse
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }
    }
}
