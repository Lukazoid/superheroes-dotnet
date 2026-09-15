using System.Threading.Tasks;

namespace Superheroes.Tests
{
    public class FakeCharactersProvider : ICharactersProvider
    {
        CharactersResponse _response;

        public int CallCount { get; private set; }

        public void FakeResponse(CharactersResponse response)
        {
            _response = response;
        }

        public Task<CharactersResponse> GetCharacters()
        {
            CallCount++;
            return Task.FromResult(_response);
        }
    }
}
