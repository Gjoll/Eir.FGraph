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
        List<SENode> nodes = new List<SENode>();
        List<SENodeGroup> childGroups = new List<SENodeGroup>();

        public String Class { get; set; }
        public String Title { get; set; }
        public IEnumerable<SENode> Nodes => this.nodes;
        public IEnumerable<SENodeGroup> ChildGroups => this.childGroups;
        public Int32 MaxRhsAnnotation()
        {
            if (this.maxRhsAnnotation == -1)
            {
                this.maxRhsAnnotation = 0;
                if (this.nodes.Count > 0)
                {
                    foreach (SENode node in this.nodes)
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
                if (this.nodes.Count > 0)
                {
                    foreach (SENode node in this.nodes)
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

        public SENodeGroup(String title)
        {
            if (title == null)
                throw new Exception("Title must be non empty for sorting");
            this.Title = title;
        }

        /// <summary>
        /// Sorts the nodes and groups, and calls sort on each child node.
        /// </summary>
        public void Sort()
        {
            this.nodes.Sort((a, b) => a.AllText().CompareTo(b.AllText()));
            this.childGroups.Sort((a, b) => a.Title.CompareTo(b.Title));
            foreach (SENodeGroup child in this.childGroups)
                child.Sort();
        }

        public void AppendNode(SENode node)
        {
            this.nodes.Add(node);
        }

        public void AppendNodes(IEnumerable<SENode> nodes)
        {
            foreach (SENode node in nodes)
                this.AppendNode(node);
        }

        public void AppendChild(SENodeGroup nodeGroup)
        {
            this.childGroups.Add(nodeGroup);
        }

        public void AppendChildren(IEnumerable<SENodeGroup> nodeGroups)
        {
            this.childGroups.AddRange(nodeGroups);
        }

        public SENodeGroup AppendChild(String title)
        {
            SENodeGroup retVal = new SENodeGroup(title);
            this.childGroups.Add(retVal);
            return retVal;
        }
    }
}