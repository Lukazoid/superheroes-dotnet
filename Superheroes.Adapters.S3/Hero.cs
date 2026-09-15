namespace Superheroes.Adapters.S3
{
    public sealed record Hero(string Name, double Score, string? Weakness) : Character(Name, Score);
}
