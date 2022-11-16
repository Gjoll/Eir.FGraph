using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Text;

namespace FGraph
{
    public class SEText
    {
        public String Class { get; set; } = String.Empty;
        public String Text { get; set; } = String.Empty;
        public String HRef { get; set; } = String.Empty;
        public String Title { get; set; } = String.Empty;

        public SEText(String text, String? hRef = null, String? title = null)
        {
            this.Text = text;
            if (hRef != null)
                this.HRef = hRef;
            if (title != null)
                this.Title = title;
        }

        public SEText()
        {
        }

        public float GetWidthOfString()
        {
            using Bitmap objBitmap = new Bitmap(200, 100);
            using Graphics objGraphics = Graphics.FromImage(objBitmap);
            SizeF stringSize = objGraphics.MeasureString(this.Text, new Font("Arial", 12));
            objBitmap.Dispose();
            objGraphics.Dispose();
            return stringSize.Width;
        }
    }
}