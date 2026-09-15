namespace Superheroes.Adapters.S3;

public sealed record Villain(string Name, double Score) : Character(Name, Score);
