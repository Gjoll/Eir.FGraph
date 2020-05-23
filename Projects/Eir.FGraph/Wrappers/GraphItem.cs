using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace FGraph
{
    public class GraphItem
    {
        protected FGrapher fGraph;
        protected String traceMsg;

        public GraphItem(FGrapher fGraph, 
            String traceMsg)
        {
            this.fGraph = fGraph;
            this.traceMsg = traceMsg;
        }

    }
}
