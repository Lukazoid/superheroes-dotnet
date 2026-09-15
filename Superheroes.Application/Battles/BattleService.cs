using Superheroes.Application.Characters;
using Superheroes.Application.Ports;

namespace Superheroes.Application.Battles
{
    public class BattleService : IBattleService
    {
        private readonly ICharacterLoader _characterLoader;

        public BattleService(ICharacterLoader characterLoader)
        {
            _characterLoader = characterLoader;
        }

        public async Task<BattleResult> Battle(string? hero, string? villain)
        {
            var catalogue = await _characterLoader.GetCharacters();

            // The catalogue's dictionaries are already keyed case-insensitively (see
            // CharacterCatalogue.Create), and each is already scoped to one role, so a name that
            // matches the wrong role (e.g. a hero's name passed as the villain) simply misses.
            Hero? heroCharacter = hero != null && catalogue.Heroes.TryGetValue(hero, out var h) ? h : null;
            Villain? villainCharacter = villain != null && catalogue.Villains.TryGetValue(villain, out var v) ? v : null;

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

            var heroScore = heroCharacter!.Score;
            if (string.Equals(heroCharacter.Weakness, villainCharacter!.Name, StringComparison.InvariantCultureIgnoreCase))
            {
                heroScore--;
            }

            return BattleResult.Won(heroScore > villainCharacter.Score ? heroCharacter : villainCharacter);
        }
    }
}
