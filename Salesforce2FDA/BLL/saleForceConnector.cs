using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Services.Protocols;
using Salesforce2FDA.Sforce;
using System.Net;
using System.Configuration;
using System.Collections.Specialized;
using System.IO;

namespace Salesforce2FDA
{
    /// <summary>
    /// Class to connect to saleforce and get data from there 
    /// </summary>
    public class saleForceConnector
    {
        private SforceService binding;
        private string _userName;
        private string _password;

        public saleForceConnector(string username, string password)
        {
            _userName = username;
            _password = password;
            // Create a service object 
            binding = new SforceService();

            // Timeout after a minute 
            binding.Timeout = 60000;

            // Try logging in   
            LoginResult lr;
            try
            {
                lr = binding.login(username, password);
            }
            // ApiFault is a proxy stub generated from the WSDL contract when     
            // the web service was imported 
            catch (SoapException e)
            {
                throw new Exception(e.ToString());
            }

            // Check if the password has expired 
            if (lr.passwordExpired)
            {
                throw new Exception("An error has occurred. Your password has expired.");
            }


            /** Once the client application has logged in successfully, it will use
             * the results of the login call to reset the endpoint of the service
             * to the virtual server instance that is servicing your organization
             */
            // Save old authentication end point URL
            String authEndPoint = binding.Url;
            // Set returned service endpoint URL
            binding.Url = lr.serverUrl;

            /** The sample client application now has an instance of the SforceService
             * that is pointing to the correct endpoint. Next, the sample client
             * application sets a persistent SOAP header (to be included on all
             * subsequent calls that are made with SforceService) that contains the
             * valid sessionId for our login credentials. To do this, the sample
             * client application creates a new SessionHeader object and persist it to
             * the SforceService. Add the session ID returned from the login to the
             * session header
             */
            binding.SessionHeaderValue = new SessionHeader();
            binding.SessionHeaderValue.sessionId = lr.sessionId;
        }


        public List<CMPL123CME__MDR__c> getMDRs(string csv_fields)
       {
           List<CMPL123CME__MDR__c> result = new List<CMPL123CME__MDR__c>();

           String soqlQuery = "SELECT " + csv_fields + " FROM CMPL123CME__MDR__c WHERE (CMPL123CME__MDR_Submission__c='Not Submitted')";
           try
           {
               QueryResult qr = binding.query(soqlQuery);
               bool done = false;

               if (qr.size > 0)
               {
                   while (!done)
                   {
                       sObject[] records = qr.records;
                       for (int i = 0; i < records.Length; i++)
                       {
                           CMPL123CME__MDR__c mdr = (CMPL123CME__MDR__c)records[i];
                           result.Add(mdr);
                       }

                       if (qr.done)
                       {
                           done = true;
                       }
                       else
                       {
                           qr = binding.queryMore(qr.queryLocator);
                       }
                   }
               }
               else
               {
                   Console.WriteLine("No records found.");
               }
           }
           catch (Exception ex)
           {
               throw new Exception(string.Format("nFailed to execute query succesfully, error message was: {0}", ex.Message));
           }
           return result;
       }

        public List<CMPL123CME__Complaint__c> getComplains(string csv_fields, List<CMPL123CME__MDR__c> MDRs)
        {
            List<CMPL123CME__Complaint__c> result = new List<CMPL123CME__Complaint__c>();
            foreach (CMPL123CME__MDR__c m in MDRs)
            {
                result.Add(getComplaint(csv_fields, m.CMPL123CME__Complaint__c));
            }
            return result;
        }

        public string getMDRreportAsPDF(string mdrID)
        {
            string url = ConfigurationManager.AppSettings["saleForceReportURL"].ToString() + mdrID;

            CookieContainer cc = new CookieContainer();
            cc.Add(new Cookie("sid", binding.SessionHeaderValue.sessionId, "/", ".salesforce.com"));

            string filePath = string.Format("{0}\\reports\\{1}.pdf" ,System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location), mdrID);
            using (var client = new CookieAwareWebClient(cc))
            {
                client.DownloadFile(url, filePath);
            }

            return filePath;
        }

        private CMPL123CME__Complaint__c getComplaint(string csv_fields, string complaintID)
        {
            CMPL123CME__Complaint__c result = null;

            String soqlQuery = "SELECT " + csv_fields + " FROM CMPL123CME__Complaint__c";// WHERE id = '" + complaintID + "'";
            try
            {
                QueryResult qr = binding.query(soqlQuery);
                if (qr.size == 1)
                {
                    result = (CMPL123CME__Complaint__c)qr.records[0];
                    qr = binding.queryMore(qr.queryLocator);
                }
                else
                {
                    Console.WriteLine("No records found.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("\nFailed to execute query succesfully," +
                    "error message was: \n{0}", ex.Message);
            }
            return result;
        }


        /// <summary>
        /// Update status of submitted MDRs
        /// </summary>
        /// <param name="MRDs"></param>
        public void updateMDRstatus(List<CMPL123CME__MDR__c> MRDs)
        {
            CMPL123CME__MDR__c[] updates = new CMPL123CME__MDR__c[MRDs.Count];
            for (int i = 0; i < MRDs.Count; i++ )
            {
                CMPL123CME__MDR__c m = new CMPL123CME__MDR__c();
                m.Id = MRDs[i].Id;
                m.CMPL123CME__MDR_Submission__c = "Submitted";
                updates[i] = m;
            }
            

            // Invoke the update call and save the results
            try
            {
                SaveResult[] saveResults = binding.update(updates);
                List<string> updateErrors = new List<string>();
                foreach (SaveResult saveResult in saveResults)
                {
                    if (!saveResult.success)
                    {
                        // Handle the errors.
                        // We just print the first error out for sample purposes.
                        Error[] errors = saveResult.errors;
                        if (errors.Length > 0)
                        {
                           updateErrors.Add("Error: could not update MRD ID " + saveResult.id + ".\tThe error reported was: (" 
                                + errors[0].statusCode + ") " +
                                  errors[0].message + ".");
                        }
                    }
                    if (updateErrors.Count > 0)
                    {
                        throw new Exception(string.Join(Environment.NewLine, updateErrors.ToArray()));
                    }
                }
            }
            catch (SoapException e)
            {
                throw new Exception("An unexpected error has occurred: " + e.Message + "\n" + e.StackTrace);
            }
        }
    }
    
}
