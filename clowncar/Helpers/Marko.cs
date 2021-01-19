using Markdig;
using System;
using System.Collections.Generic;
using System.Text;

namespace clowncar.Helpers
{
    public static class Marko
    {
        static readonly MarkdownPipeline pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        public static string ToHtml(this string rawMarkdown)
        {
            rawMarkdown = rawMarkdown.Replace(".md)", ".html)").Replace(".md#", ".html#");
            return Markdown.ToHtml(rawMarkdown, pipeline);
        }
    }
}
