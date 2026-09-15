using System.Threading.Tasks;

namespace Superheroes
{
    public interface IBattleService
    {
        Task<BattleResult> Battle(string hero, string villain);
    }
}
