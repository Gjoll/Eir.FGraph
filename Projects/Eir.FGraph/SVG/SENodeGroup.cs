using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace FGraph
{
    [DebuggerDisplay("{this.Title} [{this.nodes.Count}/{this.childGroups.Count}]")]
    public class SENodeGroup
    {
        Int32 maxLhsAnnotation = -1;
        Int32 maxRhsAnnotation = -1;

        public String Class { get; set; }
        public String SortPrefix { get; set; } = "";
        public String Title { get; set; }
        public List<SENode> Nodes { get; set; } = new List<SENode>();
        public List<SENodeGroup> ChildGroups { get; set; } = new List<SENodeGroup>();

        public String SortTitle => $"{this.SortPrefix}{this.Title}";
        public Int32 MaxRhsAnnotation()
        {
            if (this.maxRhsAnnotation == -1)
            {
                this.maxRhsAnnotation = 0;
                if (this.Nodes.Count > 0)
                {
                    foreach (SENode node in this.Nodes)
                    {
                        if (
                            (node.RhsAnnotation != null) &&
                            (node.RhsAnnotation.Length > this.maxRhsAnnotation)
                        )
                            this.maxRhsAnnotation = node.RhsAnnotation.Length;
                    }
                }
                else
                {
                    foreach (SENodeGroup group in this.ChildGroups)
                    {
                        Int32 groupMaxRhsAnnotation = group.MaxRhsAnnotation();
                        if (groupMaxRhsAnnotation > this.maxRhsAnnotation)
                            this.maxRhsAnnotation = groupMaxRhsAnnotation;
                    }
                }
            }
            return this.maxRhsAnnotation;
        }

        public Int32 MaxLhsAnnotation()
        {
            if (this.maxLhsAnnotation == -1)
            {
                this.maxLhsAnnotation = 0;
                if (this.Nodes.Count > 0)
                {
                    foreach (SENode node in this.Nodes)
                    {
                        if (
                            (node.LhsAnnotation != null) &&
                            (node.LhsAnnotation.Length > this.maxLhsAnnotation)
                        )
                            this.maxLhsAnnotation = node.LhsAnnotation.Length;
                    }
                }
                else
                {
                    foreach (SENodeGroup group in this.ChildGroups)
                    {
                        Int32 groupMaxLhsAnnotation = group.MaxLhsAnnotation();
                        if (groupMaxLhsAnnotation > this.maxLhsAnnotation)
                            this.maxLhsAnnotation = groupMaxLhsAnnotation;
                    }
                }
            }
            return this.maxLhsAnnotation;
        }

        public SENodeGroup(String sortPrefix, String title)
        {
            this.SortPrefix = sortPrefix;
            if (title == null)
                throw new Exception("Title must be non empty for sorting");
            this.Title = title;
        }

        /// <summary>
        /// Sorts the nodes and groups, and calls sort on each child node.
        /// </summary>
        public void Sort()
        {
            this.Nodes.Sort((a, b) => a.SortText.CompareTo(b.SortText));
            this.ChildGroups.Sort((a, b) => a.SortTitle.CompareTo(b.SortTitle));
            foreach (SENodeGroup child in this.ChildGroups)
                child.Sort();
        }

        public void AppendNode(SENode node)
        {
            this.Nodes.Add(node);
        }

        public void AppendNodeRange(IEnumerable<SENode> nodes)
        {
            foreach (SENode node in nodes)
                this.AppendNode(node);
        }

        public void AppendGroup(SENodeGroup nodeGroup)
        {
            this.ChildGroups.Add(nodeGroup);
        }

        public void AppendGroupRange(IEnumerable<SENodeGroup> nodeGroups)
        {
            foreach (SENodeGroup nodeGroup in nodeGroups)
                this.AppendGroup(nodeGroup);
        }
    }
}