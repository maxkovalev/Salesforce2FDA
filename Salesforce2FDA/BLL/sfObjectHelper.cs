using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Collections;
using System.Collections.Specialized;
using System.Web.Script.Serialization;




namespace Salesforce2FDA
{
    public enum sfObjectName { MDR , Complaint }
    /// <summary>
    /// Class to manipulate with loaded from configuration file SF fileds
    /// </summary>
    public class sfObjectHelper
    {
        private IEnumerable<sfField> MDRfields;
        private IEnumerable<sfField> ComplainFields;
        /// <summary>
        /// Public condtructor, load fields from config file and store in private loacal variable
        /// </summary>
        public sfObjectHelper()
        {
            MDRfields = LoadFields("MRDfiledsMapping.json");
            ComplainFields = LoadFields("ComplainFiledsMapping.json");
        }


        private IEnumerable<sfField> LoadFields(string fileName)
        {
            String jsonString = "";
            using (StreamReader sr = new StreamReader(fileName))
            {
                jsonString = sr.ReadToEnd();
            }

           
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            IEnumerable<sfField> result = serializer.Deserialize<IEnumerable<sfField>>(jsonString);
            return result;
        }

       
        /// <summary>
        /// Return coma separated fields names for quesry it from SF
        /// </summary>
        /// <returns></returns>
        public string GetCSV_MDR_Fields(string prefix)
        {
            string result = "";
            foreach (sfField f in MDRfields)
            {
                result = result + prefix + f.Name + ",";
            }
            result = result.Substring(0, result.Length - 1);
            return result;
        }

        /// <summary>
        /// Return coma separated fields names for quesry it from SF
        /// </summary>
        /// <returns></returns>
        public string GetCSV_Complain_Fields(string prefix)
        {
            string result = "";
            foreach (sfField f in ComplainFields)
            {
                result = result + prefix + f.Name + ",";
            }
            result = result.Substring(0, result.Length - 1);
            return result;
        }


        /// <summary>
        /// Return FDA code for slecific object and piklist field and field value
        /// </summary>
        /// <param name="oName">Name on the SF object</param>
        /// <param name="fieldName">Filed name of the SF Obkect</param>
        /// <param name="Value">Value of the picklist</param>
        /// <param name="error">Returned error string, if empty then no errors.</param>
        /// <returns></returns>
        public string GetCodeByValue(sfObjectName oName, string fieldName, string Value, ref string error)
        {
            error = "";
            IEnumerable<sfField> fileds=null;
            switch (oName)
            {
                case sfObjectName.MDR:
                    fileds = MDRfields;
                    break;
                case sfObjectName.Complaint:
                    fileds = ComplainFields;
                    break;
            }

            string result = null;
            sfField f = fileds.Where(p => p.Name == fieldName).FirstOrDefault();
            if (f == null)
            {
                error = String.Format("Cannot find field name '{0}' in the object '{1}'", fieldName, oName.ToString());
                return null;
            }
            sfFieldValueMapping m = f.Value2CodeMapping.Where(p => p.Value == Value).FirstOrDefault();
            if (m == null)
            {
                error = String.Format("Cannot find Value ='{0}' in the field name '{1}' in the object '{2}'",Value,  fieldName, oName.ToString());
                return null;
            }
            return m.Code;
        }

        
    }

}
