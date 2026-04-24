internal interface IPostLayoutPass
{
    string Name { get; }

    LayoutResult Run(LayoutGraph graph, LayoutResult result, LayoutOptions options);
}
