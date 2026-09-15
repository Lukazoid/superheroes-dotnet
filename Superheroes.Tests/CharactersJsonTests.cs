using System.Text.Json;
using Shouldly;
using Xunit;

namespace Superheroes.Tests
{
    /// <summary>
    /// Characterizes CharactersProvider's JSON deserialization of the S3 characters feed into
    /// CharactersResponse/CharacterResponse, so the Newtonsoft.Json -> System.Text.Json swap
    /// changes only the Deserialize helper below and no assertion in this file.
    /// </summary>
    public class CharactersJsonTests
    {
        private static CharactersResponse Deserialize(string json) =>
            JsonSerializer.Deserialize<CharactersResponse>(json);

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

            var batman = result.Items[0];
            batman.Name.ShouldBe("Batman");
            batman.Score.ShouldBe(8.3);
            batman.Type.ShouldBe("hero");
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
            result.Items[0].Name.ShouldBe("Batman");
            result.Items[0].Score.ShouldBe(8.3);
            result.Items[0].Type.ShouldBe("hero");
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
        public void UnknownMemberIsIgnored()
        {
            // "weakness" has no corresponding property on CharacterResponse. Both serializers
            // ignore unknown members by default; this pins that it does not throw.
            const string json = """
                {"items":[{"name":"Batman","score":8.3,"type":"hero","weakness":"Joker"}]}
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
