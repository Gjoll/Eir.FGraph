using System;
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
        /// <summary>
        /// Class that defines links to/from this node.
        /// </summary>
        public class Link
        {
            public GraphLink Traversal { get; set; }
            public GraphNode Node { get; set; }
            public Int32 Depth { get; set; }
        }

        /// <summary>
        /// Name of this node.
        /// </summary>
        public String NodeName { get; set; }

        /// <summary>
        /// HRef.
        /// </summary>
        public String HRef { get; set; }

        /// <summary>
        /// Optional value of what we linkt his node to. Link can be to
        /// a profile, profile element, value set, code set, etc.
        /// </summary>
        public GraphAnchor Anchor { get; set; }

        /// <summary>
        /// Name to display on graph node. String has multiple parts each seperated
        /// by a '/'. Each part is on its own line.
        /// </summary>
        public String DisplayName { get; set; }

        /// <summary>
        /// css class to set svg element to.
        /// </summary>
        public String CssClass { get; set; }

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
        public String LhsAnnotationText { get; set; }

        /// <summary>
        /// Right hand side (incoming) annotation text. This is printed on the
        /// line that comes into the graph node.
        /// </summary>
        public String RhsAnnotationText { get; set; }

        /// <summary>
        /// If anchor points to a profile, this is the profile.
        /// </summary>
        public StructureDefinition SDef { get; set; } = null;

        /// <summary>
        /// Full element id (if sdef)
        /// </summary>
        public String ElementId { get; set; }

        public GraphNode(FGrapher fGraph, String sourceFile, String traceMsg, String cssClass) : 
            base(fGraph, sourceFile, traceMsg)
        {
            this.CssClass = cssClass;
        }


        public GraphNode(FGrapher fGraph, String sourceFile, JToken data) : base(fGraph, sourceFile, data.OptionalValue("traceMsg"))
        {
            this.NodeName = data.RequiredValue("nodeName");
            this.DisplayName = data.RequiredValue("displayName");
            this.CssClass = data.OptionalValue("cssClass");
            this.LhsAnnotationText = data.OptionalValue("lhsAnnotationText");
            this.RhsAnnotationText = data.OptionalValue("rhsAnnotationText");
            {
                JToken anchor = data["anchor"];
                if (anchor != null)
                    this.Anchor = new GraphAnchor(anchor);
            }
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

            Link link = new Link
            {
                Node = child,
                Traversal = gLink,
                Depth = depth
            };
            this.ChildLinks.Add(link);
        }

        public void AddParent(GraphLink gLink, Int32 depth, GraphNode parent)
        {
            if (AlreadyLinked(this.ParentLinks, parent))
                return;

            Link link = new Link
            {
                Node = parent,
                Traversal = gLink,
                Depth = depth
            };
            this.ParentLinks.Add(link);
        }
    }
}
