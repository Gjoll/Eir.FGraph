using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace FGraph
{
    public abstract class GraphLink : GraphItem
    {
        public String TraversalName { get; set; }
        public String Key { get; set; }
        public Int32 Depth { get; set; }
        private JToken data;

        public override string TraceMsg()
        {
            return data.ToString();
        }

        public GraphLink(FGrapher fGraph, String sourceFile, JToken data) :
            base(fGraph, 
                sourceFile, 
                data.OptionalValue("traceMsg"),
                data.OptionalBoolValue("break"))
        {
            this.data = data;
            this.Key = data.OptionalValue("key");
            this.TraversalName = data.RequiredValue("traversalName");
            this.Depth = data.OptionalIntValue("depth", 1);
        }
    }
}
