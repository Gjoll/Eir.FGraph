using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Text;

namespace FGraph
{
    class GraphLinkByBinding: GraphLinkByItem
    {
        public GraphLinkByBinding(FGrapher fGraph, String sourceFile, JToken data) : base(fGraph, sourceFile, data)
        {
            this.Source = data.RequiredValue("source");
        }

        public override void Dump(StringBuilder sb, String margin) =>
            Dump("LinkByBinding", sb, margin);
    }
}
