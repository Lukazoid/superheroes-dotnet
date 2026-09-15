#nullable enable annotations
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Superheroes.Controllers
{
    [Route("battle")]
    public class BattleController : Controller
    {
        private readonly ICharactersProvider _charactersProvider;

        public BattleController(ICharactersProvider charactersProvider)
        {
            _charactersProvider = charactersProvider;
        }

        
        public async Task<IActionResult> Get(string hero, string villain)
        {
            var characters = await _charactersProvider.GetCharacters();
            
            CharacterResponse? heroCharacter = null;
            CharacterResponse? villainCharacter = null;
            foreach(var character in characters.Items)
            {
                if(string.Equals(character.Name, hero, StringComparison.InvariantCultureIgnoreCase) && character.Type == "hero")
                {
                    heroCharacter = character;
                }
                if(string.Equals(character.Name, villain, StringComparison.InvariantCultureIgnoreCase) && character.Type == "villain")
                {
                    villainCharacter = character;
                }

                if (heroCharacter is not null && villainCharacter is not null)
                    break;
            }

            if(heroCharacter is null)
            {
                ModelState.AddModelError(nameof(hero), "Hero is required");
            }
            
            if(villainCharacter is null)
            {
                ModelState.AddModelError(nameof(villain), "Villain is required");
            }

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var heroScore = heroCharacter.Score;
            if (heroCharacter.Weakness == villainCharacter.Name)
            {
                heroScore--;
            }
            
            if(heroScore > villainCharacter.Score)
            {
                return Ok(heroCharacter);
            }
            
            return Ok(villainCharacter);
        }
    }
}