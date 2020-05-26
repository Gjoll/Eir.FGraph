using Eir.DevTools;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using Hl7.Fhir.Model;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using Hl7.Fhir.Serialization;
using FhirKhit.Tools.R4;

namespace FGraph
{
    public partial class FGrapher : ConverterBase
    {
        public String FhirResourceNode_CssClass = "fhir";
        public String BindingNode_CssClass = "valueSet";
        public String FixNode_CssClass = "value";
        public String PatternNode_CssClass = "value";
        String currentPath;
        KeyValuePair<String, JToken> currentItem;

        public bool ShowClass { get; set; } = true;
        public string GraphName { get; set; } = null;

        public string BaseUrl { get; set; }

        public string OutputDir
        {
            get => this.outputDir;
            set => this.outputDir = Path.GetFullPath(value);
        }
        private string outputDir;

        Dictionary<String, DomainResource> resources = new Dictionary<String, DomainResource>();
        Dictionary<String, GraphNode> graphNodesByName = new Dictionary<string, GraphNode>();
        Dictionary<GraphAnchor, GraphNode> graphNodesByAnchor = new Dictionary<GraphAnchor, GraphNode>();

        List<GraphLink> graphLinks = new List<GraphLink>();
        List<SvgEditor> svgEditors = new List<SvgEditor>();

        public bool DebugFlag { get; set; } = true;

        public FGrapher()
        {
        }

        public void ConversionError(string method, string msg)
        {
            //Debugger.Break();
            base.ConversionError("FGrapher", method, msg);
        }

        public void ParseItemError(string method, string msg)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(msg);
            sb.AppendLine($"File {Path.GetFileName(this.currentPath)}");
            sb.AppendLine($"{this.currentItem.Key}: {this.currentItem.Value}");
            this.ConversionError(method, sb.ToString());
        }

        public void ParseItemWarn(string method, string msg)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(msg);
            sb.AppendLine($"File {Path.GetFileName(this.currentPath)}");
            sb.AppendLine($"{this.currentItem.Key}: {this.currentItem.Value}");
            this.ConversionWarn(method, sb.ToString());
        }

        public void ConversionWarn(string method, string msg)
        {
            //Debugger.Break();
            base.ConversionWarn("FGrapher", method, msg);
        }

        public void ConversionInfo(string method, string msg)
        {
            //Debugger.Break();
            base.ConversionInfo("FGrapher", method, msg);
        }

        public bool TryGetNodeByName(String name, out GraphNode node) => this.graphNodesByName.TryGetValue(name, out node);
        public bool TryGetNodeByAnchor(GraphAnchor anchor, out GraphNode node) => this.graphNodesByAnchor.TryGetValue(anchor, out node);


        public bool TryGetResource<T>(String url, out T sd)
        where T : DomainResource
        {
            sd = null;
            if (this.resources.TryGetValue(url, out DomainResource dr) == false)
                return false;
            sd = dr as T;
            if (sd == null)
                return false;
            return true;
        }

        public bool TryGetProfile(String url, out StructureDefinition sd) => this.TryGetResource<StructureDefinition>(url, out sd);
        public bool TryGetValueSet(String url, out ValueSet vs) => this.TryGetResource<ValueSet>(url, out vs);
        public bool TryGetProfile(String url, out CodeSystem cs) => this.TryGetResource<CodeSystem>(url, out cs);


        public void LoadResources(String path)
        {
            path = Path.GetFullPath(path);
            if (Directory.Exists(path) == true)
                LoadResourceDir(path);
            else if (File.Exists(path))
                LoadResourceFile(path);
            else
            {
                this.ConversionError("LoadResources", $"{path} not found");
            }
        }


        void LoadResourceDir(String path)
        {
            foreach (String subDir in Directory.GetDirectories(path))
                LoadResourceDir(subDir);
            foreach (String file in Directory.GetFiles(path, "*.json"))
                LoadResourceFile(file);
            foreach (String file in Directory.GetFiles(path, "*.xml"))
                LoadResourceFile(file);
        }


        void LoadResourceFile(String path)
        {
            DomainResource domainResource;
            switch (Path.GetExtension(path).ToUpper(CultureInfo.InvariantCulture))
            {
                case ".XML":
                    {
                        FhirXmlParser parser = new FhirXmlParser();
                        domainResource = parser.Parse<DomainResource>(File.ReadAllText(path));
                        break;
                    }

                case ".JSON":
                    {
                        FhirJsonParser parser = new FhirJsonParser();
                        domainResource = parser.Parse<DomainResource>(File.ReadAllText(path));
                        break;
                    }

                default:
                    throw new Exception($"Unknown extension for serialized fhir resource '{path}'");
            }

            String resourceUrl = null;
            switch (domainResource)
            {
                case StructureDefinition sDef:
                    if (sDef.Snapshot == null)
                    {
                        SnapshotCreator.Create(sDef);
                        sDef.SaveJson(path);
                    }
                    resourceUrl = sDef.Url;
                    break;

                case ValueSet valueSet:
                    resourceUrl = valueSet.Url;
                    break;

                case CodeSystem codeSystem:
                    resourceUrl = codeSystem.Url;
                    break;
            }

            this.resources.Add(resourceUrl, domainResource);

            // We expect to only load resources all with the same base resoruce name.
            String rBaseUrl = resourceUrl.FhirBaseUrl();
            if (String.Compare(this.BaseUrl, rBaseUrl, StringComparison.InvariantCulture) != 0)
                throw new Exception("Resource '{resourceUrl}' does not have base url '{this.BaseUrl}'");
        }

        void LoadDir(String path)
        {
            foreach (String subDir in Directory.GetDirectories(path))
                LoadDir(subDir);
            foreach (String file in Directory.GetFiles(path, "*.nodeGraph"))
                LoadFile(file);
        }

        void LoadFile(String path)
        {
            this.currentPath = path;
            using (StreamReader r = new StreamReader(path))
            {
                StringBuilder json = new StringBuilder();
                json.AppendLine("[");
                json.AppendLine(r.ReadToEnd());
                json.AppendLine("]");
                try
                {
                    JArray array = JsonConvert.DeserializeObject<JArray>(json.ToString());
                    foreach (var item in array)
                        LoadItem(item);
                }
                catch (Exception e)
                {
                    this.ConversionError("LoadFile", $"Error loading json file '{Path.GetFileName(path)}'\n" +
                                                     $"    {e.Message}");
                }
            }
        }

        public void LoadItem(JToken item)
        {
            JObject j = item as JObject;
            foreach (KeyValuePair<String, JToken> kvp in j)
            {
                this.currentItem = kvp;
                LoadItem(kvp.Key, kvp.Value);
            }
        }

        void SetAnchor(GraphNode node)
        {
            const String fcn = "SetAnchor";

            if (node.Anchor == null)
                return;

            String hRef = this.HRef(node.Anchor.Url, node.Anchor.Item);
            node.HRef = hRef;

            if (this.TryGetProfile(node.Anchor.Url, out StructureDefinition sDef) == true)
            {
                node.SDef = sDef;

                String linkElementId = node.Anchor.Item;
                /*
                 * If link element is is null or starts with '.', then prepend the anchor item id to it.
                 */
                if (
                    (String.IsNullOrEmpty(linkElementId)) ||
                    (linkElementId.StartsWith("."))
                )
                    linkElementId = node.Anchor.Item + linkElementId;

                if (node.SDef == null)
                {
                    this.ParseItemError(fcn, $"Node {node.NodeName}. Can not find profile '{node.Anchor.Url}'");
                    return;
                }

                // put in base path part (i.e. SnapShot.Element[0])
                {
                    String id = node.SDef.BaseDefinition.LastUriPart();
                    if (String.IsNullOrEmpty(linkElementId) == false)
                    {
                        id += ".";
                        id += linkElementId;
                    }

                    linkElementId = id;
                }

                ElementDefinition elementDiff = node.SDef.FindDiffElement(linkElementId);
                ElementDefinition elementSnap = node.SDef.FindSnapElement(linkElementId);
                if (elementSnap == null)
                {
                    this.ParseItemError(fcn, $"Node {node.NodeName}. Can not find snapshot element {linkElementId}'.");
                    return;
                }

                node.ElementId = linkElementId;
                node.ElementSnap = elementSnap;
                node.ElementDiff = elementDiff;
            }
        }

        public void LoadItem(String type, JToken value)
        {
            switch (type)
            {
                case "graphNode":
                    {
                        GraphNode node = new GraphNode(this, value);
                        this.SetAnchor(node);
                        this.graphNodesByName.Add(node.NodeName, node);
                        if (node.Anchor != null)
                            this.graphNodesByAnchor.Add(node.Anchor, node);
                    }
                    break;

                case "graphLinkByReference":
                    {
                        GraphLinkByReference link = new GraphLinkByReference(this, value);
                        this.graphLinks.Add(link);
                    }
                    break;

                case "graphLinkByBinding":
                    {
                        GraphLinkByBinding link = new GraphLinkByBinding(this, value);
                        this.graphLinks.Add(link);
                    }
                    break;

                case "graphLinkByName":
                    {
                        GraphLinkByName link = new GraphLinkByName(this, value);
                        this.graphLinks.Add(link);
                    }
                    break;

                default:
                    this.ParseItemError("Load", $"unknown graph item '{type}'");
                    return;
            }
        }

        public void Load(String path)
        {
            path = Path.GetFullPath(path);
            if (Directory.Exists(path) == true)
                LoadDir(path);
            else if (File.Exists(path))
                LoadFile(path);
            else
            {
                this.ConversionError("Load", $"{path} not found");
            }
        }

        protected String HRef(String url, String item = null)
        {
            const String fcn = "HRef";

            if (url.StartsWith("http://hl7.org/fhir/StructureDefinition/"))
                return url;

            if (url.StartsWith(this.BaseUrl) == false)
            {
                this.ParseItemWarn(fcn, $"Url '{url}' base is not fhir and is not {this.BaseUrl}");
                return "";
            }

            String nonBasePart = url.Substring(this.BaseUrl.Length);
            if (nonBasePart.StartsWith("/"))
                nonBasePart = nonBasePart.Substring(1);
            String[] parts = nonBasePart.Split('/');
            if (parts.Length != 2)
                throw new Exception($"Invalid url parts {nonBasePart}");

            if (String.IsNullOrEmpty(item) == true)
                return $"{parts[0]}-{parts[1]}.html";

            if (this.TryGetResource<DomainResource>(url, out DomainResource resource) == false)
            {
                this.ParseItemError(fcn, "Resource {url} not found");
                return null;
            }

            switch (resource)
            {
                case StructureDefinition sDef:
                    ElementDefinition elementSnap = sDef.FindSnapElementShortName(item);
                    if (elementSnap == null)
                    {
                        this.ParseItemError(fcn, $"Snapshot node {sDef.Name}.{item} not found");
                        return null;
                    }
                    return $"{parts[0]}-{parts[1]}-definitions.html#{elementSnap.ElementId}";

                default:
                    this.ParseItemError(fcn, $"Resource type '{resource.GetType().Name}' not implemented ");
                    return null;
            }
        }

        public void Process()
        {
            if (String.IsNullOrEmpty(this.OutputDir) == true)
                throw new Exception($"Output not set");
            if (String.IsNullOrEmpty(this.BaseUrl) == true)
                throw new Exception($"BaseUrl not set");

            ProcessLinks();
        }

        public void ProcessLinks()
        {
            foreach (GraphLink link in this.graphLinks)
                ProcessLink(link);
        }

        List<GraphNode> FindNamedNodes(String name)
        {
            List<GraphNode> retVal = new List<GraphNode>();
            Regex r = new Regex(name);
            foreach (GraphNode graphNode in this.graphNodesByName.Values)
            {
                if (r.IsMatch(graphNode.NodeName))
                    retVal.Add(graphNode);
            }
            if (retVal.Count == 0)
            {
                this.ParseItemWarn("FindNamedNodes",
                    $"No nodes named '{name}' found");
            }

            return retVal;
        }

        void ProcessLink(GraphLink link)
        {
            switch (link)
            {
                case GraphLinkByBinding linkByBinding:
                    ProcessLink(linkByBinding);
                    break;

                case GraphLinkByName linkByName:
                    ProcessLink(linkByName);
                    break;

                case GraphLinkByReference linkByRef:
                    ProcessLink(linkByRef);
                    break;

                default:
                    throw new NotImplementedException($"Unimplemented link type.");
            }
        }

        GraphNode CreateFhirPrimitiveNode(String type,
            Element fhirElement,
            String cssClass)
        {
            String System(String system) => system.LastUriPart();

            GraphNode targetNode = new GraphNode(this, "CreateFhirPrimitiveNode", cssClass);
            targetNode.LhsAnnotationText = $"{type} ";

            switch (fhirElement)
            {
                case CodeableConcept codeableConcept:
                    {
                        String system = System(codeableConcept.Coding[0].System);
                        targetNode.DisplayName += $"{system}#{codeableConcept.Coding[0].Code}";
                        targetNode.HRef = codeableConcept.Coding[0].System;
                    }
                    break;

                case Coding coding:
                    {
                        String system = System(coding.System);
                        targetNode.DisplayName += $"{system}#{coding.Code}";
                        targetNode.HRef = coding.System;
                    }
                    break;

                case Code code:
                    targetNode.DisplayName += $"{code.Value}";
                    break;

                case Integer fInteger:
                    targetNode.DisplayName += $"{fInteger.Value}";
                    break;

                case FhirString fString:
                    targetNode.DisplayName += $"{fString.Value}";
                    break;

                case FhirBoolean fBool:
                    targetNode.DisplayName += $"{fBool.Value}";
                    break;

                default:
                    targetNode.DisplayName += fhirElement.ToString();
                    break;
            }

            return targetNode;
        }

        bool TryGetChildElement(GraphNode sourceNode,
            String linkElementId,
            out ElementDefinition elementDiff,
            out ElementDefinition elementSnap)
        {
            const String fcn = "GetChildElement";

            elementDiff = null;
            elementSnap = null;

            if (sourceNode.Anchor == null)
            {
                this.ParseItemError(fcn, $"Node {sourceNode.NodeName} anchor is null");
                return false;
            }

            /*
             * If link element is is null or starts with '.', then prepend the anchor item id to it.
             */
            if (String.IsNullOrEmpty(linkElementId))
            {
                linkElementId = $"{sourceNode.ElementId}";
            }
            else if (linkElementId.StartsWith("."))
            {
                linkElementId = $"{sourceNode.ElementId}{linkElementId}";
            }

            if (sourceNode.SDef == null)
            {
                this.ParseItemError(fcn, $"Node {sourceNode.NodeName}. Can not find profile '{sourceNode.Anchor.Url}'");
                return false;
            }

            elementDiff = sourceNode.SDef.FindDiffElement(linkElementId);
            if (elementDiff == null)
            {
                this.ParseItemError(fcn, $"Node {sourceNode.NodeName}. Can not find diff element {linkElementId}'.");
                return false;
            }

            elementSnap = sourceNode.SDef.FindSnapElement(linkElementId);
            if (elementSnap == null)
            {
                this.ParseItemError(fcn, $"Node {sourceNode.NodeName}. Can not find snapshot element {linkElementId}'.");
                return false;
            }

            return true;
        }

        void ProcessLink(GraphLinkByReference link)
        {
            //const String fcn = "ProcessLink";

            void ProcessNode(GraphNode sourceNode,
                String linkElementId)
            {
                if (this.DebugFlag)
                    this.ConversionInfo("", $"{sourceNode} -> {linkElementId}");
                ElementDefinition elementDiff;
                ElementDefinition elementSnap;
                if (TryGetChildElement(sourceNode, linkElementId, out elementDiff, out elementSnap) == false)
                    return;

                foreach (ElementDefinition.TypeRefComponent typeRef in elementSnap.Type)
                {
                    switch (typeRef.Code)
                    {
                        case "Reference":
                        case "canonical":
                            foreach (String targetRef in typeRef.TargetProfile)
                            {
                                sourceNode.RhsAnnotationText = $"{elementSnap.Min.Value}..{elementSnap.Max}";
                                GraphAnchor targetAnchor = new GraphAnchor(targetRef, null);
                                GraphNode targetNode = GetTargetNode(targetAnchor);
                                sourceNode.AddChild(link, link.Depth, targetNode);
                                targetNode.AddParent(link, link.Depth, sourceNode);
                                if (this.DebugFlag)
                                    this.ConversionInfo("    ", $"{sourceNode.NodeName} -> (targetProfile) {targetNode.NodeName}");
                            }
                            break;
                        default:
                            foreach (String targetRef in typeRef.Profile)
                            {
                                sourceNode.RhsAnnotationText = $"{elementSnap.Min.Value}..{elementSnap.Max}";
                                GraphAnchor targetAnchor = new GraphAnchor(targetRef, null);
                                GraphNode targetNode = GetTargetNode(targetAnchor);
                                sourceNode.AddChild(link, link.Depth, targetNode);
                                targetNode.AddParent(link, link.Depth, sourceNode);
                                if (this.DebugFlag)
                                    this.ConversionInfo("    ", $"{sourceNode.NodeName} -> (profile) {targetNode.NodeName}");
                            }
                            break;
                    }
                }
            }

            if (this.DebugFlag)
                this.ConversionInfo("LinkByReference", $"{link.Source} -> {link.Item}");
            List<GraphNode> sources = FindNamedNodes(link.Source);
            foreach (GraphNode sourceNode in sources)
                ProcessNode(sourceNode, link.Item);
        }

        GraphNode GetTargetNode(GraphAnchor targetAnchor)
        {
            if (this.TryGetNodeByAnchor(targetAnchor, out GraphNode targetNode) == true)
                return targetNode;

            if (targetAnchor.Url.StartsWith("http://hl7.org/fhir"))
            {
                targetNode = new GraphNode(this, "GetTargetNode", FhirResourceNode_CssClass)
                {
                    NodeName = $"fhir/{targetAnchor.Url.LastUriPart()}",
                    DisplayName = $"{targetAnchor.Url.LastUriPart()}",
                    Anchor = targetAnchor,
                    HRef = targetAnchor.Url
                };
                this.graphNodesByName.Add(targetNode.NodeName, targetNode);
                if (targetNode.Anchor != null)
                    this.graphNodesByAnchor.Add(targetNode.Anchor, targetNode);
                return targetNode;
            }

            this.ParseItemError("GetTargetNode", $"Can not find target '{targetAnchor.Url}'.");
            return null;
        }


        void ProcessLink(GraphLinkByName link)
        {
            const String fcn = "ProcessLink";

            List<GraphNode> sources = FindNamedNodes(link.Source);
            List<GraphNode> targets = FindNamedNodes(link.Target);
            if ((sources.Count > 1) && (targets.Count > 1))
            {
                this.ParseItemError(fcn, $"Many to many link not supported. {link.Source}' <--> {link.Target}");
            }

            if (this.DebugFlag)
                this.ConversionInfo("LinkByName", $"{link.Source} -> {link.Target}");

            foreach (GraphNode sourceNode in sources)
            {
                foreach (GraphNode targetNode in targets)
                {
                    sourceNode.AddChild(link, link.Depth, targetNode);
                    targetNode.AddParent(link, link.Depth, sourceNode);
                    if (this.DebugFlag)
                        this.ConversionInfo("    ", $"{sourceNode.NodeName} -> {targetNode.NodeName}");
                }
            }
        }

        void ProcessLink(GraphLinkByBinding link)
        {
            //const String fcn = "ProcessLink";

            void ProcessNode(GraphNode sourceNode,
                String linkElementId)
            {
                ElementDefinition elementDiff;
                ElementDefinition elementSnap;
                if (TryGetChildElement(sourceNode, linkElementId, out elementDiff, out elementSnap) == false)
                    return;

                if (elementDiff.Binding != null)
                {
                    GraphNode targetNode = new GraphNode(this, "ProcessLink", BindingNode_CssClass);
                    targetNode.HRef = this.HRef(elementDiff.Binding.ValueSet);
                    targetNode.DisplayName = elementDiff.Binding.ValueSet.LastPathPart();
                    if (this.TryGetValueSet(elementDiff.Binding.ValueSet, out ValueSet vs) == true)
                    {
                        targetNode.DisplayName = vs.Name;
                    }
                    targetNode.DisplayName += "/ValueSet";
                    targetNode.LhsAnnotationText = "bind";
                    sourceNode.AddChild(link, 0, targetNode);
                    targetNode.AddParent(link, 0, sourceNode);
                    if (this.DebugFlag)
                        this.ConversionInfo("    ", $"{sourceNode.NodeName} -> (binding) {targetNode.NodeName}");
                }

                if (elementDiff.Pattern != null)
                {
                    GraphNode targetNode = CreateFhirPrimitiveNode("pattern", elementDiff.Pattern, PatternNode_CssClass);
                    sourceNode.AddChild(link, 0, targetNode);
                    targetNode.AddParent(link, 0, sourceNode);
                    if (this.DebugFlag)
                        this.ConversionInfo("    ", $"{sourceNode.NodeName} -> (pattern) {targetNode.NodeName}");
                }

                if (elementDiff.Fixed != null)
                {
                    GraphNode targetNode = CreateFhirPrimitiveNode("fix", elementDiff.Fixed, FixNode_CssClass);
                    sourceNode.AddChild(link, 0, targetNode);
                    targetNode.AddParent(link, 0, sourceNode);
                    if (this.DebugFlag)
                        this.ConversionInfo("    ", $"{sourceNode.NodeName} -> (fixed) {targetNode.NodeName}");
                }
            }

            List<GraphNode> sources = FindNamedNodes(link.Source);

            if (this.DebugFlag)
                this.ConversionInfo("LinkByBinding", $"{link.Source}");

            foreach (GraphNode sourceNode in sources)
                ProcessNode(sourceNode, link.Item);
        }


        public void SaveAll()
        {
            if (this.svgEditors.Count == 0)
                return;

            this.ConversionInfo("SaveAll", $"Writing svg files to {this.outputDir}");
            foreach (SvgEditor svgEditor in this.svgEditors)
            {
                String fileName = svgEditor.Name.Replace(":", "-");
                String outputFile = $"{this.outputDir}\\{fileName}.svg";
                svgEditor.Save(outputFile);
                if (this.DebugFlag)
                    this.ConversionInfo("SaveAll", $"Writing svg file {outputFile}");
            }
        }

    }
}
