using Microsoft.OpenApi.Models;

namespace OpenAPITools
{
    public class OperationsCollection
    {
        private readonly Dictionary<string, OperationWrapper> dict;

        public IEnumerable<string> Keys => dict.Keys;

        public OperationsCollection(Dictionary<string, OperationWrapper> dict)
        {
            this.dict = dict;
        }

        public SchemaFromPathInfo GetByPath(string targetPath) => GetByPath(targetPath.Split('.'));

        public SchemaFromPathInfo GetByPath(IEnumerable<string> targetPath)
        {
            var opId = targetPath.First();
            var op = dict[opId];
            var (s, r) = op.GetByPath(targetPath.Skip(1));
            return new SchemaFromPathInfo(s, r, op, opId);
        }

        public record SchemaFromPathInfo(OpenApiSchema Schema, List<OpenApiReference?> References, OperationWrapper Operation, string OperationId);
    }
}
