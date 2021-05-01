using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;

namespace FGraph
{
    [DebuggerDisplay("{AllText()}/{LhsAnnotation}/{RhsAnnotation}")]
    public class SENode
    {
        public String SortPrefix { get; set; } = "";
        public List<SEText> TextLines = new List<SEText>();
        public String Class { get; set; }

        public float Width { get; set; } = 0;

        public String HRef { get; set; }


        /// <summary>
        /// Annotation on the line coming into the node (at line end);
        /// </summary>
        public String LhsAnnotation { get; set; }

        /// <summary>
        /// Annotation on the line leaving the node (at start of outgoing line);
        /// </summary>
        public String RhsAnnotation { get; set; }

        public String SortText=> $"{this.SortPrefix}{this.AllText()}";

        public SENode()
        {
        }

        public String AllText()
        {
            StringBuilder sb = new StringBuilder();
            foreach (SEText t in this.TextLines)
                sb.Append($"{t.Text} ");
            return sb.ToString();
        }

        public SENode AddTextLine(SEText text)
        {
            float width = text.GetWidthOfString();
            if (this.Width < width)
                this.Width = width;
            this.TextLines.Add(text);
            return this;
        }

        public SENode AddTextLine(String text, String hRef = null, String title = null)
        {
            return this.AddTextLine(new SEText(text, hRef, title));
        }
    }
}