using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace TlbDump
{
    public class HelpStringIdlToNode
    {
        struct GlobalPart
        {
            public Regex testRegex_;
            public string kind_;
            public bool parent_;

            public GlobalPart(Regex testRegex, string kind, bool parent)
            {
                testRegex_ = testRegex;
                kind_ = kind;
                parent_ = parent;
            }
        }
        private static GlobalPart[] globalParts_ = new GlobalPart[]
        {
            new GlobalPart(new Regex(@"library ([^ {]+)"), "Library", true),
            new GlobalPart(new Regex(@"interface ([^ {]+)"), "Interface", true),
            new GlobalPart(new Regex(@"typedef enum ([^ {]+)"), "Enum", true),
            new GlobalPart(new Regex(@"HRESULT ([^ \(]+)"), "Method", false),
        };

        private enum ParsingMode
        {
            Global,
            Enum,
            HelpString,
        };

        private static string parseHelpString(string encoded, out bool terminated)
        {
            string decoded = "";
            terminated = false;
            for (int idx = 0; idx < encoded.Length; ++idx)
            {
                char next = encoded[idx];
                if (encoded[idx] == '\\')
                {
                    ++idx;
                    switch (encoded[idx])
                    {
                        case 'n':
                            next = '\n';
                            break;
                        default:
                            next = encoded[idx];
                            break;
                    }
                }
                else if (encoded[idx] == '"')
                {
                    terminated = true;
                    break;
                }
                decoded += next;
            }
            return decoded;
        }

        public static Node IdlToNode(string path)
        {
            Node current = new Node();
            Node root = current;
            string helpString = "";
            List<Node> stack = new List<Node>();
            string[] lines = File.ReadAllLines(path);
            ParsingMode parsingMode = ParsingMode.Global;

            foreach (var rawLine in lines)
            {
                string line = rawLine;

                // Remove comments
                if (line.Contains("//"))
                {
                    line = line.Substring(0, line.IndexOf("//"));
                }

                // Closing curly brace means we move up the stack of Nodes
                if (line.Trim().StartsWith("}") && (parsingMode == ParsingMode.Global || parsingMode == ParsingMode.Enum))
                {
                    stack.RemoveAt(stack.Count() - 1);
                    parsingMode = ParsingMode.Global;
                }
                else if (line.Trim().StartsWith("[helpstring(") && (parsingMode == ParsingMode.Global || parsingMode == ParsingMode.Enum))
                {
                    bool terminated = false;
                    helpString = line.Trim().Substring("[helpstring(\"".Length);
                    helpString = parseHelpString(helpString, out terminated);
                    if (terminated)
                    {
                        current.Add("description", helpString);
                    }
                    else
                    {
                        parsingMode = ParsingMode.HelpString;
                    }
                }
                else
                {
                    switch (parsingMode)
                    {
                        case ParsingMode.Enum:
                            {
                                var enumName = line.Trim(new char[] { ' ', ',' });
                                if (enumName.Length > 0)
                                {
                                    current.Add("name", enumName);
                                    current.Add("kind", "Field");
                                    stack[stack.Count() - 1].children_.Add(current);
                                    current = new Node();
                                }
                            }
                            break;

                        case ParsingMode.Global:
                            {
                                foreach (var globalPart in globalParts_)
                                {
                                    var match = globalPart.testRegex_.Match(line.Trim());
                                    if (match.Success)
                                    {
                                        current.Add("name", match.Groups[1].Value);
                                        current.Add("kind", globalPart.kind_);
                                        if (stack.Count() > 0)
                                        {
                                            stack[stack.Count() - 1].children_.Add(current);
                                        }
                                        if (globalPart.parent_)
                                        {
                                            stack.Add(current);
                                        }
                                        current = new Node();
                                        if (globalPart.kind_ == "Enum")
                                        {
                                            parsingMode = ParsingMode.Enum;
                                        }
                                        break;
                                    }
                                }
                            }
                            break;

                        case ParsingMode.HelpString:
                            {
                                bool terminated = false;
                                helpString += "\n" + parseHelpString(line, out terminated);

                                if (terminated)
                                {
                                    current.Add("description", helpString);
                                    parsingMode = ParsingMode.Global;
                                    if (stack.Count() > 0 && stack[stack.Count() - 1].properties_["kind"] == "Enum")
                                    {
                                        parsingMode = ParsingMode.Enum;
                                    }
                                }
                            }
                            break;
                    }
                }
            }

            return root;
        }
    }
}
