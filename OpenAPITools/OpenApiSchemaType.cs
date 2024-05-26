namespace OpenAPITools
{
    public enum OpenApiSchemaType
    {
        Object,
        Array,
        String,
        Number,
        Integer,
        Boolean,
        Reference
    }

    public static class OpenApiSchemaTypeEx
    {
        public static string AsString(this OpenApiSchemaType type) => type.ToString().ToLower();
        public static bool IsPrimitive(this OpenApiSchemaType type) => type != OpenApiSchemaType.Object && type != OpenApiSchemaType.Array && type != OpenApiSchemaType.Reference;
        public static bool IsPrimitive(string type) => Parse(type).IsPrimitive();
        public static OpenApiSchemaType? TryParse(string name) => Enum.TryParse<OpenApiSchemaType>(name, true, out var p) ? p : null;

        public static OpenApiSchemaType Parse(string name)
        {
            var v = TryParse(name);
            if (v == null)
                throw new Exception($"Not a valid schema type: {name}");
            return v.Value;
        }
    }
}
