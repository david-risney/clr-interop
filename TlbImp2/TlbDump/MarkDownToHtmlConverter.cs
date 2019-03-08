using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Markdig;

namespace TlbDump
{
    public class MarkDownToHtmlConverter
    {
        private static string htmlPrefix_ = "<html class='h-100'><head><link rel='stylesheet' href='style.css'/><style>pre { background-color: #f8f9fa; }</style></head><body class='d-flex flex-column h-100'><main role='main' class='flex-shrink-0'><div class='container'>";
        private static string htmlSuffix_ = "</div></main></body></html>";
        private static MarkdownPipeline pipeline_ = (new MarkdownPipelineBuilder()).UseAdvancedExtensions().UseAutoIdentifiers().UseAutoLinks().Build();
        public static string Convert(string markDown)
        {
            return htmlPrefix_ + Markdown.ToHtml(markDown, pipeline_) + htmlSuffix_;
        }

        public static string GetStyleSheet()
        {
            return global::TlbDump.Properties.Resources.bootstrap_min;
        }
    }
}
