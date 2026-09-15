using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Superheroes.Controllers
{
    [Route("battle")]
    public class BattleController : Controller
    {
        private readonly ICharactersProvider _charactersProvider;
        private static CharacterResponse _character1;
        private static CharacterResponse _character2;

        public BattleController(ICharactersProvider charactersProvider)
        {
            _charactersProvider = charactersProvider;
        }

        public async Task<IActionResult> Get(string hero, string villain)
        {
            var characters = await _charactersProvider.GetCharacters();

            if(hero != null && characters.TryGetValue(hero, out var heroCharacter))
            {
                _character1 = heroCharacter;
            }
            if(villain != null && characters.TryGetValue(villain, out var villainCharacter))
            {
                _character2 = villainCharacter;
            }

            if(_character1.Score > _character2.Score)
            {
                return Ok(_character1);
            }

            return Ok(_character2);
        }
    }
}