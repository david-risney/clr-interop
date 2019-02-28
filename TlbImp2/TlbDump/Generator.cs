using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TlbDump
{
    public class Generator
    {
        static readonly Regex replacementRule_ = new Regex(@"\{([^\}]+)\}", RegexOptions.Compiled);
        static readonly Regex childSelectionRule_ = new Regex(@"([^ ]+) where ([^=]+) = ([^ ]+) use (.+)", RegexOptions.Compiled);

        private Dictionary<string, string> templates_;
        public Generator(Dictionary<string, string> templates)
        {
            templates_ = templates;
        }

        public string Fill(Node node, string template)
        {
            return replacementRule_.Replace(templates_[template], match =>
            {
                string matched = match.Groups[1].Value;
                Match childMatch = childSelectionRule_.Match(matched);
                if (childMatch.Groups.Count == 5)
                {
                    string targetKind = childMatch.Groups[1].Value;
                    string targetProperty = childMatch.Groups[2].Value;
                    string targetValue = childMatch.Groups[3].Value;
                    string tempalteName = childMatch.Groups[4].Value;

                    var possibleTargets = targetKind == "self" ? new Node[]{ node }.ToList() : node.children_;
                    var filteredChildren = possibleTargets.Where(childNode => childNode.properties_[targetProperty] == targetValue);
                    var mappedChildrenToFilledTemplates = filteredChildren.Select(childNode => Fill(childNode, tempalteName));
                    var combinedFilledTemplates = string.Join("\n", mappedChildrenToFilledTemplates);

                    return combinedFilledTemplates;
                }
                else
                {
                    if (matched.EndsWith("?"))
                    {
                        matched = matched.Substring(0, matched.Length - 1);
                        if (node.properties_.ContainsKey(matched))
                        {
                            return node.properties_[matched];
                        }
                        else
                        {
                            return "";
                        }
                    }
                    else {
                        return node.properties_[matched];
                    }
                }
            });
        }
    }
}
