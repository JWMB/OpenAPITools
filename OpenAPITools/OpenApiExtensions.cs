using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Writers;

namespace OpenAPITools
{
    public static class OpenApiExtensions
    {
        public static IEnumerable<OpenApiReference> RecurseGetReferences(this OpenApiSchema schema)
        {
            var reference = schema.Reference;
            yield return reference;

            foreach (var prop in schema.Properties.Values)
            {
                if (prop.Reference != null)
                {
                    var resolved = reference.HostDocument.ResolveReference(prop.Reference);
                    if (resolved is OpenApiSchema rSchema)
                    {
                        //yield return prop.Reference;
                        foreach (var r in rSchema.RecurseGetReferences())
                            yield return r;
                    }
                }
            }
        }

        public static string? AsString(this IOpenApiAny any)
        {
            return any switch
            {
                OpenApiString s => s.Value,
                OpenApiNull _ => null,
                OpenApiLong l => l.Value.ToString(),
                OpenApiInteger i => i.Value.ToString(),
                OpenApiFloat f => f.Value.ToString(),
                OpenApiDouble d => d.Value.ToString(),
                OpenApiDate dt => dt.Value.ToString(),
                OpenApiDateTime dt => dt.Value.ToString(),
                _ => any.ToString()
            };
        }

        public static OpenApiSchemaType TypeTyped(this OpenApiSchema schema)
        {
            return OpenApiSchemaTypeEx.Parse(schema.Type);
        }

        public static string Serialize(this OpenApiDocument document)
        {
            using var sw = new StringWriter(System.Globalization.CultureInfo.InvariantCulture);
            var oaw = new OpenApiYamlWriter(sw); // OpenApiJsonWriter
            document.SerializeAsV3(oaw);
            return sw.GetStringBuilder().ToString();
        }
    }
}
