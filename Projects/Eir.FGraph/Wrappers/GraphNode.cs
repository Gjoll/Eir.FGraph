﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Hl7.Fhir.Model;
using Newtonsoft.Json.Linq;

namespace FGraph
{
    [DebuggerDisplay("{NodeName}")]
    public class GraphNode : GraphItem
    {
        public class KeySet
        {
            public HashSet<String> Keys { get; } = new HashSet<String>();
            public HashSet<String> Terms { get; } = new HashSet<String>();

            public KeySet(String keys)
            {
                if (String.IsNullOrEmpty(keys) == false)
                {
                    foreach (String key in keys.Split(','))
                    {
                        String s = key.Trim();
                        if (s.StartsWith("!"))
                            this.Terms.Add(s.Substring(1));
                        else
                            this.Keys.Add(s);
                    }
                }
            }

            public bool Traverse(HashSet<String> traversalKeys)
            {
                if (traversalKeys.Count == 0)
                    return true;
                if (this.Terms.Overlaps(traversalKeys))
                    return false;
                if (this.Keys.Count == 0)
                    return true;
                if (this.Keys.Overlaps(traversalKeys))
                    return true;
                return false;
            }

        }

        /// <summary>
        /// Class that defines links to/from this node.
        /// </summary>
        public class Link
        {
            public GraphLink Traversal { get; }
            public GraphNode Node { get; }
            public Int32 Depth { get; }
            public KeySet Keys { get; }

            public Link(GraphLink traversal,
                GraphNode node,
                Int32 depth,
                String keys)
            {
                this.Traversal = traversal;
                this.Node = node;
                this.Depth = depth;
                this.Keys = new KeySet(keys);
            }

            public void Dump(StringBuilder sb, String margin)
            {
                sb.AppendLine($"{margin}Node: '{Node?.NodeName}'");
                sb.DumpString($"{margin}    ", "depth", Depth.ToString());
                sb.AppendLine($"{margin}    Traversal");
                this.Traversal?.Dump(sb, $"{margin}        ");
            }
        }

        /// <summary>
        /// Name of this node.
        /// </summary>
        public String NodeName
        {
            get => this.nodeName;
            set
            {
                this.nodeName = value;
                Debug.Assert(this.nodeName != "fhir/BreastImagingComposition");
            }
        }

        private String nodeName = String.Empty;

        public KeySet Keys { get; } = new KeySet(String.Empty);

        /// <summary>
        /// HRef.
        /// </summary>
        public String HRef { get; set; } = String.Empty;

        /// <summary>
        /// Optional value of what we linkt his node to. Link can be to
        /// a profile, profile element, value set, code set, etc.
        /// </summary>
        public GraphAnchor? Anchor { get; set; }

        /// <summary>
        /// Name to display on graph node. String has multiple parts each seperated
        /// by a '/'. Each part is on its own line.
        /// </summary>
        public String DisplayName { get; set; } = String.Empty;

        /// <summary>
        /// Prefix to change sort position of item.
        /// </summary>
        public String? SortPrefix { get; set; }

        /// <summary>
        /// css class to set svg element to.
        /// </summary>
        public String? CssClass { get; set; }

        /// <summary>
        /// Names of traversals that this list is a part of.
        /// </summary>
        public List<String> Traversals { get; } = new List<string>();

        public List<Link> ParentLinks { get; } = new List<Link>();
        public List<Link> ChildLinks { get; } = new List<Link>();

        /// <summary>
        /// Left hand side (incoming) annotation text. This is printed on the
        /// line that comes into the graph node.
        /// </summary>
        public String? LhsAnnotationText { get; set; }

        /// <summary>
        /// Right hand side (incoming) annotation text. This is printed on the
        /// line that comes into the graph node.
        /// </summary>
        public String? RhsAnnotationText { get; set; }

        /// <summary>
        /// If anchor points to a profile, this is the profile.
        /// </summary>
        public StructureDefinition? SDef { get; set; } = null;

        /// <summary>
        /// Full element id (if sdef)
        /// </summary>
        public String ElementId { get; set; } = String.Empty;

        public GraphNode(FGrapher fGraph, String sourceFile, String traceMsg, String cssClass, bool breakFlag) :
            base(fGraph, 
                sourceFile, 
                traceMsg,
                breakFlag)
        {
            this.CssClass = cssClass;
        }


        public GraphNode(FGrapher fGraph, String sourceFile, JToken data) : 
            base(fGraph, 
                sourceFile, 
                data.OptionalValue("traceMsg"),
                data.OptionalBoolValue("break"))
        {
            this.NodeName = data.RequiredValue("nodeName");
            this.DisplayName = data.RequiredValue("displayName");
            this.SortPrefix = data.OptionalValue("sortPrefix");
            this.CssClass = data.OptionalValue("cssClass");
            this.LhsAnnotationText = data.OptionalValue("lhsAnnotationText");
            this.RhsAnnotationText = data.OptionalValue("rhsAnnotationText");
            {
                JToken? anchor = data["anchor"];
                if (anchor != null)
                    this.Anchor = new GraphAnchor(anchor);
            }

            String? keys = data.OptionalValue("keys");
            if (keys == null)
                keys = String.Empty;
            this.Keys = new KeySet(keys);
        }

        bool AlreadyLinked(IEnumerable<Link> links, GraphNode node)
        {
            foreach (Link link in links)
                if (link.Node == node)
                    return true;
            return false;
        }

        public void AddChild(GraphLink gLink, Int32 depth, GraphNode child)
        {
            if (AlreadyLinked(this.ChildLinks, child))
                return;

            Link link = new Link(gLink, child, depth, gLink.Key);
            this.ChildLinks.Add(link);
        }

        public void AddParent(GraphLink gLink, Int32 depth, GraphNode parent)
        {
            if (AlreadyLinked(this.ParentLinks, parent))
                return;

            Link link = new Link(gLink, parent, depth, "");
            this.ParentLinks.Add(link);
        }

        public override void Dump(StringBuilder sb, String margin)
        {
            sb.AppendLine($"{margin}GraphNode:");
            sb.DumpString($"{margin}    ", "NodeName", NodeName);
            sb.DumpString($"{margin}    ", "DisplayName", DisplayName);
            sb.DumpString($"{margin}    ", "SortPrefix", SortPrefix);
            sb.DumpString($"{margin}    ", "CssClass", CssClass);
            sb.DumpString($"{margin}    ", "LHS", LhsAnnotationText);
            sb.DumpString($"{margin}    ", "RHS", RhsAnnotationText);

            if (this.Traversals.Count > 0)
            {
                sb.Append($"{margin}    Traversals ");
                foreach (String traversal in Traversals)
                    sb.Append($"{traversal} ");
                sb.AppendLine($"");
            }

            if (this.ParentLinks.Count > 0)
            {
                sb.AppendLine($"{margin}    Parent Links");
                foreach (Link link in this.ParentLinks)
                    link.Dump(sb, $"{margin}        ");
                sb.AppendLine($"");
            }

            if (this.ChildLinks.Count > 0)
            {
                sb.AppendLine($"{margin}    Child Links");
                foreach (Link link in this.ChildLinks)
                    link.Dump(sb, $"{margin}        ");
                sb.AppendLine($"");
            }
        }
    }
}
