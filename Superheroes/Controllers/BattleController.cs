using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace Superheroes.Controllers
{
    [Route("battle")]
    public class BattleController : Controller
    {
        private readonly IBattleService _battleService;

        public BattleController(IBattleService battleService)
        {
            _battleService = battleService;
        }

        // Declared as CharacterResponse (the polymorphic base), not IActionResult, so the
        // output formatter serializes through it and writes the "type" discriminator -
        // returning Ok(result.Winner) directly would serialize by the winner's runtime type
        // instead and silently drop it.
        public async Task<ActionResult<CharacterResponse>> Get(string hero, string villain)
        {
            var result = await _battleService.Battle(hero, villain);

            if (!result.Success)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(error.Key, error.Value);
                }
                return BadRequest(ModelState);
            }

            return result.Winner;
        }
    }
}
