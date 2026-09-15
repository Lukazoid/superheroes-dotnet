using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Superheroes
{
    internal class CharactersProvider : ICharactersProvider
    {
        private const string CharactersUri = "https://s3.eu-west-2.amazonaws.com/build-circle/characters.json";
        readonly HttpClient _client = new HttpClient();

        // CharacterResponse's "type" discriminator must be readable wherever it falls in the
        // object - the feed puts it after "name"/"score", not first as System.Text.Json's
        // polymorphic reader otherwise requires.
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            AllowOutOfOrderMetadataProperties = true
        };

        public async Task<CharactersResponse> GetCharacters()
        {
            var response = await _client.GetAsync(CharactersUri);

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<CharactersResponse>(responseJson, SerializerOptions);
        }
    }
}