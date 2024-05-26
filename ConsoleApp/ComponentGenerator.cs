using Microsoft.OpenApi.Models;
using OpenAPITools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleApp
{
    internal class ComponentGenerator
    {
        public static (OpenApiSchema, List<OpenApiSchema>) CreateSchema(Type type)
        {
            var subSchemas = new List<OpenApiSchema>();
            var rootSchema = new OpenApiSchema();
            foreach (var p in type.GetProperties())
            {
                var schema = new OpenApiSchema();

                OpenApiSchemaType schemaType;
                if (p.PropertyType == typeof(string))
                    schemaType = OpenApiSchemaType.String;
                else if (p.PropertyType == typeof(int) || p.PropertyType == typeof(decimal))
                    schemaType = OpenApiSchemaType.Number;
                else if (p.PropertyType == typeof(bool))
                    schemaType = OpenApiSchemaType.Boolean;
                else
                {
                    throw new NotImplementedException();
                    //schemaType = OpenApiSchemaType.Reference;
                }
                schema.Type = schemaType.AsString();
                rootSchema.Properties.Add(p.Name, schema);
            }
            return (rootSchema, subSchemas);
        }
    }
}
