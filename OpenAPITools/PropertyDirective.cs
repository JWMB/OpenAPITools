using Microsoft.OpenApi.Models;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OpenAPITools
{
    public record PropertyDirective(string Target, FilterDirective? Filter = null)
    {
        public bool IsMatch(IEnumerable<string> path)
        {
            if (Filter == null)
                return true;
            return IsMatch(string.Join(".", path));
        }
        public bool IsMatch(string path)
        {
            if (Filter == null)
                return true;
            // TODO: switch for matching against full path or just last part?
            var isMatch = Regex.IsMatch(path, Filter.Rx);
            return isMatch != Filter.Exclude;
        }

        public JsonObject ExpandProperties(OperationsCollection operations)
        {
            var root = new JsonObject();
            var targetInfo = operations.GetByPath(Target);
            Utils.RecurseOverSchemas(targetInfo.Schema, (vals) => {
                if (IsMatch(vals.Select(o => o.Key)))
                {
                    var lastSchema = vals.Last().Value;
                    if (lastSchema.TypeTyped().IsPrimitive() || lastSchema.TypeTyped() == OpenApiSchemaType.Array)
                    {
                        var path = new JsonPath(vals.Select(o => o.Key));
                        root.Add(new JsonPath(vals.Select(o => o.Key)), JsonValue.Create($"{Target}.{string.Join(".", vals.Select(o => o.Key))}"));
                    }
                    else
                    {

                    }
                    return true;
                }
                else
                {
                    return false;
                }
            });
            return root;
        }

        [Obsolete]
        public OpenApiSchema CreateSchema(OperationsCollection operations)
        {
            var targetInfo = operations.GetByPath(Target);

            Console.WriteLine($"{Target}, filter:{Filter?.Rx} {(Filter?.Exclude == true ? "Exclude" : "")}");

            var root = new OpenApiSchema { Type = OpenApiSchemaType.Object.AsString() };
            
            Utils.RecurseOverSchemas(targetInfo.Schema, (vals) => {
                var item = vals.Last();

                if (IsMatch(vals.Select(o => o.Key)))
                {
                    var sc = root;
                    foreach (var v in vals)
                    {
                        if (!sc.Properties.TryGetValue(v.Key, out var s))
                        {
                            s = new OpenApiSchema { Type = OpenApiSchemaType.Object.AsString() };
                            sc.Properties.Add(v.Key, s);
                        }
                        sc = s;
                    }
                    Console.WriteLine($"Include: {string.Join("", Enumerable.Range(0, vals.Count).Select(o => ' '))}{vals.Last().Key} - {string.Join(".", vals.Select(o => o.Key))}");
                    return true;
                }
                return false;
            });

            return root;
        }
    }

    public record FilterDirective(string Rx, bool Exclude);
}
