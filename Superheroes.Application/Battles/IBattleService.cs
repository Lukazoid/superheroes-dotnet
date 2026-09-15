namespace Superheroes.Application.Battles;

public interface IBattleService
{
    Task<BattleResult> Battle(string? hero, string? villain);
}
