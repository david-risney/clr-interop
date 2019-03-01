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
        static readonly Regex conditionalChildRule_ = new Regex(@"if ([^ ]+) where ([^=]+) = ([^ ]+) use (.+)", RegexOptions.Compiled);
        static readonly Regex conditionalRule_ = new Regex(@"if ([^ ]+) use (.+)", RegexOptions.Compiled);
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
                Match conditionalChildMatch = conditionalChildRule_.Match(matched);
                Match childMatch = childSelectionRule_.Match(matched);
                Match conditionalMatch = conditionalRule_.Match(matched);

                if (conditionalChildMatch.Success && conditionalChildMatch.Groups.Count == 5)
                {
                    string targetKind = conditionalChildMatch.Groups[1].Value;
                    string targetProperty = conditionalChildMatch.Groups[2].Value;
                    string targetValue = conditionalChildMatch.Groups[3].Value;
                    string templateName = conditionalChildMatch.Groups[4].Value;
                    return applyChildMatch(node, targetKind, targetProperty, targetValue, templateName, true);
                }
                else if (childMatch.Success && childMatch.Groups.Count == 5)
                {
                    string targetKind = childMatch.Groups[1].Value;
                    string targetProperty = childMatch.Groups[2].Value;
                    string targetValue = childMatch.Groups[3].Value;
                    string templateName = childMatch.Groups[4].Value;
                    return applyChildMatch(node, targetKind, targetProperty, targetValue, templateName, false);
                }
                else if (conditionalMatch.Success && conditionalMatch.Groups.Count == 3)
                {
                    string targetProperty = conditionalMatch.Groups[1].Value;
                    string templateName = conditionalMatch.Groups[2].Value;
                    if (node.properties_.ContainsKey(targetProperty))
                    {
                        return Fill(node, templateName);
                    }
                    else
                    {
                        return "";
                    }
                }
                else
                {
                    if (node.properties_.ContainsKey(matched))
                    {
                        return node.properties_[matched];
                    }
                    else
                    {
                        return "";
                    }
                }
            });
        }

        private string applyChildMatch(Node node, string targetKind, string targetProperty, string targetValue, string templateName, bool runOnSelfNotTarget)
        {
            var possibleTargets = targetKind == "self" ? new Node[] { node }.ToList() : node.children_;
            var filteredChildren = possibleTargets.Where(childNode => childNode.properties_[targetProperty] == targetValue);
            if (filteredChildren.Count() > 0 && runOnSelfNotTarget)
            {
                filteredChildren = new Node[] { node }.ToList();
            }
            var mappedChildrenToFilledTemplates = filteredChildren.Select(childNode => Fill(childNode, templateName));
            var combinedFilledTemplates = string.Join("\n", mappedChildrenToFilledTemplates);

            return combinedFilledTemplates;

        }
    }
}
