﻿using Eir.DevTools;
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
using Hl7.Fhir.Serialization;
using FhirKhit.Tools.R4;

namespace FGraph
{
    public partial class FGrapher : ConverterBase
    {
        public string GraphName { get; set; } = null;

        public string BaseUrl { get; set; }

        public string OutputDir
        {
            get => this.outputDir;
            set => this.outputDir = Path.GetFullPath(value);
        }
        private string outputDir;

        Dictionary<String, StructureDefinition> profiles = new Dictionary<String, StructureDefinition>();
        Dictionary<String, ValueSet> valueSets = new Dictionary<String, ValueSet>();
        Dictionary<String, CodeSystem> codeSystems = new Dictionary<String, CodeSystem>();

        Dictionary<String, GraphNode> graphNodesByName = new Dictionary<string, GraphNode>();
        Dictionary<GraphAnchor, GraphNode> graphNodesByAnchor = new Dictionary<GraphAnchor, GraphNode>();

        List<GraphLink> graphLinks = new List<GraphLink>();
        List<SvgEditor> svgEditors = new List<SvgEditor>();

        public bool DebugFlag { get; set; } = false;

        public FGrapher()
        {
        }

        public override void ConversionError(string className, string method, string msg)
        {
            Debugger.Break();
            base.ConversionError(className, method, msg);
        }

        public override void ConversionWarn(string className, string method, string msg)
        {
            //Debugger.Break();
            base.ConversionWarn(className, method, msg);
        }

        public bool TryGetNodeByName(String name, out GraphNode node) => this.graphNodesByName.TryGetValue(name, out node);
        public bool TryGetNodeByAnchor(GraphAnchor anchor, out GraphNode node) => this.graphNodesByAnchor.TryGetValue(anchor, out node);


        public bool TryGetProfile(String url, out StructureDefinition sd) => this.profiles.TryGetValue(url, out sd);
        public bool TryGetValueSet(String url, out ValueSet vs) => this.valueSets.TryGetValue(url, out vs);
        public bool TryGetProfile(String url, out CodeSystem cs) => this.codeSystems.TryGetValue(url, out cs);


        public void LoadResources(String path)
        {
            path = Path.GetFullPath(path);
            if (Directory.Exists(path) == true)
                LoadResourceDir(path);
            else if (File.Exists(path))
                LoadResourceFile(path);
            else
            {
                this.ConversionError("FGrapher",
                    "LoadResources",
                    $"{path} not found");
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
                        SnapshotCreator.Create(sDef);
                    resourceUrl = sDef.Url;
                    profiles.Add(sDef.Url, sDef);
                    break;

                case ValueSet valueSet:
                    resourceUrl = valueSet.Url;
                    valueSets.Add(valueSet.Url, valueSet);
                    break;

                case CodeSystem codeSystem:
                    resourceUrl = codeSystem.Url;
                    codeSystems.Add(codeSystem.Url, codeSystem);
                    break;
            }

            // We expect to only load resources all with the same base resoruce name.
            String rBaseUrl = resourceUrl.FhirBaseUrl();
            if (this.BaseUrl == null)
                this.BaseUrl = rBaseUrl;
            else if (String.Compare(this.BaseUrl, rBaseUrl, StringComparison.InvariantCulture) != 0)
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
                string json = r.ReadToEnd();
                JArray array = JsonConvert.DeserializeObject<JArray>(json);

                foreach (var item in array)
                    LoadItem(item);
            }
        }

        public void LoadItem(JToken item)
        {
            JObject j = item as JObject;
            foreach (KeyValuePair<String, JToken> kvp in j)
                LoadItem(kvp.Key, kvp.Value);
        }


        public void LoadItem(String type, JToken value)
        {
            switch (type)
            {
                case "graphNode":
                    {
                        GraphNode node = new GraphNode(this, value);
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

                case "graphLinkByName":
                    {
                        GraphLinkByName link = new GraphLinkByName(this, value);
                        this.graphLinks.Add(link);
                    }
                    break;

                default:
                    this.ConversionError("FGrapher",
                        "Load",
                        $"unknown graph item '{type}'");
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
                this.ConversionError("FGrapher",
                    "Load",
                    $"{path} not found");
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
                this.ConversionWarn("FGrapher",
                    "FindNamedNodes",
                    $"No nodes named '{name}' found");
            }

            return retVal;
        }

        void ProcessLink(GraphLink link)
        {
            switch (link)
            {
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
            Element fhirElement)
        {
            String System(String system)
            {
                if (system.StartsWith("http://"))
                    system = system.Substring(7);
                else if (system.StartsWith("https://"))
                    system = system.Substring(8);
                return system;
            }

            GraphNode targetNode = new GraphNode(this);
            targetNode.LhsAnnotationText = $"{type} ";

            switch (fhirElement)
            {
                case CodeableConcept codeableConcept:
                    {
                        String system = System(codeableConcept.Coding[0].System);
                        targetNode.DisplayName += $"{codeableConcept.Coding[0].Code}/{system}";
                        targetNode.HRef = codeableConcept.Coding[0].System;
                    }
                    break;

                case Coding coding:
                    {
                        String system = System(coding.System);
                        targetNode.DisplayName += $"{coding.Code}/{system}";
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


        void ProcessLink(GraphLinkByReference link)
        {
            const String fcn = "ProcessLink";

            void CreateLink(GraphNode sourceNode,
                String linkElementId)
            {
                GraphAnchor anchor = sourceNode.Anchor;
                if (anchor == null)
                {
                    this.ConversionError("FGrapher", fcn, $"Node {sourceNode.NodeName} anchor is null");
                    return;
                }

                /*
                 * If link element is is null or starts with '.', then prepend the anchor item id to it.
                 */
                if (
                    (String.IsNullOrEmpty(linkElementId)) ||
                    (linkElementId.StartsWith("."))
                )
                    linkElementId = anchor.Item + linkElementId;

                if (this.profiles.TryGetValue(anchor.Url, out StructureDefinition sDef) == false)
                {
                    this.ConversionError("FGrapher",
                        fcn,
                        $"Node {sourceNode.NodeName}. Can not find profile '{anchor.Url}'");
                    return;
                }

                // put in base path part (i.e. SnapShot.Element[0])
                linkElementId = $"{sDef.BaseDefinition.LastUriPart()}.{linkElementId}";
                ElementDefinition elementDiff = sDef.FindDiffElement(linkElementId);
                if (elementDiff == null)
                {
                    this.ConversionError("FGrapher",
                        fcn,
                        $"Node {sourceNode.NodeName}. Can not find profile 'element {linkElementId}' referenced.");
                    return;
                }

                ElementDefinition elementSnap = sDef.FindSnapElement(linkElementId);
                if (elementSnap == null)
                    return;

                if (elementDiff.Binding != null)
                {
                    GraphNode targetNode = new GraphNode(this);
                    targetNode.DisplayName = elementDiff.Binding.ValueSet.LastPathPart();
                    if (this.valueSets.TryGetValue(elementDiff.Binding.ValueSet, out ValueSet vs) == true)
                    {
                        targetNode.DisplayName = vs.Name;
                    }
                    targetNode.DisplayName += "/ValueSet";
                    targetNode.LhsAnnotationText = "bind";
                    sourceNode.AddChild(link, 0, targetNode);
                    targetNode.AddParent(link, 0, sourceNode);
                }

                if (elementDiff.Pattern != null)
                {
                    GraphNode targetNode = CreateFhirPrimitiveNode("pattern", elementDiff.Pattern);
                    sourceNode.AddChild(link, 0, targetNode);
                    targetNode.AddParent(link, 0, sourceNode);
                }

                if (elementDiff.Fixed != null)
                {
                    GraphNode targetNode = CreateFhirPrimitiveNode("fix", elementDiff.Fixed);
                    sourceNode.AddChild(link, 0, targetNode);
                    targetNode.AddParent(link, 0, sourceNode);
                }

                foreach (var typeRef in elementDiff.Type)
                {
                    switch (typeRef.Code)
                    {
                        case "Reference":
                            foreach (String targetRef in typeRef.TargetProfile)
                            {
                                sourceNode.RhsAnnotationText = $"{elementSnap.Min.Value}..{elementSnap.Max}";
                                GraphAnchor targetAnchor = new GraphAnchor(targetRef, null);
                                GraphNode targetNode = GetTargetNode(targetAnchor);
                                sourceNode.AddChild(link, link.Depth, targetNode);
                                targetNode.AddParent(link, link.Depth, sourceNode);
                            }
                            break;
                    }
                }
            }

            List<GraphNode> sources = FindNamedNodes(link.Source);
            foreach (GraphNode sourceNode in sources)
                CreateLink(sourceNode, link.Item);
        }

        GraphNode GetTargetNode(GraphAnchor targetAnchor)
        {
            if (this.TryGetNodeByAnchor(targetAnchor, out GraphNode targetNode) == true)
                return targetNode;

            if (targetAnchor.Url.StartsWith("http://hl7.org/fhir"))
            {
                targetNode = new GraphNode(this)
                {
                    NodeName = $"fhir/{targetAnchor.Url.LastUriPart()}",
                    DisplayName = "{targetAnchor.Url.LastUriPart()}",
                    CssClass = "fhirResource",
                    Anchor = targetAnchor
                };
                this.graphNodesByName.Add(targetNode.NodeName, targetNode);
                if (targetNode.Anchor != null)
                    this.graphNodesByAnchor.Add(targetNode.Anchor, targetNode);
                return targetNode;
            }

            this.ConversionError("FGrapher",
                "GetTargetNode",
                $"Can not find target '{targetAnchor.Url}'.");
            return null;
        }


        void ProcessLink(GraphLinkByName link)
        {
            const String fcn = "ProcessLink";

            List<GraphNode> sources = FindNamedNodes(link.Source);
            List<GraphNode> targets = FindNamedNodes(link.Target);
            if ((sources.Count > 1) && (targets.Count > 1))
            {
                this.ConversionError("FGrapher",
                    fcn,
                    $"Many to many link not supported. {link.Source}' <--> {link.Target}");
            }

            foreach (GraphNode sourceNode in sources)
            {
                foreach (GraphNode targetNode in targets)
                {
                    sourceNode.AddChild(link, link.Depth, targetNode);
                    targetNode.AddParent(link, link.Depth, sourceNode);
                }
            }
        }

        public void SaveAll()
        {
            foreach (SvgEditor svgEditor in this.svgEditors)
            {
                String fileName = svgEditor.Name.Replace(":", "-");
                svgEditor.Save($"{this.outputDir}\\{fileName}.svg");
            }
        }
    }
}