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

            CharacterResponse? heroCharacter = null;
            CharacterResponse? villainCharacter = null;
            foreach (var character in characters.Items)
            {
                if (string.Equals(character.Name, hero, StringComparison.InvariantCultureIgnoreCase) && character.Type == "hero")
                {
                    heroCharacter = character;
                }
                if (string.Equals(character.Name, villain, StringComparison.InvariantCultureIgnoreCase) && character.Type == "villain")
                {
                    villainCharacter = character;
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
