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
        public static void LinkifyTypesInDescriptionsAndSignatures(Node node)
        {
            HashSet<string> typeNames = new HashSet<string>();
            GetTypeNames(node, typeNames);

            LinkifyTypesInDescriptionsAndSignatures(node, typeNames);
        }

        private static void GetTypeNames(Node node, HashSet<string> typeNames)
        {
            typeNames.Add(node.properties_["name"]);

            foreach (Node child in node.children_)
            {
                switch (child.properties_["kind"])
                {
                    case "Interface":
                    case "Enum":
                    case "Method":
                    case "Property":
                    case "Event":
                        GetTypeNames(child, typeNames);
                        break;
                }

            }
        }

        private static void LinkifyTypesInDescriptionsAndSignatures(Node node, HashSet<string> typeNames)
        {
            foreach (string typeName in typeNames)
            {
                string prefix = node.properties_["kind"] == "Interface" ? "Interface " : "";
                string replacement = "[" + typeName + "](#" + prefix + typeName.ToLower() + ")";
                LinkifyProperty(node, "description", typeName, replacement);
                LinkifyProperty(node, "getterSignature", typeName, replacement);
                LinkifyProperty(node, "setterSignature", typeName, replacement);
                LinkifyProperty(node, "signature", typeName, replacement);
            }

            foreach (Node child in node.children_)
            {
                LinkifyTypesInDescriptionsAndSignatures(child, typeNames);
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
