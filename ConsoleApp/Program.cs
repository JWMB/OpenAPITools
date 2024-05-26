using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using OpenAPITools;
using System.Text.Json;
using System.Text.Json.Nodes;

var config = JsonSerializer.Deserialize<DirectiveRoot>(File.ReadAllText("Resources/config.json"), new JsonSerializerOptions { PropertyNameCaseInsensitive = true, Converters = { new DocumentSourceJsonConverter() } });
if (config == null)
    throw new Exception("Could not parse");

//var dbg = JsonSerializer.Serialize(operationsById["PB"].GetPropertyStructure(), new JsonSerializerOptions { WriteIndented = true });

await config.LoadSources();

var doc = OpenApiSplitter.MergeSchemas(config.OperationsById, config.Mappings);

doc.Info = new OpenApiInfo { Title = "Merged", Version = "0.0.0" };

var mediaType = doc.Paths.Single().Value.Operations.Single().Value.RequestBody.Content.Single().Value;
var schema = mediaType.Schema;

//Utils.RecurseOverSchemas(schema, vals => { Console.WriteLine($"{string.Join("", Enumerable.Range(0, vals.Count).Select(o => ' '))}{vals.Last().Key}"); return true; });

var op = new OpenApiOperation
{
    Summary = "Combines endpoints",
    //Parameters = new List<OpenApiParameter> { }.Concat(p1.Parameters).ToList(),
    RequestBody = new OpenApiRequestBody { Content = new Dictionary<string, OpenApiMediaType> { { "application/json", new OpenApiMediaType {  Schema = schema } } } },
    Responses = new OpenApiResponses { { "200", new OpenApiResponse { } } },
};
var pathItem = new OpenApiPathItem { Operations = new Dictionary<OperationType, OpenApiOperation> { { OperationType.Post, op } } };
doc.Paths = new OpenApiPaths { { "/combo/endpoint1", pathItem } };

doc.Components.RequestBodies = new Dictionary<string, OpenApiRequestBody>();

var output = doc.Serialize();

var tmpExample = Utils.CreateExample(schema, doc);
if (tmpExample == null)
    throw new Exception("Could not create example");
//var tmpstr = example.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
var example = JsonNode.Parse(File.ReadAllText("Resources/validate.json"));
if (example == null)
    throw new Exception("not parsed");
var errors = Validator.ValidateDto(schema, example, example).ToList();

var split = OpenApiSplitter.SplitPayload(config.OperationsById, config.Mappings, example);

Console.WriteLine("OK");
