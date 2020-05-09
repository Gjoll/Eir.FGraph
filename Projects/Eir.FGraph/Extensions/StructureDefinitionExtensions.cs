using System;
using System.Collections.Generic;
using System.Text;
using Eir.DevTools;
using Hl7.Fhir.Model;

namespace FGraph
{
    static class StructureDefinitionExtensions
    {
        public static ElementDefinition FindElement(this IEnumerable<ElementDefinition> elements, String id)
        {
            foreach (ElementDefinition e in elements)
            {
                if (e.ElementId == id)
                    return e;
            }
            return null;
        }

        /// <summary>
        /// Find snapnode element.
        /// id is a typical fhir ElementDefinition.id except that the first part (up to first '.')
        /// may be the name of the profile, not he name of the base class.
        /// Replace with the name of the base class.
        /// </summary>
        public static ElementDefinition FindSnapElement(this StructureDefinition sd, String id)
        {
            String baseName = sd.BaseDefinition.LastUriPart();
            Int32 index = id.IndexOf('.');
            String normName;
            if (index > 0)
                normName = baseName + id.Substring(index);
            else
                normName = baseName;
            return sd.Snapshot.Element.FindElement(normName);
        }

        /// <summary>
        /// Find snapnode element.
        /// id is a typical fhir ElementDefinition.id except that the first part (part containing base name)
        /// is missing.
        /// </summary>
        public static ElementDefinition FindSnapElementShortName(this StructureDefinition sd, String id)
        {
            String baseName = sd.BaseDefinition.LastUriPart();
            String normName = $"{baseName}.{id}";
            return sd.Snapshot.Element.FindElement(normName);
        }

        public static ElementDefinition FindDiffElement(this StructureDefinition sd, String id)
        {
            String baseName = sd.BaseDefinition.LastUriPart();
            Int32 index = id.IndexOf('.');
            String normName;
            if (index > 0)
                normName = baseName + id.Substring(index);
            else
                normName = baseName;
            return sd.Differential.Element.FindElement(normName);
        }
    }
}
