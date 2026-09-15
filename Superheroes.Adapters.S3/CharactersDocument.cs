using System.Text.Json.Serialization;

namespace Superheroes.Adapters.S3
{
    public sealed record CharactersDocument
    {
        [JsonPropertyName("items")]
        public Character[]? Items { get; init; }
    }
}
