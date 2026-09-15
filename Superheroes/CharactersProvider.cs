using System;
using System.Collections.Immutable;
using System.Linq;
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

            // Throws if the feed has two entries for the same name (matched case-insensitively)
            // rather than silently picking a winner.
            return charactersResponse.Items.ToImmutableDictionary(c => c.Name, StringComparer.OrdinalIgnoreCase);
        }
    }
}
