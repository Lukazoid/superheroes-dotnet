namespace Superheroes.Application.Caching
{
    public class CharactersCacheOptions
    {
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);
    }
}
