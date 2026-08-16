using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace OpenDefinery
{
    /// <summary>
    /// A request to add one OpenDefinery parameter to a PROJECT as a project parameter, bound to one
    /// or more model categories under a specific parameter group and instance/type binding. Consumed
    /// by <see cref="RvtConnector.CreateProjectParams"/>.
    /// </summary>
    public class ProjectParamRequest
    {
        public DefineryParameter Param { get; set; }

        /// <summary>Target built-in parameter group. Null falls back to Identity Data.</summary>
        public ForgeTypeId Group { get; set; }

        /// <summary>True = Instance binding, false = Type binding.</summary>
        public bool IsInstance { get; set; }

        /// <summary>The model categories the parameter is bound to.</summary>
        public List<Category> Categories { get; set; }
    }
}
