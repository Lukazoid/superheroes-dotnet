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

        // Declared as CharacterResponse (the polymorphic base), not IActionResult, so the
        // output formatter serializes through it and writes the "type" discriminator -
        // returning Ok(heroCharacter) directly would serialize by heroCharacter's runtime
        // type instead and silently drop it.
        public async Task<ActionResult<CharacterResponse>> Get(string hero, string villain)
        {
            var characters = await _charactersProvider.GetCharacters();

            HeroResponse? heroCharacter = null;
            VillainResponse? villainCharacter = null;
            foreach(var character in characters.Items)
            {
                if(character is HeroResponse h && string.Equals(h.Name, hero, StringComparison.InvariantCultureIgnoreCase))
                {
                    heroCharacter = h;
                }
                if(character is VillainResponse v && string.Equals(v.Name, villain, StringComparison.InvariantCultureIgnoreCase))
                {
                    villainCharacter = v;
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
                return heroCharacter;
            }

            return villainCharacter;
        }
    }
}
