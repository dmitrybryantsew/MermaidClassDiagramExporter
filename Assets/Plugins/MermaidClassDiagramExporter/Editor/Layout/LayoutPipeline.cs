using System.Collections.Generic;

internal sealed class LayoutPipeline
{
    private readonly List<ILayoutPass> passes = new List<ILayoutPass>();

    public LayoutPipeline AddPass(ILayoutPass pass)
    {
        if (pass != null)
        {
            passes.Add(pass);
        }

        return this;
    }

    public LayoutGraph Run(LayoutGraph graph, LayoutOptions options)
    {
        LayoutGraph currentGraph = graph ?? new LayoutGraph();
        foreach (ILayoutPass pass in passes)
        {
            currentGraph = pass.Run(currentGraph, options) ?? currentGraph;
        }

        return currentGraph;
    }
}
