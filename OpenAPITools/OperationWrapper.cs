using Microsoft.OpenApi.Interfaces;
using Microsoft.OpenApi.Models;

namespace OpenAPITools
{
    public class OperationWrapper
    {
        private readonly OperationDirective directive;
        private readonly OpenApiDocument document;
        private readonly string defaultBodyMediaType;

        private readonly OpenApiOperation operation;

        public OperationWrapper(OperationDirective directive, OpenApiDocument document, string defaultBodyMediaType = "application/json")
        {
            this.directive = directive;
            this.document = document;
            this.defaultBodyMediaType = defaultBodyMediaType;

            operation = directive.Path.GetOperationOrThrow(document);
        }

        public OpenApiMediaType MediaType => operation.RequestBody.Content[defaultBodyMediaType];
        public OpenApiOperation Operation => operation;

        public (OpenApiSchema schema, List<OpenApiReference?> references) GetByPath(IEnumerable<string> path)
        {
            var references = new List<OpenApiReference?>();
            var schema = MediaType.Schema;
            foreach (var item in path)
            {
                if (!schema.Properties.TryGetValue(item, out var p))
                    throw new Exception($"Incorrect path segment: {item} ({string.Join(".", path)})");
                references.Add(schema.Reference);
                schema = p;
            }
            return (schema, references);
        }

        public IOpenApiReferenceable ResolveReference(OpenApiReference reference)
        {
            return document.ResolveReference(reference);
        }

        public List<OpenApiReference>? GetAllReferences()
        {
            //if (MediaType.Schema?.Reference.ReferenceV3?.StartsWith("#") == true)
            var refs = MediaType.Schema == null
                ? null
                : MediaType.Schema.RecurseGetReferences().GroupBy(o => o.ReferenceV3).Select(o => o.First()).ToList();

            return refs;
        }

        public Dictionary<string, object> GetPropertyStructure()
        {
            return Rec(MediaType.Schema);

            Dictionary<string, object> Rec(OpenApiSchema s)
            {
                var result = new Dictionary<string, object>();
                foreach (var prop in s.Properties)
                {
                    var value = Rec(prop.Value);
                    result.Add(prop.Key, value.Any() ? value : prop.Value.Type);
                }
                return result;
            }
        }

        public static List<OpenApiSchema> GetSchemasUsed(IEnumerable<OperationWrapper> operations)
        {
            var refs = operations.Select(o => o.GetAllReferences());

            var byDocument = refs.Select(o => new { Doc = o?.FirstOrDefault()?.HostDocument, Refs = o })
                .Where(o => o.Doc != null)
                .GroupBy(o => o.Doc!.HashCode)
                .ToDictionary(o => o.Key, o => o.SelectMany(p => p.Refs ?? new List<OpenApiReference>()).DistinctBy(p => p.ReferenceV3));

            var nameDuplicates = byDocument.SelectMany(o => o.Value ?? new List<OpenApiReference>()).GroupBy(o => o.ReferenceV3).Where(o => o.Count() > 1).ToList();
            if (nameDuplicates.Any())
            {
                throw new NotImplementedException("No support for renaming identical reference names");
            }

            var refSchemas = byDocument.SelectMany(o => o.Value ?? new List<OpenApiReference>()).ToList();
            var schemasToAdd = refSchemas.Select(o => o.HostDocument.ResolveReference(o) as OpenApiSchema).OfType<OpenApiSchema>().ToList();
            return schemasToAdd;
        }
    }
}
