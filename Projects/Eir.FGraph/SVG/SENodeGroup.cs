using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace FGraph
{
    [DebuggerDisplay("{this.Title} [{this.nodes.Count}/{this._childGroups.Count}]")]
    public class SENodeGroup
    {
        public String Class { get; set; }

        /// <summary>
        /// For debugging only.
        /// </summary>
        public bool ShowCardinalities
        {
            get
            {
                if (this.showCardinalities == true)
                    return this.showCardinalities;

                foreach (SENodeGroup child in this.ChildGroups)
                {
                    if (child.ShowCardinalities == true)
                        return true;
                }

                return false;
            }
            set { this.showCardinalities = value; }
        }

        bool showCardinalities = true;

        /// <summary>
        /// For debugging only.
        /// </summary>
        public String Title { get; set; }

        public IEnumerable<SENode> Nodes => this.nodes;
        List<SENode> nodes = new List<SENode>();

        public IEnumerable<SENodeGroup> ChildGroups => this._childGroups;
        List<SENodeGroup> _childGroups = new List<SENodeGroup>();

        public SENodeGroup(String title, bool showCardinalities)
        {
            //Debug.Assert(showCardinalities == true);
            this.ShowCardinalities = showCardinalities;
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
            this._childGroups.Sort((a, b) => a.Title.CompareTo(b.Title));
            foreach (SENodeGroup child in this._childGroups)
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
            this._childGroups.Add(nodeGroup);
        }

        public void AppendChildren(IEnumerable<SENodeGroup> nodeGroups)
        {
            this._childGroups.AddRange(nodeGroups);
        }

        public SENodeGroup AppendChild(String title, bool showCardinality)
        {
            SENodeGroup retVal = new SENodeGroup(title, showCardinality);
            this._childGroups.Add(retVal);
            return retVal;
        }
    }
}