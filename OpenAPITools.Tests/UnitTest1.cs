using Shouldly;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenAPITools.Tests
{
    public class UnitTest1
    {
        [Fact]
        public async Task Test1()
        {
            var xDoc = """
                {}
                """;
            var root = new DirectiveRoot(new List<OperationDirective> {
                new OperationDirective("X", new StringDocumentSource(xDoc), "POST")
            }, (JsonObject)JsonObject.Parse("""
                {
                    "property": "X"
                }
                """)!);

            await root.LoadSources();
        }

        // TODO: test PropertyDirective
        [Fact] public void Test2()
        {
            new PropertyDirective("PB", Filter: new("(maxTotalDocumentTokens|maxNumDocuments|documentRetrieval.documentRetention)", true))
            {

            };
        }

        [Fact]
        public void IDocumentSource_Deserialize()
        {
            var serializeOptions = new JsonSerializerOptions { Converters = { new DocumentSourceJsonConverter() } };
            var deserialized = JsonSerializer.Deserialize<List<IDocumentSource>>("""[ "https://abc.com", "file://123.json", "{ \"a\": 1 }" ]""", serializeOptions);

            deserialized!.Select(o => o.GetType()).ShouldBe([typeof(UriDocumentSource), typeof(UriDocumentSource), typeof(StringDocumentSource)]);
        }
    }
}