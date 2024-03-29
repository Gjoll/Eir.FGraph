﻿using Eir.DevTools;
using Eir.FhirKhit.R4;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Task = System.Threading.Tasks.Task;

namespace FGraph
{
    public partial class FGrapher : ConverterBase
    {
        const String fcn = "Load";
        public String FhirResourceNode_CssClass = "fhir";
        public String BindingNode_CssClass = "valueSet";
        public String ExtensionNode_CssClass = "value";
        public String FixNode_CssClass = "value";
        public String PatternNode_CssClass = "value";
        ConcurrentBag<Task> tasks = new ConcurrentBag<Task>();

        public string GraphName { get; set; } = String.Empty;

        public string BaseUrl { get; set; } = String.Empty;

        String currentItem = String.Empty;

        public string OutputDir
        {
            get => this.outputDir;
            set => this.outputDir = Path.GetFullPath(value);
        }
        private string outputDir = String.Empty;

        ConcurrentDictionary<String, List<GraphLegend>> legends = new ConcurrentDictionary<String, List<GraphLegend>>();
        ConcurrentDictionary<String, DomainResource> resources = new ConcurrentDictionary<String, DomainResource>();
        ConcurrentDictionary<String, GraphNode> graphNodesByName = new ConcurrentDictionary<string, GraphNode>();
        ConcurrentDictionary<GraphAnchor, GraphNode> graphNodesByAnchor = new ConcurrentDictionary<GraphAnchor, GraphNode>();

        public ImmutableSortedDictionary<GraphAnchor, GraphNode> graphNodesByAnchorSorted => graphNodesByAnchor.ToImmutableSortedDictionary();

        List<GraphLink> graphLinks { get; } = new List<GraphLink>();
        List<SvgEditor> svgEditors { get; } = new List<SvgEditor>();

        public bool DebugFlag { get; set; } = true;

        public FGrapher()
        {
        }

        public void ConversionError(string method, string msg)
        {
            //Debugger.Break();
            base.ConversionError("FGrapher", method, msg);
        }

        public void ParseItemError(String? sourceFile, string method, string msg)
        {
            if (sourceFile == null)
                sourceFile = String.Empty;
            String[] sourceFileLines = sourceFile.Split("/n");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(msg);
            sb.AppendLine($"File '{sourceFileLines[0]}'");
            for (Int32 i = 1; i < sourceFileLines.Length - 1; i++)
                sb.AppendLine($"     '{sourceFileLines[i]}'");

            if (String.IsNullOrEmpty(this.currentItem) == false)
                sb.AppendLine(this.currentItem);
            this.ConversionError(method, sb.ToString());
        }

        public void ParseItemWarn(String sourceFile, string method, string msg)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(msg);
            sb.AppendLine($"File {sourceFile}");
            if (String.IsNullOrEmpty(this.currentItem) == false)
                sb.AppendLine(this.currentItem);
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

        public bool TryGetNodeByName(String name, out GraphNode? node) => this.graphNodesByName.TryGetValue(name, out node);
        public bool TryGetNodeByAnchor(GraphAnchor anchor, out GraphNode? node) => this.graphNodesByAnchor.TryGetValue(anchor, out node);


        public bool TryGetResource<T>(String url, out T? sd)
        where T : DomainResource
        {
            sd = null;
            if (this.resources.TryGetValue(url, out DomainResource? dr) == false)
                return false;
            sd = dr as T;
            if (sd == null)
                return false;
            return true;
        }

        public bool TryGetProfile(String url, out StructureDefinition? sd) => this.TryGetResource<StructureDefinition>(url, out sd);
        public bool TryGetValueSet(String url, out ValueSet? vs) => this.TryGetResource<ValueSet>(url, out vs);
        public bool TryGetProfile(String url, out CodeSystem? cs) => this.TryGetResource<CodeSystem>(url, out cs);


        public void LoadResourcesStart(String path)
        {
            const String fcn = "LoadResources";

            if (Directory.Exists(path) == true)
                LoadResourceDir(path);
            else if (File.Exists(path))
                LoadResourceFile(path);
            else
            {
                this.ParseItemError(path, fcn, $"{Path.GetFullPath(path)} not found");
            }
        }

        public void LoadResourcesWaitComplete()
        {
            Task[] currentTasks = this.tasks.ToArray();
            Task.WaitAll(currentTasks);
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
            try
            {
                Task t = this.DoLoadResourceFile(path);
                this.tasks.Add(t);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }


        async Task DoLoadResourceFile(String path)
        {
            DomainResource domainResource;
            if (File.Exists(path) == false)
                throw new Exception($"Resource File '{Path.GetFullPath(path)}' does not exist");
            String text = await File.ReadAllTextAsync(path);
            switch (Path.GetExtension(path).ToUpper(CultureInfo.InvariantCulture))
            {
                case ".XML":
                    {
                        FhirXmlParser parser = new FhirXmlParser(new ParserSettings
                        {
                            AcceptUnknownMembers = true,
                            AllowUnrecognizedEnums = true
                        });
                        domainResource = parser.Parse<DomainResource>(text);
                        break;
                    }

                case ".JSON":
                    {
                        FhirJsonParser parser = new FhirJsonParser(new ParserSettings
                        {
                            AcceptUnknownMembers = true,
                            AllowUnrecognizedEnums = true
                        });
                        domainResource = parser.Parse<DomainResource>(text);
                        break;
                    }

                default:
                    throw new Exception($"Unknown extension for serialized fhir resource '{path}'");
            }

            String? resourceUrl = null;
            switch (domainResource)
            {
                case StructureDefinition sDef:
                    if ((sDef.Snapshot == null) ||
                        (sDef.Snapshot.Element.Count == 0))
                    {
                        this.ConversionInfo("DoLoadResourceFile", $"Start snapshot of {sDef.Name}");
                        SnapshotCreator.Create(sDef);

                        sDef.SaveJson(path);
                        Debug.Assert(sDef.Snapshot != null);
                        Debug.Assert(sDef.Snapshot.Element.Count > 0);
                        this.ConversionInfo("DoLoadResourceFile", $"Completed snapshot of {sDef.Name}");
                    }
                    resourceUrl = sDef.Url;
                    break;

                case ValueSet valueSet:
                    resourceUrl = valueSet.Url;
                    break;

                case CodeSystem codeSystem:
                    resourceUrl = codeSystem.Url;
                    break;

                default:
                    return;
            }

            if (this.resources.TryAdd(resourceUrl, domainResource) == false)
                throw new Exception($"Error adding item to resources");

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
            using (StreamReader r = new StreamReader(path))
            {
                StringBuilder json = new StringBuilder();
                json.AppendLine("[");
                json.AppendLine(r.ReadToEnd());
                json.AppendLine("]");
                try
                {
                    JArray? array = JsonConvert.DeserializeObject<JArray>(json.ToString());
                    if (array == null)
                        throw new Exception($"Null array");
                    foreach (var item in array)
                        LoadItem(path, item);
                }
                catch (Exception e)
                {
                    this.ParseItemError(path, "LoadFile", $"Error loading json file '{path}'\n" +
                                                     $"    {e.Message}");
                }
            }
        }

        void LoadItem(String sourceFile, JToken item)
        {
            JObject j = (JObject)item;
            foreach (KeyValuePair<String, JToken?> kvp in j)
            {
                if (kvp.Value == null)
                    throw new Exception($"Null jtoken");
                this.currentItem = $"{kvp.Key}: {kvp.Value}";
                LoadItem(sourceFile, kvp.Key, kvp.Value);
            }
        }

        void SetAnchor(GraphNode node)
        {
            const String fcn = "SetAnchor";

            if (node.Anchor == null)
                return;

            String hRef = this.HRef(node.TraceMsg(), node.Anchor.Url, node.Anchor.Item);
            node.HRef = hRef;

            if (this.TryGetProfile(node.Anchor.Url, out StructureDefinition? sDef) == true)
            {
                node.SDef = sDef;
                String? linkElementId = node.Anchor?.Item;
                /*
                 * If link element is is null or starts with '.', then prepend the anchor item id to it.
                 */
                if (
                    (String.IsNullOrEmpty(linkElementId)) ||
                    (linkElementId.StartsWith("."))
                )
                    linkElementId = node.Anchor?.Item + linkElementId;

                if (node.SDef == null)
                {
                    this.ParseItemError(node.TraceMsg(), fcn, $"Node {node.NodeName}. Can not find profile '{node.Anchor?.Url}'");
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
                node.ElementId = linkElementId;
            }
        }

        public void LoadItem(String sourceFile, String type, JToken value)
        {
            switch (type)
            {
                case "legend":
                    {
                        GraphLegend node = new GraphLegend(this, sourceFile, value);
                        if (this.legends.TryGetValue(node.LegendName, out List<GraphLegend>? itemList) == false)
                        {
                            itemList = new List<GraphLegend>();
                            this.legends.TryAdd(node.LegendName, itemList);
                        }
                        itemList.Add(node);
                    }
                    break;

                case "node":
                    {
                        GraphNode node = new GraphNode(this, sourceFile, value);
                        //if (node.NodeName.Contains("BreastAssessmentCategory"))
                        //    Debugger.Break();
                        this.SetAnchor(node);
                        if (this.graphNodesByName.TryAdd(node.NodeName, node) == false)
                        {
                            this.ParseItemError(sourceFile,
                                fcn,
                                $"Error adding node {node.NodeName} to graphNodesByName. Item already exists?");
                            return;
                        }

                        if (node.Anchor != null)
                        {
                            if (this.graphNodesByAnchor.TryAdd(node.Anchor, node) == false)
                            {
                                this.ParseItemError(sourceFile,
                                    fcn,
                                    $"Error adding node {node.NodeName} to graphNodesByAnchor");
                                return;
                            }
                        }
                    }
                    break;

                case "graph":
                    {
                        String? nodeName = value?["nodeName"]?.Value<String>();
                        String? traversalName = value?["traversalName"]?.Value<String>();
                        if (String.IsNullOrEmpty(nodeName))
                        {
                            this.ParseItemError(sourceFile, fcn, $"graph command missing required nodeName");
                            return;
                        }

                        if (String.IsNullOrEmpty(traversalName))
                        {
                            this.ParseItemError(sourceFile, fcn, $"graph command missing required traversalName");
                            return;
                        }

                        if (this.TryGetNodeByName(nodeName, out GraphNode? node) == false)
                        {
                            this.ParseItemError(sourceFile, fcn, $"unknown node '{nodeName}' in graph command");
                            return;
                        }
                        if (node?.Traversals.Contains(traversalName) == false)
                            node?.Traversals.Add(traversalName);
                    }
                    break;

                case "linkByReference":
                    {
                        GraphLinkByReference link = new GraphLinkByReference(this, sourceFile, value);
                        lock (this.graphLinks)
                            this.graphLinks.Add(link);
                    }
                    break;

                case "linkByBinding":
                    {
                        GraphLinkByBinding link = new GraphLinkByBinding(this, sourceFile, value);
                        lock (this.graphLinks)
                            this.graphLinks.Add(link);
                    }
                    break;

                case "linkByName":
                    {
                        GraphLinkByName link = new GraphLinkByName(this, sourceFile, value);
                        lock (this.graphLinks)
                        {
                            this.graphLinks.Add(link);
                        }
                    }
                    break;

                default:
                    this.ParseItemError(sourceFile, fcn, $"unknown graph item '{type}'");
                    return;
            }
        }

        public void Load(String path)
        {
            if (Directory.Exists(path) == true)
                LoadDir(path);
            else if (File.Exists(path))
                LoadFile(path);
            else
            {
                this.ParseItemError(path, fcn, $"{Path.GetFullPath(path)} not found");
            }
        }

        protected String HRefStructDef(String profile, String elementId)
        {
            return $"StructureDefinition-{profile}-definitions.html#{elementId}";
        }


        protected String HRef(String sourceFile, String url,
            String? item = null,
            bool warnFlag = true)
        {
            const String fcn = "HRef";

            if (url.StartsWith("http://hl7.org/fhir/StructureDefinition/"))
                return url;

            if (url.StartsWith(this.BaseUrl) == false)
            {
                if (warnFlag == false)
                    return String.Empty;
                this.ParseItemWarn(sourceFile, fcn, $"Url '{url}' base is not fhir and is not {this.BaseUrl}");
                return String.Empty;
            }

            String nonBasePart = url.Substring(this.BaseUrl.Length);
            if (nonBasePart.StartsWith("/"))
                nonBasePart = nonBasePart.Substring(1);
            String[] parts = nonBasePart.Split('/');
            if (parts.Length != 2)
                throw new Exception($"Invalid url parts {nonBasePart}");

            if (String.IsNullOrEmpty(item) == true)
                return $"{parts[0]}-{parts[1]}.html";

            if (this.TryGetResource<DomainResource>(url, out DomainResource? resource) == false)
            {
                this.ParseItemError(sourceFile, fcn, $"Resource {url} not found");
                return String.Empty;
            }

            switch (resource)
            {
                case StructureDefinition sDef:
                    String normalizedName = sDef.NormalizedName(item);
                    return HRefStructDef(parts[1], normalizedName);

                case null:
                    this.ParseItemError(sourceFile, fcn, $"Resource is null");
                    return String.Empty;

                default:
                    this.ParseItemError(sourceFile, fcn, $"Resource type '{resource.GetType().Name}' not implemented ");
                    return String.Empty;
            }
        }

        public void DumpNodeLinks(String path)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("");
            sb.AppendLine("");
            sb.AppendLine("Nodes");
            sb.AppendLine("");
            {
                List<String> keys = graphNodesByName.Keys.ToList();
                keys.Sort();
                foreach (String key in keys)
                    graphNodesByName[key].Dump(sb, "    ");
            }

            sb.AppendLine("");
            sb.AppendLine("");
            sb.AppendLine("Links");
            sb.AppendLine("");
            foreach (GraphLink link in graphLinks)
                link.Dump(sb, "    ");
            File.WriteAllText(path, sb.ToString());
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
            lock (this.graphLinks)
            {
                foreach (GraphLink link in this.graphLinks)
                    ProcessLink(link);
            }
        }

        void ShowCloseMatches(String sourceFile,
            String fcn,
            String name,
            IEnumerable<GraphNode> nodes)
        {
            if (name.StartsWith('^') == false)
                return;
            name = name.Substring(1);
            Int32 index = name.IndexOfAny(new char[] { '.', '[', '/' });
            if (index > 0)
                name = name.Substring(0, index);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Close matches");

            foreach (GraphNode node in nodes)
                if (node.NodeName.StartsWith(name))
                    sb.AppendLine($"  {node.NodeName}");
            this.ParseItemWarn(sourceFile, fcn, sb.ToString());
        }

        List<GraphNode> FindNamedNodes(String sourceFile, String name)
        {
            List<GraphNode> retVal = new List<GraphNode>();
            name = name.Replace("[x]", @"\[x\]");
            Regex r = new Regex(name);
            foreach (GraphNode graphNode in this.graphNodesByName.Values)
            {
                if (r.IsMatch(graphNode.NodeName))
                    retVal.Add(graphNode);
            }
            if (retVal.Count == 0)
            {
                this.ParseItemWarn(sourceFile,
                    "FindNamedNodes",
                    $"No nodes named '{name}' found");

                ShowCloseMatches(sourceFile,
                    "FindNamedNodes",
                    name,
                    this.graphNodesByName.Values);
            }

            return retVal;
        }

        void ProcessLink(GraphLink link)
        {
            //if (link.breakFlag)
            //    Debugger.Break();
            this.currentItem = link.TraceMsg();
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

        GraphNode CreateFhirPrimitiveNode(String sourceFile,
            String type,
            Element fhirElement,
            String cssClass)
        {
            String System(String system) => system.LastUriPart();

            GraphNode targetNode = new GraphNode(this, "CreateFhirPrimitiveNode", "FGrapher.CreateFhirPrimitiveNode", cssClass, false);
            targetNode.LhsAnnotationText = $"{type} ";

            switch (fhirElement)
            {
                case CodeableConcept codeableConcept:
                    {
                        String system = System(codeableConcept.Coding[0].System);
                        targetNode.DisplayName += $"{system}#{codeableConcept.Coding[0].Code}";
                        targetNode.HRef = this.HRef(String.Empty, codeableConcept.Coding[0].System, null, false);
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
            out ElementDefinition? elementDiff,
            out ElementDefinition? elementSnap)
        {
            const String fcn = "GetChildElement";

            elementDiff = null;
            elementSnap = null;

            if (sourceNode.Anchor == null)
            {
                this.ParseItemError(sourceNode.TraceMsg(), fcn, $"Node {sourceNode.NodeName} anchor is null");
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
                this.ParseItemError(sourceNode?.TraceMsg(), fcn, $"Node {sourceNode?.NodeName}. Can not find profile '{sourceNode?.Anchor?.Url}'");
                return false;
            }

            elementDiff = sourceNode?.SDef?.FindDiffElement(linkElementId);
            if (elementDiff == null)
            {
                this.ParseItemError(sourceNode?.TraceMsg(), fcn, $"Node {sourceNode?.NodeName}. Can not find diff element {linkElementId}'.");
                return false;
            }

            if (sourceNode?.SDef?.BaseDefinition == "http://hl7.org/fhir/StructureDefinition/Extension")
            {
                elementSnap = elementDiff;
                return true;
            }

            elementSnap = sourceNode?.SDef?.FindSnapElement(linkElementId);
            if (elementSnap == null)
            {
                this.ParseItemError(sourceNode?.TraceMsg(),
                    fcn,
                    $"Node {sourceNode?.NodeName}. Can not find snapshot element {linkElementId}'.");
                return false;
            }

            return true;
        }

        void ProcessLink(GraphLinkByReference link)
        {
            const String fcn = "ProcessLink";

            void LinkToProfile(GraphNode sourceNode,
                ElementDefinition elementSnap,
                ElementDefinition.TypeRefComponent typeRef,
                IEnumerable<String> profiles)
            {
                foreach (String targetRef in profiles)
                {
                    GraphAnchor targetAnchor = new GraphAnchor(targetRef);
                    GraphNode? targetNode = GetTargetNode(targetAnchor);
                    if (targetNode != null)
                    {
                        sourceNode.AddChild(link, link.Depth, targetNode);
                        targetNode.AddParent(link, link.Depth, sourceNode);
                        Int32? min = elementSnap?.Min;
                        if (min == null)
                            min = 0;
                        targetNode.LhsAnnotationText = $"{min}..{elementSnap?.Max}";
                        if (this.DebugFlag)
                            this.ConversionInfo(fcn, $"    {sourceNode.NodeName} -> (profile) {targetNode.NodeName}");
                    }
                }
            }

            void ProcessNode(GraphNode sourceNode,
                GraphLinkByReference link)
            {
                String linkElementId = link.Item;
                if (this.DebugFlag)
                    this.ConversionInfo(fcn, $"{sourceNode} -> {linkElementId}");
                ElementDefinition? elementDiff;
                ElementDefinition? elementSnap;
                if (TryGetChildElement(sourceNode, linkElementId, out elementDiff, out elementSnap) == false)
                    return;
                if (elementDiff == null)
                    throw new ArgumentException("elementDiff");
                if (elementSnap == null)
                    throw new ArgumentException("elementSnap");
                foreach (ElementDefinition.TypeRefComponent typeRef in elementSnap.Type)
                {
                    if (typeRef.Profile.Count() > 0)
                        LinkToProfile(sourceNode, elementSnap, typeRef, typeRef.Profile);
                    else if (typeRef.TargetProfile.Count() > 0)
                        LinkToProfile(sourceNode, elementSnap, typeRef, typeRef.TargetProfile);
                    else
                        throw new NotImplementedException();
                }
            }

            if (this.DebugFlag)
                this.ConversionInfo("LinkByReference", $"{link.Source} -> {link.Item}");
            List<GraphNode> sources = FindNamedNodes(link.TraceMsg(), link.Source);
            foreach (GraphNode sourceNode in sources)
                ProcessNode(sourceNode, link);
        }

        GraphNode? GetTargetNode(GraphAnchor targetAnchor)
        {
            if (this.TryGetNodeByAnchor(targetAnchor, out GraphNode? targetNode) == true)
            {
                if (targetNode == null)
                    throw new ArgumentException("targetNode");
                return targetNode;
            }

            if (targetAnchor.Url.StartsWith("http://hl7.org/fhir"))
            {
                targetNode = new GraphNode(this, "GetTargetNode", "FGrapher.getTargetNode", FhirResourceNode_CssClass, false)
                {
                    NodeName = $"fhir/{targetAnchor.Url.LastUriPart()}",
                    DisplayName = $"{targetAnchor.Url.LastUriPart()}",
                    Anchor = targetAnchor,
                    HRef = targetAnchor.Url
                };
                if (this.graphNodesByName.TryAdd(targetNode.NodeName, targetNode) == false)
                    throw new Exception($"Error adding node {targetNode.NodeName} to graphNodesByAnchor");

                if (targetNode.Anchor != null)
                {
                    if (this.graphNodesByAnchor.TryAdd(targetNode.Anchor, targetNode) == false)
                        throw new Exception($"Error adding node {targetNode.NodeName} to graphNodesByAnchor");
                }
                return targetNode;
            }

            this.ParseItemError(String.Empty, "GetTargetNode", $"Can not find target '{targetAnchor.Url}'.");
            return null;
        }


        void ProcessLink(GraphLinkByName link)
        {
            const String fcn = "ProcessLink";

            List<GraphNode> sources = FindNamedNodes(link.TraceMsg(), link.Source);
            List<GraphNode> targets = FindNamedNodes(link.TraceMsg(), link.Target);
            if ((sources.Count > 1) && (targets.Count > 1))
            {
                this.ParseItemError(link.Source, fcn, $"Many to many link not supported. {link.Source}' <--> {link.Target}");
            }

            if (this.DebugFlag)
                this.ConversionInfo("LinkByName", $"{link.Source} -> {link.Target}");

            //Debug.Assert(link.Source != "^BreastRadiologyReport/$");
            foreach (GraphNode sourceNode in sources)
            {
                foreach (GraphNode targetNode in targets)
                {
                    sourceNode.AddChild(link, link.Depth, targetNode);
                    targetNode.AddParent(link, link.Depth, sourceNode);
                    if (this.DebugFlag)
                        this.ConversionInfo(fcn, $"    {sourceNode.NodeName} -> {targetNode.NodeName}");
                }
            }
        }

        void ProcessNodeBindings(GraphLink link,
            GraphNode sourceNode,
            String linkElementId)
        {
            //const String fcn = "ProcessNodeBindings";

            ElementDefinition? elementDiff;
            ElementDefinition? elementSnap;
            if (TryGetChildElement(sourceNode, linkElementId, out elementDiff, out elementSnap) == false)
                return;
            if (elementDiff == null)
                throw new ArgumentException("elementDiff");
            if (elementSnap == null)
                throw new ArgumentException("elementSnap");
            ProcessNodeBindings(link, sourceNode, linkElementId, elementDiff, elementSnap);
        }

        void ProcessNodeBindings(GraphLink link,
            GraphNode sourceNode,
            String linkElementId,
            ElementDefinition elementDiff,
            ElementDefinition elementSnap)
        {
            const String fcn = "ProcessNodeBindings";

            if (elementDiff.Binding != null)
            {
                GraphNode targetNode = new GraphNode(this, "ProcessLink", "FGrapher.ProcessNode", BindingNode_CssClass, false);
                targetNode.HRef = this.HRef(link.TraceMsg(), elementDiff.Binding.ValueSet);
                targetNode.DisplayName = elementDiff.Binding.ValueSet.LastPathPart();
                if (this.TryGetValueSet(elementDiff.Binding.ValueSet, out ValueSet? vs) == true)
                {
                    if (vs == null)
                        throw new ArgumentException("vs");
                    targetNode.DisplayName = vs.Name;
                }
                targetNode.LhsAnnotationText = "bind";
                sourceNode.AddChild(link, 0, targetNode);
                targetNode.AddParent(link, 0, sourceNode);
                if (this.DebugFlag)
                    this.ConversionInfo(fcn, $"    {sourceNode.NodeName} -> (binding) {targetNode.NodeName}");
            }

            if (elementDiff.Pattern != null)
            {
                GraphNode targetNode = CreateFhirPrimitiveNode(sourceNode.TraceMsg(), "pattern", elementDiff.Pattern, PatternNode_CssClass);
                sourceNode.AddChild(link, 0, targetNode);
                targetNode.AddParent(link, 0, sourceNode);
                if (this.DebugFlag)
                    this.ConversionInfo(fcn, $"    {sourceNode.NodeName} -> (pattern) {targetNode.NodeName}");
            }

            if (elementDiff.Fixed != null)
            {
                GraphNode targetNode = CreateFhirPrimitiveNode(sourceNode.TraceMsg(), "fix", elementDiff.Fixed, FixNode_CssClass);
                sourceNode.AddChild(link, 0, targetNode);
                targetNode.AddParent(link, 0, sourceNode);
                if (this.DebugFlag)
                    this.ConversionInfo(fcn, $"    {sourceNode.NodeName} -> (fixed) {targetNode.NodeName}");
            }
        }

        void ProcessLink(GraphLinkByBinding link)
        {
            //const String fcn = "ProcessLink";

            List<GraphNode> sources = FindNamedNodes(link.TraceMsg(), link.Source);

            if (this.DebugFlag)
                this.ConversionInfo("LinkByBinding", $"{link.Source}");

            foreach (GraphNode sourceNode in sources)
                this.ProcessNodeBindings(link, sourceNode, link.Item);
        }


        public void SaveAll()
        {
            lock (this.graphLinks)
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
}
