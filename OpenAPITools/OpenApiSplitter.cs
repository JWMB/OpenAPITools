using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenAPITools
{
    public static class OpenApiSplitter
    {
        public static OpenApiDocument MergeSchemas(OperationsCollection operations, JsonObject rootObj) // Dictionary<string, OperationWrapper>
        {
            var schemaInfos = operations.Keys.ToDictionary(o => o, o => new Dictionary<string, OpenApiSchema>());

            // TODO: find and fix name clashes

            Rec(rootObj, ACT2);

            var doc = new OpenApiDocument();

            var roots = schemaInfos
                .Select(o => new { o.Key, Value = o.Value.TryGetValue("root", out var r) ? r : null })
                .Where(o => o.Value != null)
                .ToDictionary(o => o.Key, o => o.Value!);

            var components = schemaInfos.SelectMany(o => o.Value.Select(p => new { Key = GetRefKey(o.Key, p.Key), p.Value })).ToDictionary(o => o.Key, o => o.Value);
            doc.Components = new OpenApiComponents { Schemas = components };

            var rootSchema = roots.Count == 1
                ? new OpenApiSchema
                {
                    Type = OpenApiSchemaType.Reference.AsString(),
                    Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = $"{roots.Single().Key}_root", HostDocument = doc }
                }
                : new OpenApiSchema
                {
                    Type = OpenApiSchemaType.Object.AsString(),
                    Properties = roots.ToDictionary(kv => kv.Key, kv => new OpenApiSchema
                    {
                        Type = OpenApiSchemaType.Reference.AsString(),
                        Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = $"{kv.Key}_root", HostDocument = doc }
                    }) //#/components/schemas/
                };

            var path = "/path";
            doc.Paths = new OpenApiPaths {
                { path, new OpenApiPathItem {
                    Operations = new Dictionary<OperationType, OpenApiOperation> {
                    { OperationType.Post, new OpenApiOperation { RequestBody = new OpenApiRequestBody {
                        Content = new Dictionary<string, OpenApiMediaType>{ { "application/json", new OpenApiMediaType { Schema = rootSchema } } } } } } }
                }}
                };

            // TODO: how to use doc.Validate(new Microsoft.OpenApi.Validations.ValidationRuleSet { ...? }).ToList();

            return doc;

            void Rec(JsonObject obj, Action<NodeActionArgs> action)
            {
                foreach (var propKV in obj)
                {
                    if (propKV.Value is JsonObject child)
                    {
                        if (child["__"] is JsonObject pdir)
                        {
                            var propDirective = pdir.Deserialize<PropertyDirective>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                            if (propDirective == null)
                                throw new Exception($"Could not deserialize {nameof(PropertyDirective)}: {pdir}");
                            var subnode = propDirective.ExpandProperties(operations);

                            var parent = new JsonObject();
                            parent.Add(child.GetPathInfo(), subnode);
                            Rec(parent, action);
                        }
                        else
                        {
                            Rec(child, action);
                        }
                    }
                    else
                    {
                        if (propKV.Value != null && propKV.Value.GetValueKind() == JsonValueKind.String)
                        {
                            var val = propKV.Value.GetValue<string>();
                            if (val.Any())
                            {
                                var parts = val.Split('.').Where(o => o.Any());
                                var targetPath = CreateTargetPath(propKV.Value, parts);

                                var targetInfo = operations.GetByPath(parts.Take(1).Concat(targetPath));

                                OpenApiSchema? parentSchema;
                                {
                                    var r = targetInfo.References.LastOrDefault();
                                    if (r == null)
                                    {
                                        var parentSchemaInfo = targetInfo.Operation.GetByPath(targetPath.SkipLast(1));
                                        parentSchema = parentSchemaInfo.schema;
                                    }
                                    else
                                    {
                                        var resolved = targetInfo.Operation.ResolveReference(r);
                                        parentSchema = resolved as OpenApiSchema;
                                    }
                                }

                                // TODO: the *correct* way is to analyze references when all is done, and see if a structure is used from more than one single place
                                // If not, we should inline the definition instead of creating a component, right?

                                action(new NodeActionArgs(propKV.Value, targetInfo.Schema, targetInfo.OperationId, targetPath, parentSchema));
                            }
                        }
                        else
                        { }
                    }
                }
            }

            void ACT2(NodeActionArgs args) //JsonNode node, OpenApiSchema schema, string opId, IEnumerable<string> targetPath)
            {
                var schemasById = schemaInfos.First().Value;
                var rootId = $"root";
                if (!schemasById.TryGetValue(rootId, out var rootSchema))
                {
                    rootSchema = new OpenApiSchema { Type = OpenApiSchemaType.Object.AsString() };
                    schemasById.Add(rootId, rootSchema);
                }

                var schemax = rootSchema;
                var nodePath = args.Node.GetPathInfo();
                foreach (var item in nodePath.Segments.SkipLast(1))
                {
                    if (!schemax.Properties.TryGetValue(item, out var s))
                    {
                        s = new OpenApiSchema { Type = OpenApiSchemaType.Object.AsString() };
                        schemax.Properties.Add(item, s);
                    }
                    schemax = s;
                }

                // TODO: refactor - might have multiple '!'
                var propName = nodePath.Segments.Last();
                if (propName == "!")
                    propName = args.TargetPath.Last();

                if (args.ParentSchema?.Required.Contains(propName) == true)
                {
                    schemax.Required.Add(propName);
                }

                schemax.Properties.Add(propName, args.Schema);
            }

            //void ACT(NodeActionArgs args)
            //{
            //    var schemasById = schemaInfos[args.OperationId];
            //    OpenApiSchema? targetSchema = null;
            //    foreach (var item in new[] { "root" }.Concat(args.TargetPath).SkipLast(1))
            //    {
            //        if (!schemasById.TryGetValue(item, out var s))
            //        {
            //            s = new OpenApiSchema();
            //            s.Type = OpenApiSchemaType.Object.AsString();
            //            schemasById.Add(item, s);
            //        }
            //        if (targetSchema != null)
            //        {
            //            if (!targetSchema.Properties.ContainsKey(item))
            //            {
            //                targetSchema.Properties.Add(item, new OpenApiSchema
            //                {
            //                    Type = OpenApiSchemaType.Reference.AsString(),
            //                    Reference = new OpenApiReference { Type = ReferenceType.Schema, Id = $"{GetRefKey(args.OperationId, item)}", HostDocument = doc } //#/components/schemas/
            //                });
            //            }
            //        }
            //        targetSchema = s;
            //    }
            //    targetSchema!.Properties.Add(args.TargetPath.Last(), args.Schema);
            //}
        }

        private static List<string> CreateTargetPath(JsonNode node, IEnumerable<string> parts)
        {
            var nodePath = node.GetPathInfo().Segments;

            return parts.Skip(1).Select(part =>
            {
                if (part.Contains("!"))
                {
                    if (part.Distinct().Count() > 1)
                        throw new Exception($"Bad format '{part}' in {parts}");
                    return nodePath.Reverse().Take(part.Count()).Reverse();
                }
                else
                {
                    return new[] { part };
                }
            }).SelectMany(o => o).ToList();
        }

        private static string GetRefKey(string operationKey, string componentKey) => $"{operationKey}_{componentKey}";

        public static JsonNode SplitPayload(OperationsCollection operations, JsonObject mappings, JsonNode payload)
        {
            var destinations = new JsonObject(); // operations.ToDictionary(o => o.Key, o => )

            Rec(mappings, payload);

            return destinations;

            void Rec(JsonObject currentMappings, JsonNode current)
            {
                if (current is JsonArray jarr)
                { }
                else if (current is JsonObject jobj)
                {
                    foreach (var prop in jobj)
                    {
                        var mapping = currentMappings[prop.Key];
                        if (mapping == null)
                        {
                            // resolve mapping
                            var targetPath = CreateTargetPath(currentMappings, ["asd"]);
                            throw new Exception($"No mapping for '{prop.Key}'");
                        }

                        if (prop.Value == null)
                        { }
                        else
                        {
                            if (mapping is JsonObject childMapping)
                            {
                                Rec(childMapping, prop.Value);
                            }
                        }

                        //var path = mapping.GetPath();
                        //var targetPath = "";
                    }
                }
                else
                { }
            }
        }
    }
}
