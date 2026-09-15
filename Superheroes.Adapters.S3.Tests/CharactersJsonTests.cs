using System.Text.Json;
using Shouldly;
using Xunit;

namespace Superheroes.Adapters.S3.Tests
{
    /// <summary>
    /// Characterizes S3CharacterLoader's JSON deserialization of the S3 characters feed into
    /// CharactersDocument/Character, so the Newtonsoft.Json -> System.Text.Json swap changes only
    /// the Deserialize helper below and no assertion in this file.
    /// </summary>
    public class CharactersJsonTests
    {
        // The feed puts "type" after "name"/"score", not first as System.Text.Json's
        // polymorphic reader otherwise requires - see S3CharacterLoader for the same options.
        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            AllowOutOfOrderMetadataProperties = true
        };

        private static CharactersDocument Deserialize(string json) =>
            JsonSerializer.Deserialize<CharactersDocument>(json, SerializerOptions);

        [Fact]
        public void RealCharactersFeedDeserializesAllElevenItems()
        {
            var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "characters.json"));

            var result = Deserialize(json);

            result.Items.Length.ShouldBe(11);
        }

        [Fact]
        public void RealCharactersFeedFirstItemIsBatman()
        {
            var json = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "characters.json"));

            var result = Deserialize(json);

            var batman = result.Items[0].ShouldBeOfType<Hero>();
            batman.Name.ShouldBe("Batman");
            batman.Score.ShouldBe(8.3);
            batman.Weakness.ShouldBe("Joker");
        }

        [Fact]
        public void LowercaseKeysBindToPascalCaseProperties()
        {
            // The core trap in a naive Newtonsoft -> System.Text.Json swap: the feed uses
            // lowercase keys but the model properties are PascalCase. Newtonsoft matches these
            // case-insensitively by default; a naive System.Text.Json.Deserialize call does not
            // and would silently leave every property at its default value.
            const string json = """
                {"items":[{"name":"Batman","score":8.3,"type":"hero"}]}
                """;

            var result = Deserialize(json);

            result.Items.ShouldNotBeNull();
            result.Items.Length.ShouldBe(1);
            var batman = result.Items[0].ShouldBeOfType<Hero>();
            batman.Name.ShouldBe("Batman");
            batman.Score.ShouldBe(8.3);
        }

        [Fact]
        public void IntegerScoreBindsToDouble()
        {
            const string json = """
                {"items":[{"name":"Lex Luthor","score":8,"type":"villain"}]}
                """;

            var result = Deserialize(json);

            result.Items[0].Score.ShouldBe(8.0);
        }

        [Fact]
        public void FractionalScoreBindsWithoutPrecisionLoss()
        {
            const string json = """
                {"items":[{"name":"Batman","score":8.3,"type":"hero"}]}
                """;

            var result = Deserialize(json);

            result.Items[0].Score.ShouldBe(8.3);
        }

        [Fact]
        public void WeaknessMemberBindsToWeaknessProperty()
        {
            // "weakness" is lowercase in the feed like every other member, so it needs the
            // same [JsonPropertyName] treatment as name/score/type to bind under
            // System.Text.Json's default case-sensitive matching.
            const string json = """
                {"items":[{"name":"Batman","score":8.3,"type":"hero","weakness":"Joker"}]}
                """;

            var result = Deserialize(json);

            result.Items[0].ShouldBeOfType<Hero>().Weakness.ShouldBe("Joker");
        }

        [Fact]
        public void HeroTypeDeserializesToHero()
        {
            const string json = """
                {"items":[{"name":"Batman","score":8.3,"type":"hero"}]}
                """;

            var result = Deserialize(json);

            result.Items[0].ShouldBeOfType<Hero>();
        }

        [Fact]
        public void VillainTypeDeserializesToVillainWithNoWeaknessProperty()
        {
            // Villain has no Weakness property at all - unlike a hero with no configured
            // weakness (a null Weakness), this isn't representable as null.
            const string json = """
                {"items":[{"name":"Joker","score":8.2,"type":"villain"}]}
                """;

            var result = Deserialize(json);

            result.Items[0].ShouldBeOfType<Villain>();
        }

        [Fact]
        public void UnrecognizedTypeValueThrows()
        {
            // Unlike the old flat model - where an unrecognised "type" was just never matched
            // by BattleController's string comparisons and silently skipped - the polymorphic
            // discriminator rejects it eagerly, while parsing the feed itself.
            const string json = """
                {"items":[{"name":"Mystique","score":7.0,"type":"antihero"}]}
                """;

            Should.Throw<JsonException>(() => Deserialize(json));
        }

        [Fact]
        public void UnknownMemberIsIgnored()
        {
            // "nickname" has no corresponding property on Character. Both serializers ignore
            // unknown members by default; this pins that it does not throw.
            const string json = """
                {"items":[{"name":"Batman","score":8.3,"type":"hero","nickname":"The Dark Knight"}]}
                """;

            var result = Deserialize(json);

            result.Items[0].Name.ShouldBe("Batman");
        }

        [Fact]
        public void AbsentOptionalMemberParsesFine()
        {
            const string json = """
                {"items":[{"name":"Joker","score":8.2,"type":"villain"}]}
                """;

            var result = Deserialize(json);

            result.Items[0].Name.ShouldBe("Joker");
        }

        [Fact]
        public void EmptyItemsArrayDeserializesToEmptyArrayNotNull()
        {
            const string json = """{"items":[]}""";

            var result = Deserialize(json);

            result.Items.ShouldNotBeNull();
            result.Items.ShouldBeEmpty();
        }

        [Fact]
        public void MissingItemsKeyLeavesItemsNull()
        {
            const string json = """{}""";

            var result = Deserialize(json);

            result.Items.ShouldBeNull();
        }

        [Fact]
        public void LiteralJsonNullDeserializesToNull()
        {
            const string json = "null";

            var result = Deserialize(json);

            result.ShouldBeNull();
        }
    }
}
