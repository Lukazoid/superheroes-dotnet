using System;

namespace Superheroes
{
    public class CharactersCacheOptions
    {
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(5);
    }
}
