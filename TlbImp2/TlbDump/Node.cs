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
        public Dictionary<string, string> properties_ = new Dictionary<string, string>();
        public List<Node> children_ = new List<Node>();
        public void Add(string name, string value) { properties_.Add(name, value); }

        public Node() { }
        public Node(string kind, string name, string description)
        {
            Add("kind", kind);
            Add("name", name);
            if (description != null)
            {
                Add("description", description);
            }
        }

        public Node Clone()
        {
            var clone = new Node();
            clone.children_ = new List<Node>(children_.Select(node => node.Clone()));
            clone.properties_ = new Dictionary<string, string>(properties_);
            return clone;
        }

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
                    targets.AddRange(parent.children_.Where(node => node.properties_["name"] == name));
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
                var childName = new string[] { child.properties_["name"] };
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
