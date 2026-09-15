using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Superheroes.Controllers
{
    [ApiController]
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
        [HttpGet]
        [EndpointSummary("Determines the winner of a battle between a hero and a villain.")]
        [EndpointDescription("Looks up the named hero and villain and returns whichever has the higher score, after applying the hero's weakness penalty if it matches the villain's name.")]
        [ProducesResponseType<CharacterResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<IDictionary<string, string[]>>(StatusCodes.Status400BadRequest)]
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
