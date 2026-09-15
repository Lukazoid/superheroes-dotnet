#nullable enable annotations
using System.Collections.Generic;

namespace Superheroes
{
    /// <summary>
    /// The outcome of a battle: either a winning CharacterResponse, or one or more
    /// validation errors keyed the way ModelState.AddModelError expects (field name to
    /// message), so BattleController can pass them straight through as a 400 response.
    /// </summary>
    public class BattleResult
    {
        private BattleResult(CharacterResponse winner, IReadOnlyDictionary<string, string> errors)
        {
            Winner = winner;
            Errors = errors;
        }

        public bool Success => Errors.Count == 0;

        public CharacterResponse Winner { get; }

        public IReadOnlyDictionary<string, string> Errors { get; }

        public static BattleResult Won(CharacterResponse winner) =>
            new(winner, new Dictionary<string, string>());

        public static BattleResult Invalid(IReadOnlyDictionary<string, string> errors) =>
            new(null, errors);
    }
}
