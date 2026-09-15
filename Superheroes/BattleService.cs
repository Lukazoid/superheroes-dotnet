#nullable enable annotations
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Superheroes
{
    public class BattleService : IBattleService
    {
        private readonly ICharactersProvider _charactersProvider;

        public BattleService(ICharactersProvider charactersProvider)
        {
            _charactersProvider = charactersProvider;
        }

        public async Task<BattleResult> Battle(string hero, string villain)
        {
            var characters = await _charactersProvider.GetCharacters();

            // The dictionary's keys are already matched case-insensitively (see
            // CharactersProvider), so no StringComparison is needed here - just the extra
            // Type check, since a name match alone doesn't guarantee the right role.
            CharacterResponse? heroCharacter = null;
            if (hero != null && characters.TryGetValue(hero, out var heroMatch) && heroMatch.Type == "hero")
            {
                heroCharacter = heroMatch;
            }

            CharacterResponse? villainCharacter = null;
            if (villain != null && characters.TryGetValue(villain, out var villainMatch) && villainMatch.Type == "villain")
            {
                villainCharacter = villainMatch;
            }

            var errors = new Dictionary<string, string>();
            if (heroCharacter is null)
            {
                errors[nameof(hero)] = "Hero is required";
            }
            if (villainCharacter is null)
            {
                errors[nameof(villain)] = "Villain is required";
            }

            if (errors.Count > 0)
                return BattleResult.Invalid(errors);

            var heroScore = heroCharacter.Score;
            if (string.Equals(heroCharacter.Weakness, villainCharacter.Name, StringComparison.InvariantCultureIgnoreCase))
            {
                heroScore--;
            }

            return BattleResult.Won(heroScore > villainCharacter.Score ? heroCharacter : villainCharacter);
        }
    }
}
