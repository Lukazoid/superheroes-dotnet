using System.Text.Json.Serialization;

namespace Superheroes.Adapters.S3
{
    public sealed record Hero : Character
    {
        [JsonPropertyName("weakness")]
        public string? Weakness { get; init; }
    }
}
