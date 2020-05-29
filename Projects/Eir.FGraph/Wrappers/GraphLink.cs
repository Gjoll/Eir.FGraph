using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using CommonMark.Syntax;

namespace FGraph
{
    public abstract class GraphLink : GraphItem
    {
        public String TraversalName { get; set; }
        public Int32 Depth { get; set; }
        private JToken data;

        public override string TraceMsg()
        {
            return data.ToString();
        }

        public GraphLink(FGrapher fGraph, JToken data) : base(fGraph, data.OptionalValue("traceMsg"))
        {
            this.data = data;
            this.TraversalName = data.RequiredValue("traversalName");
            this.Depth = data.OptionalIntValue("depth", 1);
        }
    }
}
