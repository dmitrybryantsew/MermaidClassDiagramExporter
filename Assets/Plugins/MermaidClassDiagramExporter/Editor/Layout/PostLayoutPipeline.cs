using System.Collections.Generic;

internal sealed class PostLayoutPipeline
{
    private readonly List<IPostLayoutPass> passes = new List<IPostLayoutPass>();

    public PostLayoutPipeline AddPass(IPostLayoutPass pass)
    {
        if (pass != null)
        {
            passes.Add(pass);
        }

        return this;
    }

    public LayoutResult Run(LayoutGraph graph, LayoutResult result, LayoutOptions options)
    {
        LayoutResult currentResult = result ?? new LayoutResult();
        foreach (IPostLayoutPass pass in passes)
        {
            currentResult = pass.Run(graph, currentResult, options) ?? currentResult;
        }

        return currentResult;
    }
}
