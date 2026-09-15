using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Superheroes.Application.Battles;
using Superheroes.Responses;

namespace Superheroes.Controllers;

[Route("battle")]
public class BattleController(IBattleService battleService) : Controller
{
    // Declared as CharacterResponse (the polymorphic base), not IActionResult, so the
    // output formatter serializes through it and writes the "type" discriminator -
    // returning Ok(result.Winner) directly would serialize by the winner's runtime type
    // instead and silently drop it.
    public async Task<ActionResult<CharacterResponse>> Get(string? hero, string? villain)
    {
        var result = await battleService.Battle(hero, villain);

        if (!result.Success)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(error.Key, error.Value);
            }
            return BadRequest(ModelState);
        }

        // result.Winner is non-null here: Success (Errors.Count == 0) only holds for the
        // BattleResult.Won path, which always carries a winner - see BattleResult.
        return result.Winner!.ToResponse();
    }
}
