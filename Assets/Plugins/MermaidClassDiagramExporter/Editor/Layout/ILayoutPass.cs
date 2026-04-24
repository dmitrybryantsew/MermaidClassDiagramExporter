internal interface ILayoutPass
{
    string Name { get; }

    LayoutGraph Run(LayoutGraph graph, LayoutOptions options);
}
