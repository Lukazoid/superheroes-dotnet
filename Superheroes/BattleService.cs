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

            HeroResponse? heroCharacter = null;
            VillainResponse? villainCharacter = null;
            foreach (var character in characters.Items)
            {
                if (character is HeroResponse h && string.Equals(h.Name, hero, StringComparison.InvariantCultureIgnoreCase))
                {
                    heroCharacter = h;
                }
                if (character is VillainResponse v && string.Equals(v.Name, villain, StringComparison.InvariantCultureIgnoreCase))
                {
                    villainCharacter = v;
                }

                if (heroCharacter is not null && villainCharacter is not null)
                    break;
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
