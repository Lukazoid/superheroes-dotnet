namespace Superheroes.Application.Characters;

public sealed record Villain(string Name, double Score) : Character(Name, Score);
