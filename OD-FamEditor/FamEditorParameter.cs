using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OD_FamEditor
{
    public class FamEditorParameter
    {
        public FamilyParameter FamilyParameter{ get; set; }
        public object Value { get; set; }
        public string PropGroup { get; set; }
    }
}
