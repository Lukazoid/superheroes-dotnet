using Superheroes.Application.Characters;

namespace Superheroes.Application.Battles;

/// <summary>
/// The outcome of a battle: either a winning Character, or one or more validation errors
/// keyed the way ASP.NET's ModelState.AddModelError expects (field name to message), so a
/// controller can pass them straight through as a 400 response.
/// </summary>
public class BattleResult
{
    private BattleResult(Character? winner, IReadOnlyDictionary<string, string> errors)
    {
        Winner = winner;
        Errors = errors;
    }

    public bool Success => Errors.Count == 0;

    public Character? Winner { get; }

    public IReadOnlyDictionary<string, string> Errors { get; }

    public static BattleResult Won(Character winner) =>
        new(winner, new Dictionary<string, string>());

    public static BattleResult Invalid(IReadOnlyDictionary<string, string> errors) =>
        new(null, errors);
}
