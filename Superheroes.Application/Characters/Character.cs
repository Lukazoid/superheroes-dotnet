namespace Superheroes.Application.Characters;

/// <summary>
/// A superhero-universe character, as understood by the domain - a name, a battle score, and
/// a role (Hero/Villain) that decides which business rules apply. Deliberately carries no
/// serialization concerns: adapters are responsible for mapping to/from their own wire
/// formats (see Superheroes.Adapters.S3's records for the S3 feed, and Superheroes'
/// Responses/ for the API contract).
/// </summary>
public abstract record Character(string Name, double Score);
