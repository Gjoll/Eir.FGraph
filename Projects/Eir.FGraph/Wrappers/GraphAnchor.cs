using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Eir.DevTools;
using Hl7.Fhir.Model;
using static Eir.FhirKhit.R4.ElementPath;

namespace FGraph
{
    [DebuggerDisplay("{UrlName}:{Item}")]
    public class GraphAnchor : IEquatable<GraphAnchor>
    {
        public String UrlName => this.Url.LastUriPart();

        /// <summary>
        /// Url of item.
        /// </summary>
        public String Url { get; }

        /// <summary>
        /// Optional path to sub item.
        /// if empty, refers to whole item with url.
        /// i.e. if url is of a profile, this may be the element id of the
        /// element this anchor refers to.
        /// </summary>
        public String Item { get; }

        public GraphAnchor(String url, String item = "")
        {
            //if (url.Contains("BreastAssessmentCategory"))
            //    Debugger.Break();
            this.Url = url;
            this.Item = item;
        }

        public GraphAnchor(JToken data)
        {
            this.Url = data.RequiredValue("url");
            this.Item= data.OptionalValue("item", String.Empty);
        }

        public bool Equals(GraphAnchor? other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (this.GetType() != other.GetType())
                return false;
            if (this.Url != other.Url) return false;
            if (this.Item != other.Item) return false;
            return true;
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof(GraphAnchor)) return false;
            return Equals((GraphAnchor)obj);
        }

        /// <summary>
        /// This must be overridden in all child classes.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            Int32 retVal = this.Url.GetHashCode();
            if (this.Item != null)
                retVal ^= this.Item.GetHashCode();
            return retVal;
        }
    }
}
