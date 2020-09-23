using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text;

namespace FGraph
{
    public class GraphItem
    {
        protected FGrapher fGraph;
        protected String traceMsg;
        public String SourceFile;
        public bool breakFlag;

        public GraphItem(FGrapher fGraph,
            String sourceFile,
            String traceMsg,
            bool breakFlag)
        {
            this.fGraph = fGraph;
            this.SourceFile = sourceFile;
            this.traceMsg = traceMsg;
            this.breakFlag = breakFlag;
        }

        public virtual String TraceMsg() => this.traceMsg;
    }
}
