using Microsoft.OpenApi.Models;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Diagnostics.CodeAnalysis;

namespace OpenAPITools
{
    public interface IDocumentSource : IEquatable<IDocumentSource>, IEqualityComparer<IDocumentSource>
    {
        Task<string> Load();
    }

    public record UriDocumentSource(Uri Uri) : IDocumentSource
    {
        public bool Equals(IDocumentSource? other) =>
            other == null ? false
                : (other is UriDocumentSource ud ? ud.Uri == Uri  : false);
        public bool Equals(IDocumentSource? x, IDocumentSource? y) => x == null ? (y == null ? true : y.Equals(x)) : x.Equals(y);
        public int GetHashCode([DisallowNull] IDocumentSource obj) => $"{nameof(UriDocumentSource)}_{Uri}".GetHashCode();

        public async Task<string> Load()
        {
            return Uri.Scheme.ToLower() == "file" ? await File.ReadAllTextAsync(Uri.LocalPath.TrimStart('\\')) : await DownloadString(Uri);
            async Task<string> DownloadString(Uri url)
            {
                var client = new HttpClient();
                var rm = new HttpRequestMessage(HttpMethod.Get, url.AbsoluteUri);
                var response = await client.SendAsync(rm);
                return await response.Content.ReadAsStringAsync();
            }
        }
        public override string ToString() => Uri.AbsoluteUri;
    }

    public record StringDocumentSource(string Data) : IDocumentSource
    {
        public bool Equals(IDocumentSource? other) =>
            other == null ? false
                : (other is StringDocumentSource ud ? ud.Data == Data : false);
        public bool Equals(IDocumentSource? x, IDocumentSource? y) => x == null ? (y == null ? true : y.Equals(x)) : x.Equals(y);
        public int GetHashCode([DisallowNull] IDocumentSource obj) => $"{nameof(StringDocumentSource)}_{Data}".GetHashCode();

        public Task<string> Load() => Task.FromResult(Data);
        public override string ToString() => Data;
    }

    public class DocumentSourceJsonConverter : JsonConverter<IDocumentSource>
    {
        public override IDocumentSource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var val = reader.GetString();
            if (val == null)
                throw new ArgumentNullException(nameof(val));
            if (val == string.Empty)
                throw new ArgumentException(nameof(val));

            if (Uri.TryCreate(val, UriKind.Absolute, out var uri))
                return new UriDocumentSource(uri);
            return new StringDocumentSource(val);
        }

        public override void Write(Utf8JsonWriter writer, IDocumentSource value, JsonSerializerOptions options) =>
                writer.WriteStringValue($"{value}");
    }

    public record OperationDirective(string Id, IDocumentSource Source, string Operation)
    {
        public OperationPath Path => new OperationPath { OperationType = Enum.Parse<OperationType>(Operation.Split(' ').First(), true), Path = Operation.Split(' ').Last() };
        //public List<Dependency> Dependencies { get; set; } = new();
    }

    //public class Dependency
    //{
    //    public OperationDirective Directive { get; init; }
    //    public string? ResponseValuePath { get; set; }
    //    // TODO:
    //    // Conditions, e.g. status codes
    //    // Mappings, e.g. use the response value xxx (optionally transform it somehow) and add to our request parameters or payload
    //}

    public class OperationPath
    {
        public string Path { get; set; } = string.Empty;
        public OperationType OperationType { get; set; }

        public OpenApiOperation? GetOperation(OpenApiDocument document)
        {
            try
            {
                return GetOperationOrThrow(document);
            }
            catch { }
            return null;
        }
        public OpenApiOperation GetOperationOrThrow(OpenApiDocument document)
        {
            var p = document.Paths.SingleOrDefault(o => o.Key == Path);
            if (p.Value == null)
                throw new Exception("Path not found: ");

            var found = p.Value.Operations.SingleOrDefault(o => o.Key == OperationType);
            if (found.Value == null)
                throw new Exception("Operation not found: ");

            return found.Value;
        }
    }

    //public class OperationPropertyPath
    //{
    //    public OperationPath OperationPath { get; set; }
    //}

    //public class CompositionDirective
    //{
    //    //public List<OperationDirective> Sources { get; set; } = new();
    //    public OperationDirective SourceOperation { get; set; }
    //    public string SourcePath { get; set; }
    //    public string? DestinationPath { get; set; }
    //}

    //public class PropertyTransformationDirective
    //{
    //    public string Src { get; set; }
    //    public string? Dst { get; set; }
    //    public string Transform { get; set; }
    //}
}
