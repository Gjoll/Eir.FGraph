using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FGraph
{
    public static class StringBuilderExtensions
    {
        public static void DumpString(this StringBuilder sb, String margin, String name, String value)
        {
            if (String.IsNullOrEmpty(value))
                return;
            sb.AppendLine($"{margin}{name} = '{value}'");
        }
    }
}
