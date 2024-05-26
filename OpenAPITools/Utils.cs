using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using System.Data;
using System.Text.Json.Nodes;

namespace OpenAPITools
{
    public class Utils
    {
        public static OpenApiDocument LoadDocument(string data, OpenApiReaderSettings? settings = null)
        {
            var reader = new OpenApiStringReader(settings ?? new OpenApiReaderSettings { });
            var doc = reader.Read(data, out var diag);
            if (diag.Errors.Any())
                throw new Exception($"Errors in OpenAPI spec");

            if (doc == null)
                throw new Exception($"Could not load OpenAPI spec");
            return doc;
        }

        public static void RecurseOverSchemas(OpenApiSchema schema, Func<List<KeyValuePair<string, OpenApiSchema>>, bool> action)
        {
            Rec(schema, new Stack<KeyValuePair<string, OpenApiSchema>>());

            void Rec(OpenApiSchema schema, Stack<KeyValuePair<string, OpenApiSchema>> parents)
            {
                switch (schema.TypeTyped())
                {
                    case OpenApiSchemaType.Reference:
                        if (schema.Reference.Type == ReferenceType.Schema)
                        {
                            var resolved = schema.Reference.HostDocument.ResolveReference(schema.Reference);
                            if (resolved is OpenApiSchema rSchema)
                                Rec(rSchema, parents);
                            else
                                throw new NotImplementedException();
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                        break;

                    case OpenApiSchemaType.Object:
                        foreach (var item in schema.Properties)
                        {
                            parents.Push(item);
                            var cont = action(parents.Reverse().ToList());
                            if (cont)
                                Rec(item.Value, parents);
                            parents.Pop();
                        }
                        break;

                    case OpenApiSchemaType.Array:
                        Rec(schema.Items, parents);
                        break;

                    default:
                        if (OpenApiSchemaTypeEx.IsPrimitive(schema.Type))
                        {
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                        break;
                }
            }
        }

        public static JsonNode CreateExample(OpenApiSchema schema, OpenApiDocument doc)
        {
            var rnd = new Random();
            var root = Rec(schema, new JsonObject());

            JsonNode Rec(OpenApiSchema schema, JsonNode? parent)
            {
                JsonNode node;
                switch (schema.TypeTyped())
                {
                    case OpenApiSchemaType.Array:
                        var jarr = new JsonArray();
                        node = jarr;
                        foreach (var i in Enumerable.Range(0, Math.Max(schema.MinItems ?? 0, 1)))
                        {
                            jarr.Add(Rec(schema.Items, null));
                        }
                        break;
                    case OpenApiSchemaType.Reference:
                        if (schema.Reference == null)
                            throw new NotImplementedException();
                        //var jobjRef = new JsonObject();
                        //node = jobjRef;
                        parent ??= new JsonObject();
                        //node = parent;
                        var refObj = doc.ResolveReference(schema.Reference);
                        if (refObj is OpenApiSchema refSchema)
                            node = Rec(refSchema, parent);
                        //((JsonObject)parent).Add(schema.Reference.Id, Rec(refSchema, parent));
                        else
                            throw new NotImplementedException();
                        break;
                    case OpenApiSchemaType.Object:
                        if (schema.OneOf?.Any() == true)
                        {
                            return Rec(schema.OneOf.Last(), parent);
                        }
                        if (schema.AnyOf?.Any() == true)
                        {
                            // TODO: !
                        }

                        var jobj = new JsonObject();
                        node = jobj;

                        foreach (var item in schema.Properties)
                        {
                            jobj.Add(item.Key, Rec(item.Value, node));
                        }
                        break;

                    default:
                        node = CreatePrimitive(schema);
                        break;
                }
                return node;
            }
            return root;

            JsonValue CreatePrimitive(OpenApiSchema s)
            {
                return s.TypeTyped() switch
                {
                    OpenApiSchemaType.Boolean =>
                        JsonValue.Create(rnd.Next(2) == 1),
                    OpenApiSchemaType.String =>
                        JsonValue.Create(
                            (s.Default as OpenApiString)?.Value
                            ?? (s.Example as OpenApiString)?.Value
                            ?? (s.Format == "uri" ? "https://a.b" : (RandomString(s.MinLength ?? 5, Math.Min(s.MaxLength ?? int.MaxValue, 100))))
                            ),
                    OpenApiSchemaType.Integer =>
                        JsonValue.Create(rnd.Next((int)(s.Minimum ?? 0), (int)(s.Maximum ?? int.MaxValue))),
                    OpenApiSchemaType.Number =>
                        JsonValue.Create(RandomRange(s.Minimum ?? 0M, s.Maximum ?? decimal.MaxValue)),
                    _ => throw new NotImplementedException(s.Type),
                };

                string RandomString(int minLength, int maxLength) => string.Join("", Enumerable.Range(0, rnd.Next(minLength, maxLength)).Select(o => (char)rnd.Next(65, 85)));
                decimal RandomRange(decimal min, decimal max) => (decimal)(rnd.NextSingle() * (float)(max - min)) + min;
            }
        }
    }

    public record NodeActionArgs(JsonNode Node, OpenApiSchema Schema, string OperationId, IEnumerable<string> TargetPath, OpenApiSchema? ParentSchema);
}
