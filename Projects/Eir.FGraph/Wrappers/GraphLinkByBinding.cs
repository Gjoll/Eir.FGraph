using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace FGraph
{
    class GraphLinkByBinding: GraphLinkByItem
    {
        public GraphLinkByBinding(FGrapher fGraph, JToken data) : base(fGraph, data)
        {
            this.Source = data.RequiredValue("source");
        }
    }
}
