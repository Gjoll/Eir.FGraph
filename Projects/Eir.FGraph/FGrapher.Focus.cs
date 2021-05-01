using Eir.DevTools;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using System.Drawing;
using Hl7.Fhir.Model;
using System.Diagnostics;

namespace FGraph
{
    public partial class FGrapher
    {
        public void RenderFocusGraphs(String cssFile, String traversal, Int32 depth)
        {
            foreach (GraphNode node in this.graphNodesByAnchor.Values)
            {
                String tName = traversal.ToMachineName();
                // Only render top level (profile) nodes.
                if (node.Traversals.Contains(traversal))
                    this.RenderFocusGraph(cssFile, node, depth, traversal, $"{tName}Graph-{node.Anchor.Url.LastUriPart()}");
            }
        }

        public void RenderSingleNode(String cssFile,
            String startNode,
            Int32 depth,
            String traversalName,
            String graphName,
            String keys)
        {
            if (this.graphNodesByName.TryGetValue(startNode, out GraphNode focusGraphNode) == false)
            {
                this.ConversionError("RenderSingleNode", $"Start node '{startNode}' not found");
                return;
            }

            HashSet<String> keysHash = new HashSet<string>();
            foreach (String key in keys.Split(','))
                keysHash.Add(key);
            RenderFocusGraph(cssFile, focusGraphNode, depth, traversalName, graphName, keysHash);
        }

        public void RenderFocusGraph(String cssFile,
            GraphNode focusGraphNode,
            Int32 depth,
            String traversalName,
            String graphName,
            HashSet<String> keys = null)
        {
            SvgEditor e = new SvgEditor(graphName);
            e.AddCssFile(cssFile);

            lock (this.svgEditors)
            {
                this.svgEditors.Add(e);
            }
            SENodeGroup seGroupParents = new SENodeGroup("", "parents");
            SENodeGroup seGroupFocus = new SENodeGroup("", "focus");
            SENodeGroup seGroupChildren = new SENodeGroup("", "children");
            seGroupParents.AppendGroup(seGroupFocus);
            seGroupFocus.AppendGroup(seGroupChildren);

            SENode focusSENode = this.CreateNode(focusGraphNode);
            focusSENode.Class = "focus";
            seGroupFocus.AppendNode(focusSENode);
            {
                IEnumerable<SENode> parentNodes = TraverseParents(focusGraphNode,
                    focusSENode,
                    $"{traversalName}/*",
                    1);
                seGroupParents.AppendNodeRange(parentNodes);
            }
            {
                IEnumerable<SENodeGroup> childNodes = TraverseChildren(focusGraphNode,
                    $"{traversalName}/*",
                    depth,
                    keys, new Stack<GraphNode>());
                seGroupFocus.AppendGroupRange(childNodes);
            }
            seGroupParents.Sort();
            this.legends.TryGetValue("focus", out List<GraphLegend> legendNodes);
            e.Render(seGroupParents, legendNodes);
        }

        protected SENode CreateNodeBinding(ElementDefinition.ElementDefinitionBindingComponent binding)
        {
            String hRef = null;
            SENode node = new SENode()
            {
                HRef = hRef
            };
            node.Class = "valueSet";

            String displayName = binding.ValueSet.LastPathPart();
            if (this.TryGetValueSet(binding.ValueSet, out ValueSet vs) == false)
            {
                displayName = vs.Name;
            }
            node.AddTextLine(displayName, hRef);
            node.LhsAnnotation = "bind";
            return node;
        }

        protected SENode CreateNode(GraphNode graphNode)
        {
            SENode node = new SENode
            {
                HRef = graphNode.HRef
            };
            node.Class = graphNode.CssClass;

            String displayName = graphNode.DisplayName;
            //Debug.Assert(displayName != "Breast/Radiology/Composition");

            foreach (String titlePart in displayName.Split('/'))
            {
                String s = titlePart.Trim();
                node.AddTextLine(s, graphNode.HRef);
            }

            node.SortPrefix = graphNode.SortPrefix;
            node.LhsAnnotation = ResolveAnnotation(graphNode, graphNode.LhsAnnotationText);
            node.RhsAnnotation = ResolveAnnotation(graphNode, graphNode.RhsAnnotationText);
            return node;
        }

        String ResolveCardinality(GraphNode node,
            String elementId)
        {
            const String fcn = "ResolveCardinality";

            GraphAnchor anchor = node.Anchor;
            if (anchor == null)
            {
                this.ParseItemError(node.TraceMsg(), fcn, $"Anchor is null");
                return null;
            }

            if (this.TryGetProfile(anchor.Url, out StructureDefinition sDef) == false)
            {
                this.ParseItemError(node.TraceMsg(), fcn, $"StructureDefinition {anchor.Url} not found");
                return null;
            }

            ElementDefinition e = sDef.FindSnapElement(elementId);
            if (e == null)
            {
                e = sDef.FindSnapElement(elementId);
                this.ParseItemError(node.TraceMsg(), fcn, $"Element {elementId} not found");
                return null;
            }

            if (e.Min.HasValue == false)
            {
                this.ParseItemError(node.TraceMsg(),
                    fcn,
                    $"element {elementId}' min cardinality is empty");
                return null;
            }

            if (String.IsNullOrWhiteSpace(e.Max) == true)
            {
                this.ParseItemError(node.TraceMsg(),
                    fcn,
                    $"element {elementId}' max cardinality is empty");
                return null;
            }

            return $"{e.Min.Value}..{e.Max}";
        }


        String ResolveAnnotation(GraphNode node,
            String annotationSource)
        {
            if (String.IsNullOrEmpty(annotationSource))
                return null;
            switch (annotationSource[0])
            {
                case '^':
                    return ResolveCardinality(node, annotationSource.Substring(1));

                default:
                    return annotationSource;
            }
        }

        IEnumerable<SENode> TraverseParents(GraphNode focusNode,
            SENode seFocusNode,
            String traversalFilter,
            Int32 depth)
        {
            const String fcn = "TraverseParents";

            Regex traversalFilterRex = new Regex(traversalFilter);

            HashSet<GraphNode> parentNodes = new HashSet<GraphNode>();
            parentNodes.Add(focusNode);

            foreach (GraphNode.Link parentLink in focusNode.ParentLinks)
            {
                if (
                    (depth >= 0) &&
                    (traversalFilterRex.IsMatch(parentLink.Traversal.TraversalName)) &&
                    (parentNodes.Contains(parentLink.Node) == false)
                )
                {
                    var parentNode = parentLink.Node;

                    // we want to link to top level parent, not element node.
                    while (parentNode.Anchor.Item != null)
                    {
                        switch (parentNode.ParentLinks.Count)
                        {
                            case 0:
                                this.ParseItemError(parentNode.TraceMsg(), fcn, $"No parent nodes found");
                                yield break;
                            case 1:
                                parentNode = parentNode.ParentLinks[0].Node;
                                break;
                            default:
                                this.ParseItemError(parentNode.TraceMsg(), fcn, $"Multiple ({parentNode.ParentLinks.Count}) parent nodes detected");
                                break;
                        }
                    }
                    SENode parent = CreateNode(parentNode);
                    yield return parent;
                }
            }
        }

        IEnumerable<SENodeGroup> TraverseChildren(GraphNode focusNode,
            String traversalFilter,
            Int32 depth,
            HashSet<String> keys,
            Stack<GraphNode> nodeStack)
        {
            if (nodeStack.Contains(focusNode))
                throw new Exception($"Circular linkage at {focusNode.TraceMsg()}");
            nodeStack.Push(focusNode);

            Regex traversalFilterRex = new Regex(traversalFilter);

            bool HasKey(GraphNode.Link childLink) => (keys == null) || keys.Overlaps(childLink.Keys);

            foreach (GraphNode.Link childLink in focusNode.ChildLinks)
            {
                if (
                    (depth > 0) &&
                    (traversalFilterRex.IsMatch(childLink.Traversal.TraversalName)) &&
                    (HasKey(childLink))
                )
                {
                    SENode child = CreateNode(childLink.Node);

                    SENodeGroup childContainer = new SENodeGroup(child.SortPrefix, child.AllText());
                    childContainer.AppendNode(child);

                    childContainer.AppendGroupRange(TraverseChildren(childLink.Node,
                        traversalFilter,
                        depth - childLink.Depth,
                        keys, nodeStack));
                    yield return childContainer;
                }
            }
            nodeStack.Pop();
        }
    }
}
