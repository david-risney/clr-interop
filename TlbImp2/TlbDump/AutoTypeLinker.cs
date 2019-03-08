using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

namespace TlbDump
{
    public class AutoTypeLinker
    {
        private Dictionary<string, Dictionary<string, string>> contextToTypeNameToLink_ = new Dictionary<string, Dictionary<string, string>>();

        public AutoTypeLinker(Node library)
        {
            contextToTypeNameToLink_[""] = new Dictionary<string, string>();
            Fill(library, null);
        }

        private void Fill(Node node, Node parent)
        {
            bool addChildren = false;
            switch (node.properties_["kind"])
            {
                case "Interface":
                case "Enum":
                    contextToTypeNameToLink_[""][node.properties_["name"]] = node.properties_["name"].ToLower() + ".html";
                    contextToTypeNameToLink_[node.properties_["name"]] = new Dictionary<string, string>();

                    addChildren = true;
                    break;

                case "Library":
                    addChildren = true;
                    break;

                case "Method":
                case "Property":
                case "Event":
                case "Field":
                    {
                        List<string> nodeNames = new List<string>();
                        string parentName = parent.properties_["name"];
                        nodeNames.Add(node.properties_["name"]);
                        string targetPrefix = parentName.ToLower() + ".html#";
                        string target = targetPrefix + node.properties_["name"].ToLower();

                        if (node.properties_["kind"] == "Method" || node.properties_["kind"] == "Event")
                        {
                            string name = node.properties_["name"];
                            if (name.StartsWith("get_") ||
                                name.StartsWith("put_"))
                            {
                                nodeNames.Add(name.Substring("get_".Length - 1));
                            }
                            else if (name.StartsWith("Set") && name.EndsWith("EventHandler"))
                            {
                                string eventName = name.Substring("Set".Length, name.Length - ("SetEventHandler".Length));
                                nodeNames.Add(eventName);
                                target = targetPrefix + eventName.ToLower();
                            }
                        }

                        foreach (string nodeName in nodeNames)
                        {
                            contextToTypeNameToLink_[""][parentName + "." + nodeName] = target;
                            contextToTypeNameToLink_[parentName][nodeName] = target;
                        }
                    }
                    break;
            }

            if (addChildren)
            {
                foreach (Node child in node.children_)
                {
                    Fill(child, node);
                }
            }
        }

        public void Linkify(Node node, Node parent = null)
        {
            LinkifyInContext(node, parent, "");
            if (parent != null && contextToTypeNameToLink_.ContainsKey(parent.properties_["name"]))
            {
                LinkifyInContext(node, parent, parent.properties_["name"]);
            }

            foreach (Node child in node.children_)
            {
                Linkify(child, node);
            }
        }

        private void LinkifyInContext(Node node, Node parent, string context)
        {
            foreach (KeyValuePair<string, string> entry in contextToTypeNameToLink_[context].OrderByDescending(entry => entry.Key.Length))
            {
                string prefix = node.properties_["kind"] == "Interface" ? "Interface " : "";
                string replacement = "[" + entry.Key + "](" + entry.Value + ")";
                LinkifyProperty(node, "description", entry.Key, replacement);
                //LinkifyProperty(node, "getterSignature", entry.Key, replacement);
                //LinkifyProperty(node, "setterSignature", entry.Key, replacement);
                //LinkifyProperty(node, "signature", entry.Key, replacement);
                LinkifyProperty(node, "eventHandlerType", entry.Key, replacement);
                LinkifyProperty(node, "eventArgsType", entry.Key, replacement);
                LinkifyProperty(node, "asyncInterface", entry.Key, replacement);
            }
        }

        private static void LinkifyProperty(Node node, string property, string typeName, string replacement)
        {
            if (node.properties_.ContainsKey(property))
            {
                Regex regex = new Regex(@"(\W|^)(" + typeName + @")(\W|$)");
                node.properties_[property] = regex.Replace(node.properties_[property], @"$1" + replacement + @"$3");
            }
        }

    }
}
