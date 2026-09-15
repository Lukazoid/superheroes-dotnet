using System.Text.Json.Serialization;

namespace Superheroes.Adapters.S3;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(Hero), "hero")]
[JsonDerivedType(typeof(Villain), "villain")]
public abstract record Character(string Name, double Score);
