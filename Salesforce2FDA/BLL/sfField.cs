using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Salesforce2FDA
{
    /// <summary>
    /// CLass represent Sale Force fileds that shoudl be mapped to HL7 XML for FDA
    /// </summary>
    public class sfField
    {
        public string Name { get; set; }
        public IEnumerable<sfFieldValueMapping> Value2CodeMapping { get; set; }

        /// <summary>
        /// Return cade by vale for secific field
        /// </summary>
        /// <param name="value">Value of the filed</param>
        /// <returns>FDA code where that value mapped</returns>
        public string GetCodeByValue(string value)
        {
            sfFieldValueMapping entry = Value2CodeMapping.Where(p => p.Value == value).FirstOrDefault();

            if (entry == null)
                throw new Exception(string.Format("Cannot find FDA code for SF field {0} SF value {1}", this.Name, value));
            else
                return entry.Code;
        }
    }
}
