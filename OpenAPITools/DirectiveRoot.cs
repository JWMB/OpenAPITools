using Microsoft.OpenApi.Models;
using System.Text.Json.Nodes;

namespace OpenAPITools
{
    public record DirectiveRoot(List<OperationDirective> UpstreamApis, JsonObject Mappings)
    {
        private Dictionary<IDocumentSource, OpenApiDocument>? sourceToDoc;
        public async Task LoadSources()
        {
            var allSources = UpstreamApis.Select(o => o.Source).Distinct().ToList(); // By(o => o.ToString())

            var tasks = allSources
                .Select(o => new { Source = o, Data = o.Load() });
            //.Select(src => new { Source = src, Data = src.Scheme == "file" ? File.ReadAllTextAsync(src.LocalPath.TrimStart('\\')) : DownloadString(src) });
            await Task.WhenAll(tasks.Select(o => o.Data));

            sourceToDoc = tasks.ToDictionary(o => o.Source, o => Utils.LoadDocument(o.Data.Result));

            //async Task<string> DownloadString(Uri url)
            //{
            //    var client = new HttpClient();
            //    var rm = new HttpRequestMessage(HttpMethod.Get, url.AbsoluteUri);
            //    var response = await client.SendAsync(rm);
            //    return await response.Content.ReadAsStringAsync();
            //}
        }

        public OperationsCollection OperationsById =>
            sourceToDoc == null 
            ? throw new ArgumentNullException(nameof(sourceToDoc))
            : new OperationsCollection(UpstreamApis.ToDictionary(dir => dir.Id, dir => new OperationWrapper(dir, sourceToDoc[dir.Source])));
    }
}
