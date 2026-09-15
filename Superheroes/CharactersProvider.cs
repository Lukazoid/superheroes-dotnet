using System.Collections.Immutable;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace Superheroes
{
    internal class CharactersProvider : ICharactersProvider
    {
        private const string CharactersUri = "https://s3.eu-west-2.amazonaws.com/build-circle/characters.json";
        readonly HttpClient _client = new HttpClient();


        public async Task<ImmutableDictionary<string, CharacterResponse>> GetCharacters()
        {
            var response = await _client.GetAsync(CharactersUri);

            var responseJson = await response.Content.ReadAsStringAsync();
            var charactersResponse = JsonSerializer.Deserialize<CharactersResponse>(responseJson);

            return CharacterLookup.Build(charactersResponse);
        }
    }
}
