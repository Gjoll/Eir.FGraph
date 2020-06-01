using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace FGraph
{
    class GraphLinkByReference : GraphLinkByItem
    {
        public GraphLinkByReference(FGrapher fGraph, String sourceFile, JToken data) : base(fGraph, sourceFile, data)
        {
        }
    }
}
