﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Hl7.Fhir.ElementModel.Types;
using Hl7.Fhir.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using String = System.String;

namespace FGraph
{
    [DebuggerDisplay("{LegendName}")]
    public class GraphLegend : GraphItem
    {
        public String LegendName { get; set; } = String.Empty;
        public String Item { get; set; } = String.Empty;
        public String CssClass { get; set; } = String.Empty;


        public GraphLegend(FGrapher fGraph,
            String sourceFile,
            String traceMsg,
            bool breakFlag) :
            base(fGraph,
                sourceFile,
                traceMsg,
                breakFlag)
        {
        }


        public GraphLegend(FGrapher fGraph, String sourceFile, JToken data) :
            base(fGraph,
                sourceFile,
                data.OptionalValue("traceMsg"),
                data.OptionalBoolValue("break"))
        {
            this.LegendName = data.RequiredValue("legendName");
            this.Item = data.RequiredValue("item");
            this.CssClass = data.RequiredValue("cssClass");
        }

        public override void Dump(StringBuilder sb, String margin) =>
            sb.AppendLine($"{margin}Legend: {LegendName} {Item} {CssClass}");
    }
}
