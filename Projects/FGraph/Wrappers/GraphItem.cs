using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Text;

namespace FGraph
{
    public abstract class GraphItem
    {
        protected FGrapher fGraph;
        protected String traceMsg;
        public String SourceFile;
        public bool breakFlag;

        public GraphItem(FGrapher fGraph,
            String? sourceFile,
            String? traceMsg,
            bool breakFlag)
        {
            this.fGraph = fGraph;
            this.SourceFile = sourceFile == null ? String.Empty : sourceFile;
            this.traceMsg = traceMsg == null ? String.Empty : traceMsg;
            this.breakFlag = breakFlag;
        }

        public virtual String TraceMsg() => this.traceMsg;
        public abstract void Dump(StringBuilder sb, String margin);
    }
}
