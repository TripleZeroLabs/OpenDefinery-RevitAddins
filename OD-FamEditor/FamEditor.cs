using Autodesk.Revit.DB;
using OpenDefinery;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OD_FamEditor
{
    public class FamEditor
    {
        public RvtConnector RvtConnector { get; set; }
        public Document Doc { get; set; }
        public Dictionary<string, List<FamilyParameter>> FamilyParams { get; set; }
        public List<FamilyType> FamilyTypes { get; set; }

        /// <summary>
        /// Loads all Family Type and Parameter data from the current Revit family
        /// </summary>
        /// <param name="famEditor"></param>
        /// <returns>Debug information</returns>
        public FamilyTypeSet LoadData()
        {
            // String for showing debug information in the UI
            //var debugOutput = "";

            // Instantiate the Family Manager to begin editing the family
            var fm = this.Doc.FamilyManager;

            var currentType = fm.CurrentType;

            // Get all Parameters and set the list
            var allParameters = fm.Parameters;
            //this.FamilyParameters = fm.GetParameters().ToList();

            int n = allParameters.Size;

            Dictionary<string, FamilyParameter> fps
              = new Dictionary<string, FamilyParameter>(n);

            foreach (FamilyParameter fp in allParameters)
            {
                string name = fp.Definition.Name;
                fps.Add(name, fp);
            }

            List<string> keys = new List<string>(fps.Keys);
            keys.Sort();

            // Set the Family Types
            this.FamilyTypes = new List<FamilyType>();

            foreach (FamilyType t in fm.Types)
            {
                this.FamilyTypes.Add(t);

                //// Generate debugging information
                //string typeName = t.Name;
                //debugOutput += string.Format("\n  {0}:", typeName);
                //foreach (string key in keys)
                //{
                //    FamilyParameter fp = fps[key];
                //    if (t.HasValue(fp))
                //    {
                //        debugOutput += string.Format(
                //            "\n    {0} = {1}", key, FamilyParamValueString(t, fp, this.Doc)
                //            );
                //    }
                //}
            }

            // If the family has no types named, the enumeration is 0
            // If the family has at least one type named, there may be a blank type with name ""

            //return debugOutput;
            return fm.Types;
        }


        /// <summary>
        /// Retrieve all Parameters within the current Family and group them by type.
        /// </summary>
        public void GetFamParams()
        {
            var allParameters = this.Doc.FamilyManager.Parameters;

            int n = allParameters.Size;

            Dictionary<string, List<FamilyParameter>> familyParams
              = new Dictionary<string, List<FamilyParameter>>(n);

            // Add each parameter to the dictionary
            foreach (FamilyParameter fp in allParameters)
            {
                // The FamilyParameter has the Family Type identified as it's Definition
                string name = fp.Definition.Name;

                // If the Family Type already exists, add the Parameter to the list
                if (familyParams.Keys.Contains(name))
                {
                    familyParams[name].Add(fp);
                }
                // If it is a new Family Type, create the list of Parameters 
                else
                {
                    var newParamList = new List<FamilyParameter>();
                    newParamList.Add(fp);
                    familyParams.Add(name, newParamList);
                }
            }

            List<string> keys = new List<string>(familyParams.Keys);
            keys.Sort();

            this.FamilyParams = familyParams;
        }

        public static object FamilyParamValue(
          FamilyType t,
          FamilyParameter fp,
          Document doc)
        {
            object value = t.AsValueString(fp);

            switch (fp.StorageType)
            {
                case StorageType.Double:
                    var doubleValue =
                      (double)t.AsDouble(fp);

                    value = doubleValue;

                    break;

                case StorageType.ElementId:
                    //if (fp.Definition.ParameterType == ParameterType.Image)
                    //{
                    //    value = "{{ Image }}";
                    //}
                    //else if (fp.Definition.ParameterType == ParameterType.Material)
                    //{
                    //    value = "{{ Material }}";
                    //}
                    //else
                    //{
                    //    ElementId id = t.AsElementId(fp);
                    //    Element e = doc.GetElement(id);
                    //    value = id.ToString();
                    //}

                    ElementId id = t.AsElementId(fp);
                    value = id.ToString();

                    break;

                case StorageType.Integer:
                    if (fp.Definition.ParameterType == ParameterType.YesNo)
                    {
                        var intValue = t.AsInteger(fp).ToString();

                        if (intValue == "1")
                        {
                            value = true;
                        }
                        else if (intValue == "0")
                        {
                            value = false;
                        }
                        else
                        {
                            value = null;
                        }
                    }
                    else
                    {
                        value = t.AsInteger(fp).ToString();
                    }

                    break;

                case StorageType.String:
                    value = t.AsString(fp);

                    break;
            }
            return value;
        }


        /// <summary>
        /// Helper method to cast any Parameter datatype to a string
        /// </summary>
        /// <param name="t"></param>
        /// <param name="fp"></param>
        /// <param name="doc"></param>
        /// <returns></returns>
        public static string FamilyParamValueString(
          FamilyType t,
          FamilyParameter fp,
          Document doc)
        {
            string value = t.AsValueString(fp);

            switch (fp.StorageType)
            {
                case StorageType.Double:
                    var doubleValue =
                      (double)t.AsDouble(fp);

                    value = doubleValue.ToString();

                    break;

                case StorageType.ElementId:
                    //if (fp.Definition.ParameterType == ParameterType.Image)
                    //{
                    //    value = "Image";
                    //}
                    //else if (fp.Definition.ParameterType == ParameterType.Material)
                    //{
                    //    value = "Material";
                    //}
                    //else
                    //{
                    //    ElementId id = t.AsElementId(fp);
                    //    Element e = doc.GetElement(id);
                    //    value = id.ToString();
                    //}

                    ElementId id = t.AsElementId(fp);
                    value = id.ToString();

                    break;

                case StorageType.Integer:
                    if (fp.Definition.ParameterType == ParameterType.YesNo)
                    {
                        var intValue = t.AsInteger(fp).ToString();

                        if (intValue == "1")
                        {
                            value = "YES";
                        }
                        else if (intValue == "0")
                        {
                            value = "NO";
                        }
                        else
                        {
                            value = "NULL";
                        }
                    }
                    else
                    {
                        value = t.AsInteger(fp).ToString();
                    }

                    break;

                case StorageType.String:
                    value = t.AsString(fp);

                    break;
            }
            return value;
        }
    }

    public class FamEditorParameter
    {
        public FamilyParameter FamilyParameter { get; set; }
        public object Value { get; set; }
        public string PropGroup { get; set; }
    }
}
