using SVGLib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace FGraph
{
    class SvgEditor
    {
        class EndPoint
        {
            public PointF Location { get; set; }
            public String Annotation { get; set; }
        }

        const Int32 MinXGap = 2;
        const String ArrowStart = "arrowStart";
        const String ArrowEnd = "arrowEnd";

        const float ArrowEndSize = 0.5f;
        SvgDoc doc;
        SvgRoot root;

        public String Name { get; set; }

        //public String RenderTestPoint;
        public float BorderWidth { get; set; } = 0.125f;
        public float LineHeight { get; set; } = 1.25f;
        public float BorderMargin { get; set; } = 0.5f;
        public float NodeGapY { get; set; } = 0.5f;
        public float NodeGapX { get; set; } = 0.5f;
        public float RectRx { get; set; } = 0.25f;
        public float RectRy { get; set; } = 0.25f;

        List<String> cssFiles = new List<string>();

        String ToPx(float value) => $"{15 * value}";

        float screenX = -1;
        float screenY = -1;

        float minX = 0;
        float maxX = 0;
        float minY = 0;
        float maxY = 0;

        public SvgEditor(String name)
        {
            this.Name = name;
            this.doc = new SvgDoc();
            this.root = this.doc.CreateNewDocument();
            this.CreateArrowStart();
            this.CreateArrowEnd();
        }

        public void AddCssFile(String cssFile)
        {
            this.cssFiles.Add(cssFile);
            String cssCmd = $"type=\"text/css\" href=\"{Path.GetFileName(cssFile)}\" ";
            this.doc.StyleSheets.Add(cssCmd);
        }

        void CreateArrowEnd()
        {
            SvgMarker arrowEnd = this.doc.AddMarker();
            arrowEnd.RefX = $"{this.ToPx(ArrowEndSize)}";
            arrowEnd.RefY = $"{this.ToPx(ArrowEndSize / 2)}";
            arrowEnd.MarkerWidth = $"{this.ToPx(ArrowEndSize)}";
            arrowEnd.MarkerHeight = $"{this.ToPx(ArrowEndSize)}";
            arrowEnd.MarkerUnits = "px";
            arrowEnd.Id = ArrowEnd;

            SvgPolygon p = this.doc.AddPolygon(arrowEnd);
            p.Points = $"0 0 {this.ToPx(ArrowEndSize)} {this.ToPx(ArrowEndSize / 2)} 0 {this.ToPx(ArrowEndSize)}";
            p.StrokeWidth = "0";
            p.Fill = Color.Black;
            p.StrokeWidth = "0";
        }

        void CreateArrowStart()
        {
            float radius = 0.125f;

            SvgMarker arrowStart = this.doc.AddMarker();
            arrowStart.RefX = $"{this.ToPx(radius)}";
            arrowStart.RefY = $"{this.ToPx(radius)}";
            arrowStart.MarkerWidth = $"{this.ToPx(2 * radius)}";
            arrowStart.MarkerHeight = $"{this.ToPx(2 * radius)}";
            arrowStart.MarkerUnits = "px";
            arrowStart.Id = ArrowStart;

            SvgCircle c = this.doc.AddCircle(arrowStart);
            c.CX = $"{this.ToPx(radius)}";
            c.CY = $"{this.ToPx(radius)}";
            c.R = $"{this.ToPx(radius)}";
            c.Fill = Color.Black;
            c.StrokeWidth = "0";
        }

        public float NodeGapRhsX(SENodeGroup g)
        {
            Int32 retVal = g.MaxRhsAnnotation();
            if (retVal < MinXGap)
                retVal = MinXGap;
            return retVal;
        }

        public float NodeGapLhsX(SENodeGroup g)
        {
            Int32 retVal = g.MaxLhsAnnotation();
            if (retVal < MinXGap)
                retVal = MinXGap;
            return retVal;
        }

        public void Render(SENodeGroup group)
        {
            this.minX = 0;
            this.minY = 0;
            this.screenX = this.minX + this.BorderMargin;
            this.screenY = this.minY + this.BorderMargin;

            List<EndPoint> endConnectors = new List<EndPoint>();

            this.RenderGroup(group, this.screenX, this.screenY, out float width, out float height,
                endConnectors);
            float totalWidth = this.maxX - this.minX;
            float totalHeight = this.maxY - this.minY;
            this.root.Width = $"{this.ToPx(totalWidth) + 2 * this.NodeGapX}";
            this.root.Height = $"{this.ToPx(totalHeight + 2 * this.NodeGapY)}";
            this.screenY = this.maxY + 4 * this.BorderMargin;
        }

        void RenderGroup(SENodeGroup group,
            float screenX,
            float screenY,
            out float colWidth,
            out float colHeight,
            List<EndPoint> endConnectors)
        {
            colWidth = 0;
            colHeight = 0;

            // Some groups just contain sub groups (no nodes). Make each group children of this groups parent.

            if (group.Nodes.Count() > 0)
                this.RenderSimpleGroup(group, screenX, screenY, out colWidth, out colHeight, endConnectors);
            else if (group.ChildGroups.Count() > 0)
            {
                foreach (SENodeGroup childGroup in group.ChildGroups)
                {
                    this.RenderGroup(childGroup, screenX, screenY, out float tColWidth, out float tColHeight,
                        endConnectors);
                    colHeight += tColHeight;
                    screenY += tColHeight;
                    if (colWidth < tColWidth)
                        colWidth = tColWidth;
                }
            }
        }

        void RenderSimpleGroup(SENodeGroup group,
            float screenX,
            float screenY,
            out float colWidth,
            out float colHeight,
            List<EndPoint> endConnectors)
        {
            colWidth = 0;
            colHeight = 0;

            SvgGroup childGroup = this.doc.AddGroup(null);
            childGroup.Class = group.Class;
            float col1ScreenX = screenX;
            float col1ScreenY = screenY;
            float col1Width = 0;
            float col1Height = 0;

            float topConnectorY = float.MaxValue;
            float bottomConnectorY = float.MinValue;

            List<EndPoint> startConnectors = new List<EndPoint>();

            foreach (SENode node in group.Nodes)
            {
                this.Render(childGroup, node, screenX, col1ScreenY, out float nodeWidth, out float nodeHeight);
                if (col1Width < nodeWidth)
                    col1Width = nodeWidth;

                float connectorY = col1ScreenY + nodeHeight / 2;
                if (topConnectorY > connectorY)
                    topConnectorY = connectorY;
                if (bottomConnectorY < connectorY)
                    bottomConnectorY = connectorY;
                startConnectors.Add(new EndPoint
                {
                    Location = new PointF(screenX + nodeWidth, col1ScreenY + nodeHeight / 2),
                    Annotation = node.RhsAnnotation
                });

                endConnectors.Add(new EndPoint
                {
                    Location = new PointF(screenX, col1ScreenY + nodeHeight / 2),
                    Annotation = node.LhsAnnotation
                });
                col1Height += nodeHeight + this.NodeGapY;
                col1ScreenY += nodeHeight + this.NodeGapY;
            }

            if (this.maxX < col1ScreenX + col1Width)
                this.maxX = col1ScreenX + col1Width;
            if (this.maxY < col1ScreenY)
                this.maxY = col1ScreenY;

            RenderGroupChildren(group,
                childGroup,
                screenX,
                screenY,
                col1Width,
                topConnectorY,
                bottomConnectorY,
                out colWidth,
                out colHeight,
                startConnectors);


            if (colHeight < col1Height)
                colHeight = col1Height;
        }


        void RenderGroupChildren(SENodeGroup group,
            SvgGroup childGroup,
            float screenX,
            float screenY,
            float col1Width,
            float topConnectorY,
            float bottomConnectorY,
            out float colWidth,
            out float colHeight,
            List<EndPoint> startConnectors)
        {
            colWidth = 0;
            colHeight = 0;

            float col2ScreenXStart = screenX + col1Width + this.NodeGapRhsX(group);
            float col2ScreenY = screenY;

            float col2Height = 0;
            bool endConnectorFlag = false;
            foreach (SENodeGroup child in group.ChildGroups)
            {
                float col2ScreenX = col2ScreenXStart + this.NodeGapLhsX(child);

                List<EndPoint> col2EndConnectors = new List<EndPoint>();

                this.RenderGroup(child,
                    col2ScreenX,
                    col2ScreenY,
                    out float col2GroupWidth,
                    out float col2GroupHeight,
                    col2EndConnectors);
                col2ScreenY += col2GroupHeight;
                col2Height += col2GroupHeight;

                if (startConnectors.Count > 0)
                {
                    for (Int32 i = 0; i < col2EndConnectors.Count; i++)
                    {
                        EndPoint stubEnd = col2EndConnectors[i];
                        endConnectorFlag = true;
                        float xStart = screenX + col1Width + this.NodeGapRhsX(group);
                        this.CreateArrow(childGroup,
                            false,
                            true,
                            xStart,
                            stubEnd.Location.Y,
                            stubEnd.Location.X,
                            stubEnd.Location.Y);

                        if (String.IsNullOrEmpty(stubEnd.Annotation) == false)
                        {
                            SvgText t = this.doc.AddText(childGroup);
                            t.Class = "lhsText";
                            t.X = this.ToPx(xStart + 0.25f);
                            t.Y = this.ToPx(stubEnd.Location.Y - 0.25f);
                            t.TextAnchor = "right";
                            t.Value = stubEnd.Annotation;
                        }

                        if (topConnectorY > stubEnd.Location.Y)
                            topConnectorY = stubEnd.Location.Y;
                        if (bottomConnectorY < stubEnd.Location.Y)
                            bottomConnectorY = stubEnd.Location.Y;
                    }
                }

                float width = col1Width + this.NodeGapRhsX(group) + this.NodeGapLhsX(child) + col2GroupWidth;
                if (colWidth < width)
                    colWidth = width;

                if (this.maxX < col2ScreenX + col2GroupWidth)
                    this.maxX = col2ScreenX + col2GroupWidth;
            }

            if (endConnectorFlag == true)
            {
                foreach (EndPoint stubStart in startConnectors)
                {
                    this.CreateArrow(childGroup,
                        true,
                        false,
                        stubStart.Location.X,
                        stubStart.Location.Y,
                        screenX + col1Width + this.NodeGapRhsX(group),
                        stubStart.Location.Y);

                    if (String.IsNullOrEmpty(stubStart.Annotation) == false)
                    {
                        SvgText t = this.doc.AddText(childGroup);
                        t.Class = "rhsText";
                        t.X = this.ToPx(stubStart.Location.X + 0.25f);
                        t.Y = this.ToPx(stubStart.Location.Y - 0.25f);
                        t.TextAnchor = "left";
                        t.Value = stubStart.Annotation;
                    }
                }

                // Make vertical line that connects all stubs.
                if (group.ChildGroups.Count() > 0)
                {
                    float x = screenX + col1Width + this.NodeGapRhsX(group);
                    this.CreateLine(childGroup, x, topConnectorY, x, bottomConnectorY);
                }
            }

            if (this.maxY < col2ScreenY)
                this.maxY = col2ScreenY;

            if (colHeight < col2Height)
                colHeight = col2Height;
        }






        String GetClass(params String[] cssClassNames)
        {
            foreach (String cssClassName in cssClassNames)
            {
                if (String.IsNullOrWhiteSpace(cssClassName) == false)
                    return cssClassName;
            }

            return null;
        }

        void Render(SvgGroup parentGroup,
            SENode node,
            float screenX,
            float screenY,
            out float width,
            out float height)
        {
            const float CharMod = 0.7f;

            //Debug.Assert((this.RenderTestPoint == null) || node.AllText().Contains(RenderTestPoint) == false);
            height = node.TextLines.Count * this.LineHeight + 2 * this.BorderMargin;
            width = node.Width * CharMod + 2 * this.BorderMargin;

            SvgGroup g = this.doc.AddGroup(parentGroup);
            g.Class = parentGroup.Class;
            g.Transform = $"translate({this.ToPx(screenX)} {this.ToPx(screenY)})";
            SvgRect square;

            if (node.HRef != null)
            {
                SvgHyperLink l = this.doc.AddHyperLink(g);
                l.Target = "_top";
                l.HRef = node.HRef.ToString();
                square = this.doc.AddRect(l);
            }
            else
            {
                square = this.doc.AddRect(g);
            }

            square.Class = node.Class;
            square.Stroke = Color.Black;
            square.StrokeWidth = this.ToPx(this.BorderWidth);
            square.RX = this.ToPx(this.RectRx);
            square.RY = this.ToPx(this.RectRy);
            square.X = "0";
            square.Y = "0";
            square.Width = this.ToPx(width);
            square.Height = this.ToPx(height);
            square.Fill = node.FillColor;

            float textX = this.BorderMargin;
            float textY = this.BorderMargin + 1;

            foreach (SEText line in node.TextLines)
            {
                SvgText t;
                if (line.HRef != null)
                {
                    SvgHyperLink l = this.doc.AddHyperLink(g);
                    l.HRef = line.HRef;
                    l.Target = "_top";
                    if (line.Title != null)
                    {
                        SvgTitle title = this.doc.AddTitle(l);
                        title.Value = line.Title;
                    }

                    t = this.doc.AddText(l);
                }
                else
                {
                    t = this.doc.AddText(g);
                }
                t.Class = GetClass(line.Class, node.Class);

                t.X = this.ToPx(width / 2);
                t.Y = this.ToPx(textY);
                t.TextAnchor = "middle";
                t.Value = line.Text;

                textY += this.LineHeight;
            }
        }

        public String GetXml()
        {
            return this.doc.GetXML();
        }

        public void Save(String path)
        {
            String outputDir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (Directory.Exists(outputDir) == false)
                Directory.CreateDirectory(outputDir);
            this.doc.SaveToFile(path);
            foreach (String cssFile in this.cssFiles)
            {
                String outputCssPath = Path.Combine(outputDir, Path.GetFileName(cssFile));
                if (File.Exists(outputCssPath))
                    File.Delete(outputCssPath);
                File.Copy(cssFile, Path.Combine(outputDir, Path.GetFileName(cssFile)));
            }
        }

        void CreateArrow(SvgGroup g,
            bool startMarker,
            bool endMarker,
            float xStart,
            float yStart,
            float xEnd,
            float yEnd)
        {
            SvgLine stub = this.doc.AddLine(g);
            stub.Stroke = Color.Black;
            stub.X1 = this.ToPx(xStart);
            stub.X2 = this.ToPx(xEnd);
            stub.Y1 = this.ToPx(yStart);
            stub.Y2 = this.ToPx(yEnd);
            stub.StrokeWidth = this.ToPx(this.BorderWidth);
            if (startMarker)
                stub.MarkerStart = $"url(#{ArrowStart})";
            if (endMarker)
                stub.MarkerEnd = $"url(#{ArrowEnd})";
        }

        void CreateLine(SvgGroup g, float x1, float y1, float x2, float y2)
        {
            SvgLine stub = this.doc.AddLine(g);
            stub.Stroke = Color.Black;
            stub.X1 = this.ToPx(x1);
            stub.X2 = this.ToPx(x2);
            stub.Y1 = this.ToPx(y1);
            stub.Y2 = this.ToPx(y2);
            stub.StrokeWidth = this.ToPx(this.BorderWidth);
        }


    }
}