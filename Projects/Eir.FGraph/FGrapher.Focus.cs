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
                    this.RenderFocusGraph(cssFile,
                        node,
                        depth,
                        traversal,
                        $"{tName}Graph-{node.Anchor?.Url.LastUriPart()}",
                        new HashSet<string>());
            }
        }

        public void RenderSingleNode(String cssFile,
            String startNode,
            Int32 depth,
            String traversalName,
            String graphName,
            String keys)
        {
            if (keys == null)
                keys = String.Empty;
            if (this.graphNodesByName.TryGetValue(startNode, out GraphNode? focusGraphNode) == false)
            {
                this.ConversionError("RenderSingleNode", $"Start node '{startNode}' not found");
                return;
            }

            if (focusGraphNode == null)
                throw new ArgumentException("vs");

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
            HashSet<String> keys)
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
            this.legends.TryGetValue("focus", out List<GraphLegend>? legendNodes);
            e.Render(seGroupParents, legendNodes);
        }

        protected SENode CreateNodeBinding(ElementDefinition.ElementDefinitionBindingComponent binding)
        {
            String hRef = String.Empty;
            SENode node = new SENode()
            {
                HRef = hRef
            };
            node.Class = "valueSet";

            String displayName = binding.ValueSet.LastPathPart();
            if (this.TryGetValueSet(binding.ValueSet, out ValueSet? vs))
            {
                if (vs == null)
                    throw new ArgumentException("vs");
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
            if (graphNode.CssClass != null)
                node.Class = graphNode.CssClass;

            String displayName = graphNode.DisplayName;
            //Debug.Assert(displayName != "Breast/Radiology/Composition");

            foreach (String titlePart in displayName.Split('/'))
            {
                String s = titlePart.Trim();
                node.AddTextLine(s, graphNode.HRef);
            }

            if (graphNode.SortPrefix != null)
                node.SortPrefix = graphNode.SortPrefix;
            if (graphNode.LhsAnnotationText != null)
                node.LhsAnnotation = ResolveAnnotation(graphNode, graphNode.LhsAnnotationText);
            if (graphNode.RhsAnnotationText != null)
                node.RhsAnnotation = ResolveAnnotation(graphNode, graphNode.RhsAnnotationText);
            return node;
        }

        String ResolveCardinality(GraphNode node,
            String elementId)
        {
            const String fcn = "ResolveCardinality";

            if (node.Anchor == null)
            {
                this.ParseItemError(node.TraceMsg(), fcn, $"Anchor is null");
                return String.Empty;
            }

            GraphAnchor anchor = node.Anchor;

            if (this.TryGetProfile(anchor.Url, out StructureDefinition? sDef) == false)
            {
                this.ParseItemError(node.TraceMsg(), fcn, $"StructureDefinition {anchor.Url} not found");
                return String.Empty;
            }

            ElementDefinition? e = sDef?.FindSnapElement(elementId);
            if (e == null)
            {
                this.ParseItemError(node.TraceMsg(), fcn, $"Element {elementId} not found");
                return String.Empty;
            }

            if (e.Min.HasValue == false)
            {
                this.ParseItemError(node.TraceMsg(),
                    fcn,
                    $"element {elementId}' min cardinality is empty");
                return String.Empty;
            }

            if (String.IsNullOrWhiteSpace(e.Max) == true)
            {
                this.ParseItemError(node.TraceMsg(),
                    fcn,
                    $"element {elementId}' max cardinality is empty");
                return String.Empty;
            }

            return $"{e.Min.Value}..{e.Max}";
        }


        String ResolveAnnotation(GraphNode node,
            String annotationSource)
        {
            if (String.IsNullOrEmpty(annotationSource))
                return String.Empty;
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

            List<SENode> retVal = new List<SENode>();
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
                    while ((parentNode != null) && (parentNode.Anchor?.Item != null))
                    {
                        switch (parentNode.ParentLinks.Count)
                        {
                            case 0:
                                this.ParseItemError(parentNode.TraceMsg(), fcn, $"No parent nodes found");
                                parentNode = null;
                                break;
                            case 1:
                                parentNode = parentNode.ParentLinks[0].Node;
                                break;
                            default:
                                this.ParseItemError(parentNode.TraceMsg(), fcn, $"Multiple ({parentNode.ParentLinks.Count}) parent nodes detected");
                                break;
                        }
                    }
                    if (parentNode != null)
                    {
                        SENode parent = CreateNode(parentNode);
                        retVal.Add(parent);
                    }
                }
            }

            return retVal;
        }

        IEnumerable<SENodeGroup> TraverseChildren(GraphNode focusNode,
            String traversalFilter,
            Int32 depth,
            HashSet<String> keys,
            Stack<GraphNode> nodeStack)
        {
            //if (focusNode.DisplayName.Contains("Left Breast"))
            //    Debugger.Break();

            List<SENodeGroup> retVal = new List<SENodeGroup>();
            if ((svgEditors != null) &&
                (focusNode.Keys.Traverse(keys) == false))
                return retVal;

            if (nodeStack.Contains(focusNode))
                throw new Exception($"Circular linkage at {focusNode.TraceMsg()}");
            nodeStack.Push(focusNode);

            Regex traversalFilterRex = new Regex(traversalFilter);
            foreach (GraphNode.Link childLink in focusNode.ChildLinks)
            {
                //if (childLink.Node.NodeName == "BreastImagingComposition/")
                //    Debugger.Break();

                if (
                    (depth > 0) &&
                    (traversalFilterRex.IsMatch(childLink.Traversal.TraversalName)) &&
                    (childLink.Keys.Traverse(keys))
                    )
                {
                    SENode child = CreateNode(childLink.Node);
                    SENodeGroup childContainer = new SENodeGroup(child.SortPrefix, child.AllText());
                    childContainer.AppendNode(child);

                    childContainer.AppendGroupRange(TraverseChildren(childLink.Node,
                        traversalFilter,
                        depth - childLink.Depth,
                        keys, nodeStack));
                    retVal.Add(childContainer);
                }
            }
            nodeStack.Pop();
            return retVal;
        }
    }
}
