using System.Text.Json.Nodes;

namespace OpenAPITools
{
    public static class JsonNodeExtensions
    {
        public static JsonPath GetPathInfo(this JsonNode node) => new JsonPath(node.GetPath());

        public static void Add(this JsonObject obj, JsonPath path, JsonNode value)
        {
            foreach (var item in path.Segments.SkipLast(1)) 
            {
                if (!obj.TryGetPropertyValue(item, out var v))
                {
                    v = new JsonObject();
                    obj.Add(item, v);
                }
                obj = (JsonObject)v!;
            }
            obj[path.Segments.Last()] = value;
        }
    }
}
