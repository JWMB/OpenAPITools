using Microsoft.OpenApi.Models;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OpenAPITools
{
    public class Validator
    {
        public record ValidationError(string Message, string Path, OpenApiSchema schema);
        public static IEnumerable<ValidationError> ValidateDto(OpenApiSchema schema, JsonNode? dto, JsonNode parent)
        {
            if (dto == null)
            {
                if (!schema.Nullable)
                    yield return new ValidationError(nameof(schema.Nullable), $"{parent.GetPath()}.?", schema);
                yield break;
            }

            switch (schema.TypeTyped())
            {
                case OpenApiSchemaType.Object:
                    var jobj = dto as JsonObject;
                    if (jobj == null)
                    {
                        yield return new ValidationError("Unexpected JSON node", dto.GetPath(), schema);
                        yield break;
                    }
                    var keys = jobj.Select(o => o.Key).ToList(); // Unbelievable... JsonNode/JsonObject not differentiating between missing property and null-value https://github.com/dotnet/runtime/issues/66948 

                    if (schema.OneOf?.Any() == true)
                    {
                        var discriminatorKey = schema.Discriminator?.PropertyName;
                        if (discriminatorKey == null)
                        {
                            // TODO: would this be error in the OAS?
                        }
                        else
                        {
                            if (!keys.Contains(discriminatorKey))
                            {
                                yield return new ValidationError($"Missing {nameof(schema.Discriminator)}", dto.GetPath(), schema);
                            }
                            else
                            {
                                var discValue = jobj[discriminatorKey]?.GetValue<string>();
                                if (discValue == null)
                                {
                                    yield return new ValidationError($"Null value {nameof(schema.Discriminator)}", dto.GetPath(), schema);
                                }
                                else
                                {
                                    var selectedSchema = schema.OneOf.SingleOrDefault(o => o.Properties[discriminatorKey].Default.AsString() == discValue);
                                    if (selectedSchema == null)
                                    {
                                        yield return new ValidationError($"Schema missing for {nameof(schema.Discriminator)}={discValue}", dto.GetPath(), schema);
                                    }
                                    else
                                    {
                                        foreach (var err in ValidateDto(selectedSchema, jobj, parent))
                                            yield return err;
                                    }
                                }
                            }
                        }
                    }
                    else if (schema.AnyOf?.Any() == true)
                    { }
                    else
                    {
                        var propsInInputWithoutSpec = keys.Where(o => schema.Properties.ContainsKey(o) == false);
                        if (propsInInputWithoutSpec.Any())
                        {
                            foreach (var item in propsInInputWithoutSpec)
                            {
                                yield return new ValidationError($"Unexpected property '{item}'", dto.GetPath(), schema);
                            }
                        }

                        foreach (var prop in schema.Properties)
                        {
                            if (keys.Contains(prop.Key) == false)
                            {
                                if (schema.Required.Contains(prop.Key))
                                {
                                    yield return new ValidationError(nameof(schema.Required), dto.GetPath(), schema);
                                    yield break;
                                }
                                continue;
                            }
                            var val = dto[prop.Key];
                            foreach (var err in ValidateDto(prop.Value, val, dto))
                                yield return err;
                        }
                    }
                    break;

                case OpenApiSchemaType.Reference:
                    if (schema.Reference == null)
                    {
                        yield return new ValidationError("Reference", dto.GetPath(), schema);
                        yield break;
                    }
                    var resolved = schema.Reference.HostDocument.ResolveReference(schema.Reference);
                    if (resolved is not OpenApiSchema s)
                        throw new NotImplementedException();

                    foreach (var err in ValidateDto(s, dto, parent))
                        yield return err;
                    break;

                case OpenApiSchemaType.Array:
                    var arr = (JsonArray)dto;
                    if (schema.MinItems != null && arr.Count < schema.MinItems.Value)
                        yield return new ValidationError(nameof(schema.MinItems), dto.GetPath(), schema);
                    if (schema.MaxItems != null && arr.Count > schema.MaxItems.Value)
                        yield return new ValidationError(nameof(schema.MaxItems), dto.GetPath(), schema);
                    foreach (var item in arr)
                    {
                        foreach (var err in ValidateDto(schema.Items, item, dto))
                            yield return err;
                    }
                    break;

                default:
                    var type = schema.TypeTyped();
                    if (type.IsPrimitive())
                    {
                        var valNode = dto as JsonValue;
                        if (valNode == null)
                        {
                            yield return new ValidationError("Not a JsonValue", dto.GetPath(), schema);
                        }
                        else
                        {
                            var kind = valNode.GetValueKind();
                            var hasTypeError = type switch
                            {
                                OpenApiSchemaType.Boolean =>
                                    kind != System.Text.Json.JsonValueKind.True && kind != System.Text.Json.JsonValueKind.False,
                                OpenApiSchemaType.Number =>
                                    kind != System.Text.Json.JsonValueKind.Number,
                                OpenApiSchemaType.Integer =>
                                    kind != System.Text.Json.JsonValueKind.Number,
                                OpenApiSchemaType.String =>
                                    kind != System.Text.Json.JsonValueKind.String,
                                _ => false
                            };
                            if (hasTypeError)
                            {
                                yield return new ValidationError($"Incorrect type: {kind} is not {type}", dto.GetPath(), schema);
                            }
                            else
                            {
                                switch (type)
                                {
                                    case OpenApiSchemaType.Boolean:
                                        break;
                                    case OpenApiSchemaType.Number:
                                    case OpenApiSchemaType.Integer:
                                        var val = type == OpenApiSchemaType.Integer ? valNode.GetValue<int>() : valNode.GetValue<decimal>();
                                        if (schema.Minimum.HasValue && val < schema.Minimum.Value)
                                            yield return new ValidationError(nameof(schema.Minimum), dto.GetPath(), schema);
                                        if (schema.Maximum.HasValue)
                                        {
                                            if (val > schema.Maximum.Value)
                                                yield return new ValidationError(nameof(schema.Maximum), dto.GetPath(), schema);
                                            else if (schema.ExclusiveMaximum.HasValue && val >= schema.Maximum.Value)
                                                yield return new ValidationError(nameof(schema.ExclusiveMaximum), dto.GetPath(), schema);
                                        }
                                        break;

                                    case OpenApiSchemaType.String:
                                        var str = valNode.GetValue<string>();
                                        if (schema.MinLength.HasValue && str.Length < schema.MinLength.Value)
                                            yield return new ValidationError(nameof(schema.MinLength), dto.GetPath(), schema);
                                        if (schema.MaxLength.HasValue && str.Length > schema.MaxLength.Value)
                                            yield return new ValidationError(nameof(schema.MaxLength), dto.GetPath(), schema);
                                        if (schema.Pattern?.Any() == true)
                                        {
                                            var match = Regex.Match(str, schema.Pattern);
                                            if (!match.Success || match.Index != 0 || match.Length != str.Length)
                                                yield return new ValidationError(nameof(schema.Pattern), dto.GetPath(), schema);
                                        }
                                        break;

                                    default:
                                        throw new NotImplementedException();
                                }
                            }
                        }
                    }
                    break;
            }
        }
    }
}
