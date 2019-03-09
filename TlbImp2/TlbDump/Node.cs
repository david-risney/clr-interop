using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TlbImpRuleEngine;
using TypeLibTypes.Interop;

namespace TlbDump
{
    public class Node
    {
        private Dictionary<string, string> properties_ = new Dictionary<string, string>();
        private List<Node> children_ = new List<Node>();

        public void Set(string name, string value) { properties_[name] = value; }
        public string Get(string name) { return properties_[name]; }
        public bool Has(string name) { return properties_.ContainsKey(name); }

        public string Name { get { return properties_["name"]; } set { properties_["name"] = value; } }
        public string Kind { get { return properties_["kind"]; } set { properties_["kind"] = value; } }

        public Node() { }
        public Node(string kind, string name)
        {
            Kind = kind;
            Name = name;
        }

        public Node Clone()
        {
            var clone = new Node();
            clone.children_ = new List<Node>(children_.Select(node => node.Clone()));
            clone.properties_ = new Dictionary<string, string>(properties_);
            return clone;
        }

        public IReadOnlyList<Node> Children { get { return new List<Node>(children_).AsReadOnly(); } }
        public void AddChild(Node child) { children_.Add(child); }
        public void AddChildren(IEnumerable<Node> children) { children_.AddRange(children); }
        public void RemoveChild(Node child) { children_.Remove(child); }

        public List<Node> FindAllChildren(string[] names)
        {
            List<Node> targets = new List<Node>();
            targets.Add(this);

            foreach (var name in names)
            {
                List<Node> parentTargets = targets;
                targets = new List<Node>();
                foreach (var parent in parentTargets)
                {
                    targets.AddRange(parent.Children.Where(node => node.Name == name));
                }
            }

            return targets;
        }

        // Copy missing properties from alternateTree to this Node and its children matching children by name.
        public void Merge(Node alternateTree)
        {
            ShallowMerge(alternateTree);
            foreach (var child in children_)
            {
                var childName = new string[] { child.Name };
                var alternateChildren = alternateTree.FindAllChildren(childName);
                foreach (var alternateChild in alternateChildren)
                {
                    child.Merge(alternateChild);
                }
            }
        }

        private void ShallowMerge(Node alternate)
        {
            foreach (var key in alternate.properties_.Keys)
            {
                if (!properties_.ContainsKey(key))
                {
                    properties_[key] = alternate.properties_[key];
                }
            }
        }
    }
}
