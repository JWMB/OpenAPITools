namespace OpenAPITools
{
    public class JsonPath
    {
        public JsonPath(IEnumerable<string> segments, bool isAbsolute = true)
        {
            Segments = segments.ToArray();
            IsAbsolute = isAbsolute;
        }

        public JsonPath(string path)
        {
            var split = path.Split('.');
            if (split.First() == "$")
            {
                IsAbsolute = true;
                Segments = split.Skip(1).ToArray();
            }
            else
            {
                IsAbsolute = false;
                Segments = split.ToArray();
                throw new NotImplementedException("Haven't checked what this means");
            }
        }
        public string[] Segments { get; private set; }
        public JsonPath GetParentPath(int stepsUp = 1) => new JsonPath($"$.{string.Join(".", Segments.SkipLast(stepsUp))}");
        public bool IsAbsolute { get; private set; }
    }
}
