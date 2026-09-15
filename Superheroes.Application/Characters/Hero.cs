namespace Superheroes.Application.Characters;

public sealed record Hero(string Name, double Score, string? Weakness) : Character(Name, Score);
