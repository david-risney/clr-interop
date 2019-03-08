using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Markdig;

namespace TlbDump
{
    public class MarkDownToHtmlConverter
    {
        public static string Convert(string markDown)
        {
            return Markdown.ToHtml(markDown);
        }
    }
}
