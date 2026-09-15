using System.Text.Json;
using Superheroes.Application.Ports;
using Domain = Superheroes.Application.Characters;

namespace Superheroes.Adapters.S3
{
    // Public (rather than internal, as the pre-hex CharactersProvider was) because the
    // composition root registering it by concrete type - services.AddSingleton<ICharacterLoader,
    // S3CharacterLoader>() - now lives in a different assembly (the host project).
    public class S3CharacterLoader : ICharacterLoader
    {
        private const string CharactersUri = "https://s3.eu-west-2.amazonaws.com/build-circle/characters.json";
        private readonly HttpClient _client = new HttpClient();

        // Character's "type" discriminator must be readable wherever it falls in the object -
        // the feed puts it after "name"/"score", not first as System.Text.Json's polymorphic
        // reader otherwise requires.
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            AllowOutOfOrderMetadataProperties = true
        };

        public async Task<Domain.CharacterCatalogue> GetCharacters()
        {
            var response = await _client.GetAsync(CharactersUri);

            var responseJson = await response.Content.ReadAsStringAsync();
            var document = JsonSerializer.Deserialize<CharactersDocument>(responseJson, SerializerOptions);

            return Domain.CharacterCatalogue.Create(document!.Items!.Select(Map));
        }

        private static Domain.Character Map(Character character) => character switch
        {
            Hero hero => new Domain.Hero(hero.Name, hero.Score, hero.Weakness),
            Villain villain => new Domain.Villain(villain.Name, villain.Score),
            _ => throw new ArgumentOutOfRangeException(nameof(character), character, "Unknown character type.")
        };
    }
}
