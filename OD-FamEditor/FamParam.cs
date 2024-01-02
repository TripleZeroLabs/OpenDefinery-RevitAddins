using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OD_FamEditor
{
    public class FamParam
    {
        public string Name { get; set; }
        public string FamilyTypeName { get; set; }
        public string Value { get; set; }
        public string DataType { get; set; }
        public string PropGroup { get; set; }
        public bool IsShared { get; set; }
    }
}
