using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Configuration;
using Salesforce2FDA.Sforce;
using System.IO;

namespace Salesforce2FDA
{
    /// <summary>
    /// CLass that conect to FDA and send prepared XML there
    /// </summary>
    public class fdaConnector
    {
        private saleForceConnector _sConn;
        private List<CMPL123CME__MDR__c> _mdrs;
        private sfObjectHelper _sfOHelper;
        private List<string> _errorLog;

        public fdaConnector(saleForceConnector sConn, List<CMPL123CME__MDR__c> mdrs, sfObjectHelper sfOHelper)
        {
            _errorLog = new List<string>();
            _sConn = sConn;
            _sfOHelper = sfOHelper;
            _mdrs = mdrs;
        }

        /// <summary>
        /// Rerurn resutl XML as a string to send to FDA
        /// </summary>
        /// <returns></returns>
        public string getXML()
        {

            XDocument doc = XDocument.Load("TemplateBody.xml");
            var ns = doc.Root.Name.Namespace;
            //insert sender
            XElement element = doc.Descendants(ns + "id").ToList().Where(x => x.Attribute("assigningAuthorityName").Value == "MessageSender").FirstOrDefault();
            element.SetAttributeValue("extension", getExtension());

            //insert creationTime
            doc.Element(ns + "PORR_IN040001UV01").Element(ns + "creationTime").SetAttributeValue("value", DateTime.Now.ToString("yyyyMMdd"));

            //inset batchTotalNumber
            doc.Element(ns + "PORR_IN040001UV01").Element(ns + "batchTotalNumber").SetAttributeValue("value", _mdrs.Count);

            string result = doc.ToString();
            string messages = getMessages(_mdrs);


            result = result.Replace("</PORR_IN040001UV01>", messages + "</PORR_IN040001UV01>");
            return result;
        }



        private string getExtension()
        {
            return string.Format("{0}-{1}", ConfigurationManager.AppSettings["CentralFileNumber"], DateTime.Now.ToString("yyyyMMddhhmmss"));
        }

        private string getMessages(List<CMPL123CME__MDR__c> mdrs)
        {
            int messageID = 1;
            string result = "";
            foreach (CMPL123CME__MDR__c m in mdrs)
            {
                XDocument doc = XDocument.Load("TemplateMessage.xml");
                var ns = doc.Root.Name.Namespace;
                //Insert message ID
                XElement element = doc.Descendants(ns + "id").ToList().Where(x => x.Attribute("extension").Value == "1").FirstOrDefault();
                element.SetAttributeValue("extension", messageID.ToString());

                //insert PDF document
                element = doc.Descendants(ns + "attachment").FirstOrDefault();
                element.Element(ns + "text").SetValue(getPDFReportAsBase64(m.Id));
                element.Element(ns + "text").Add( new XElement(ns + "reference",new XAttribute("value",m.Id + ".pdf")));

                string error = "";
                //fill the rest of info
                XElement element_controlActProcess = doc.Descendants(ns + "controlActProcess").FirstOrDefault();
                if (m.CMPL123CME__Report_Date__c.HasValue)
                    element_controlActProcess.Element(ns + "effectiveTime").SetAttributeValue("value", m.CMPL123CME__Report_Date__c.Value.ToString("yyyyMMdd"));
                else
                {
                    _errorLog.Add("'Date of this report' cannot be null. MDR " + m.Name + " has not been send.");
                }
                string code = "";
                //Fill Type_of_Reporter TODO
                /*
                string code = _sfOHelper.GetCodeByValue(sfObjectName.Complaint, "CMPL__Reporter_Type__c", m.CMPL123CME__Complaint__r.CMPL__Reporter_Type__c, ref error);
                if (!string.IsNullOrEmpty(error))
                {
                    _errorLog.Add(error);
                    error = "";
                }
                else
                    element_controlActProcess.Element(ns + "authorOrPerformer")
                        .Element(ns + "assignedPerson").Element(ns + "code").SetAttributeValue("code", code);
                 */

                //Fill CMPL123CME__Mfg_Report_No__c
                XElement element_investigationEvent = element_controlActProcess.Element(ns + "subject").Element(ns + "investigationEvent");
                element_investigationEvent.Element(ns + "id").SetAttributeValue("extension", m.CMPL123CME__Mfg_Report_No__c ?? string.Empty);

                //Fill  Additional Manufacturer Narrative, BOX H10
                element_investigationEvent.Element(ns + "text").SetValue(m.CMPL123CME__Addtl_Mfg_Narrative__c ?? string.Empty);

                //Fill Describe Event or Problem, BOX B5
                element_investigationEvent.Element(ns + "trigger")
                    .Element(ns + "reaction").Element(ns + "text").SetValue(m.CMPL123CME__Describe_Event__c ?? string.Empty);

                //Fill Date of Event, BOX B3 
                if (m.CMPL123CME__Date_of_Event__c.HasValue)
                    element_investigationEvent.Element(ns + "trigger")
                        .Element(ns + "reaction").Element(ns + "effectiveTime").SetAttributeValue("value", m.CMPL123CME__Date_of_Event__c.Value.ToString("yyyyMMdd"));

                //fill <!-- Patient Identifier, BOX A1 -->
                XElement element_investigativeSubject = element_investigationEvent.Element(ns + "trigger")
                        .Element(ns + "reaction").Element(ns + "subject").Element(ns + "investigativeSubject");
                element_investigativeSubject.Element(ns + "subjectAffectedPerson").Element(ns + "name").SetValue(m.CMPL123CME__Patient_ID__c ?? string.Empty);

                //fill <!-- Sex, BOX A3, Code is populated with Male-->
                code = _sfOHelper.GetCodeByValue(sfObjectName.MDR, "CMPL123CME__Sex__c", m.CMPL123CME__Sex__c, ref error);
                if (!string.IsNullOrEmpty(error))
                {
                    _errorLog.Add(error);
                    error = "";
                }
                else
                    element_investigativeSubject.Element(ns + "subjectAffectedPerson")
                        .Element(ns + "administrativeGenderCode").SetAttributeValue("code", code);

                //fill <!-- Date of Birth, BOX A2 -->
                if (m.CMPL123CME__DOB__c.HasValue)
                    element_investigativeSubject.Element(ns + "subjectAffectedPerson")
                        .Element(ns + "birthTime").SetAttributeValue("value", m.CMPL123CME__DOB__c.Value.ToString("yyyyMMdd"));

                //fill<!-- Outcomes Attributed to Adverse Event\Death Date, BOX B2  -->
                if (m.CMPL123CME__Date_of_Death__c.HasValue)
                    element_investigativeSubject.Element(ns + "subjectAffectedPerson")
                        .Element(ns + "deceasedTime").SetAttributeValue("value", m.CMPL123CME__Date_of_Death__c.Value.ToString("yyyyMMdd"));

                //fill <!-- Other Relevant History, Including Preexisting Medical Conditions\Race, BOX B7 --> TODO
                /*code = _sfOHelper.GetCodeByValue(sfObjectName.MDR, "CMPL__Patient_Race__c", m.CMPL__Patient_Race__c, ref error);
                if (!string.IsNullOrEmpty(error))
                {
                    _errorLog.Add(error);
                    error = "";
                }
                else
                    element_investigativeSubject.Element(ns + "subjectAffectedPerson")
                        .Element(ns + "raceCode").SetAttributeValue("code", code);*/

                //Fill <!-- Weight, A4 -->
                element = element_investigativeSubject.Descendants(ns + "subjectOf").ToList().Where(x => x.Element(ns + "observation").
                    Element(ns +"code").Attribute("codeSystemName").Value == "Weight").FirstOrDefault();
                element.Element(ns + "observation").Element(ns + "value").SetAttributeValue("value", m.CMPL123CME__Weight__c.HasValue ? m.CMPL123CME__Weight__c.Value.ToString("0") : string.Empty);
                element.Element(ns + "observation").Element(ns + "value").SetAttributeValue("unit", m.CMPL123CME__Weight_Units_Patient__c ?? string.Empty);

                //fill <!-- Relevant Tests/Labratory Data, Including Dates, BOX B6 -->
                element = element_investigativeSubject.Descendants(ns + "subjectOf").ToList().Where(x => x.Element(ns + "observation").
                    Element(ns +"code").Attribute("codeSystemName").Value == "Test_Result").FirstOrDefault();
                element.Element(ns + "observation").Element(ns + "value").SetValue(m.CMPL123CME__Relevant_tests_and_lab_data__c ?? string.Empty);

                //fill <!-- Other Relevant History, Including Preexisting Medical Conditions\Race, BOX B7 -->
                element = element_investigativeSubject.Descendants(ns + "subjectOf").ToList().Where(x => x.Element(ns + "observation").
                    Element(ns +"code").Attribute("codeSystemName").Value == "Other_Personal_Medical_History").FirstOrDefault();
                element.Element(ns + "observation").Element(ns + "value").SetValue(m.CMPL123CME__Other_Relevant_History__c ?? string.Empty);
                
                //fill <!-- Event Problem Codes\Patient Problem Codes, BOX F10 --> TODO cannot find data for now

                //fill <!-- Location where Event Occurred, BOX F12 -->
                element_investigationEvent.Element(ns + "trigger").Element(ns + "reaction")
                    .Element(ns + "location").Element(ns + "locatedEntity").Element(ns + "location")
                    .Element(ns + "code").Element(ns + "originalText").SetValue(m.CMPL123CME__Loc_where_event_occurred__c ?? string.Empty);
                
                //fill <!-- Initial Reporter Also Sent Report To FDA? BOX E.4 -->
                XElement element_receiver = element_investigationEvent.Element(ns + "trigger").Element(ns + "reaction")
                    .Element(ns + "pertinentInformation").Element(ns + "primarySourceReport").Element(ns + "receiver");
                
                code = _sfOHelper.GetCodeByValue(sfObjectName.MDR, "CMPL123CME__Initial_Report_sent_to_FDA__c", m.CMPL123CME__Initial_Report_sent_to_FDA__c, ref error);
                if (code == null)
                {
                    element_receiver.Attributes().Remove();
                    element_receiver.Add(new XAttribute("nullFlavor","ASKU"));
                }
                else
                    element_receiver.SetAttributeValue("negationInd",code);

                //Fill <!-- Initial Reporter Occupation, BOX E3 -->
                XElement element_assignedEntity = element_investigationEvent.Element(ns + "trigger").Element(ns + "reaction")
                    .Element(ns + "pertinentInformation").Element(ns + "primarySourceReport").Element(ns + "author").Element(ns + "assignedEntity");

                element_assignedEntity.Element(ns + "code").Element(ns + "originalText").SetValue(m.CMPL123CME__Occupation__c ?? string.Empty);
                   
                //fill <!-- Initial Reporter prefix, BOX E1 --> TODO

                //fill pertinentInformation1 first entry
                XElement element_pertinentInformation1_First = element_investigationEvent.Descendants(ns + "pertinentInformation1").ToList()[0];
                //fill <!-- Follow-up Number if Follow-up is selected in BOX F7 -->
                element_pertinentInformation1_First.Element(ns + "sequenceNumber").SetAttributeValue("value", m.CMPL123CME__Follow_up_No__c ?? "0");
                
                //Fill <!-- UF/Importer Report Number, BOX F2 FDA OID is used here temporarily-->
                element_pertinentInformation1_First.Element(ns + "secondaryCaseNotification").Element(ns + "id").SetAttributeValue("extension", m.CMPL123CME__UF_Dist_Report_No__c ?? "NA");

                //Fill <!-- Type of Report, BOX F7 Populated with Initial Report of An Adverse Event -->
                code = _sfOHelper.GetCodeByValue(sfObjectName.MDR, "CMPL123CME__Type_of_Report__c", m.CMPL123CME__Type_of_Report__c, ref error);
                if (!string.IsNullOrEmpty(error))
                {
                    _errorLog.Add(error);
                    error = "";
                }
                else
                    element_pertinentInformation1_First.Element(ns + "secondaryCaseNotification").Element(ns + "code").SetAttributeValue("code", code);
                
                //fill <!-- Date User Facility or Importer Became Aware of Event, BOX F6 -->
                if (m.CMPL123CME__Date_facility_dist_was_aware__c.HasValue)
                    element_pertinentInformation1_First.Element(ns + "secondaryCaseNotification").Element(ns + "effectiveTime")
                        .SetAttributeValue("value", m.CMPL123CME__Date_facility_dist_was_aware__c.Value.ToString("yyyyMMdd"));

                //Fill  <!-- Report Sent to FDA?, BOX F11 -->
                if (m.CMPL123CME__Date_report_sent_to_FDA__c.HasValue)
                {
                    element_pertinentInformation1_First.Element(ns + "secondaryCaseNotification").Element(ns + "receiver").SetAttributeValue("negationInd", "true");
                    element_pertinentInformation1_First.Element(ns + "secondaryCaseNotification").Element(ns + "receiver")
                        .Element(ns + "time").SetAttributeValue("value", m.CMPL123CME__Date_report_sent_to_FDA__c.Value.ToString("yyyyMMdd"));
                }
                else
                {
                    element_pertinentInformation1_First.Element(ns + "secondaryCaseNotification").Element(ns + "receiver").SetAttributeValue("negationInd", "false");
                }

                //Fill <!-- Report from User Facility or Importer, BOX F1 --> TODO

                //Fill <pertinentInformation1> seconf node
                //fill pertinentInformation1 first entry
                XElement element_pertinentInformation1_second = element_investigationEvent.Descendants(ns + "pertinentInformation1").ToList()[1];

                 //fill<!-- Report Sent to Manufacturer? BOX F13 -->
                if (m.CMPL123CME__Report_sent_to_MFG__c.HasValue && m.CMPL123CME__Report_sent_to_MFG__c.Value)
                    element_pertinentInformation1_second.Element(ns + "secondaryCaseNotification").Element(ns + "receiver").SetAttributeValue("negationInd", "true");
                else
                    element_pertinentInformation1_second.Element(ns + "secondaryCaseNotification").Element(ns + "receiver").SetAttributeValue("negationInd", "false");

                //fill <!-- Report sent to Manufacturer\YES, populate date below, BOX F13 -->
                if (m.CMPL123CME__Date_report_sent_to_MFG__c.HasValue)
                    element_pertinentInformation1_second.Element(ns + "secondaryCaseNotification").Element(ns + "receiver")
                        .Element(ns + "time").Element(ns + "low").SetAttributeValue("value", m.CMPL123CME__Date_report_sent_to_MFG__c.Value.ToString("yyyyMMdd"));
                //fill <!-- Date Received by Manufacturer, BOX G4 -->
                if (m.CMPL123CME__Date_received_by_MFG__c.HasValue)
                    element_pertinentInformation1_second.Element(ns + "secondaryCaseNotification").Element(ns + "receiver")
                        .Element(ns + "time").Element(ns + "high").SetAttributeValue("value", m.CMPL123CME__Date_received_by_MFG__c.Value.ToString("yyyyMMdd"));

                // TODO F14 shoudl be break down

                XElement element_pertinentInformation2 = element_investigationEvent.Element(ns + "pertinentInformation2");
                //fill <!-- Outcomes Attributed to Adverse Event, BOX B2 -->

                if (!string.IsNullOrEmpty(m.CMPL123CME__Outcomes_attributed_to_AE__c))
                {
                    string firstValue="";
                    int deviderIndex = m.CMPL123CME__Outcomes_attributed_to_AE__c.IndexOf(";");
                    if (deviderIndex == -1)
                        firstValue = m.CMPL123CME__Outcomes_attributed_to_AE__c;
                    else
                        firstValue = m.CMPL123CME__Outcomes_attributed_to_AE__c.Substring(0,deviderIndex);

                    code = _sfOHelper.GetCodeByValue(sfObjectName.MDR, "CMPL123CME__Outcomes_attributed_to_AE__c", firstValue, ref error);
                    if (!string.IsNullOrEmpty(error))
                    {
                        _errorLog.Add(error);
                        error = "";
                    }
                    else
                        element_pertinentInformation2.Element(ns + "caseSeriousness").Element(ns + "value")
                            .SetAttributeValue("code", code);
                }

                
                XElement element_identifiedDevice = element_investigationEvent.Element(ns + "pertainsTo")
                    .Element(ns + "procedureEvent").Element(ns + "device").Element(ns+ "identifiedDevice");
                //fill <!-- Device Other Number, BOX D4-->
                element_identifiedDevice.Element(ns + "id").SetAttributeValue("extension",m.CMPL123CME__Other_No__c ?? string.Empty);
                
                //fill<!-- Device Serial Number, BOX D4  -->
                element_identifiedDevice.Element(ns + "identifiedDevice").Element(ns + "id").SetAttributeValue("extension", m.CMPL123CME__Serial_No__c ?? string.Empty);

                //fill <!-- Device Manufacture Date, BOX H4 -->
                if (m.CMPL123CME__Device_mfg_date__c.HasValue)
                    element_identifiedDevice.Element(ns + "identifiedDevice").Element(ns + "existenceTime").SetAttributeValue("value", m.CMPL123CME__Device_mfg_date__c.Value.ToString("yyyyMMdd"));

                //fill <!-- Device Lot Number, BOX D4 -->
                element_identifiedDevice.Element(ns + "identifiedDevice").Element(ns + "lotNumberText").SetValue(m.CMPL123CME__Lot_No__c ?? string.Empty);

                //fill <!-- Device Expiration Date, BOX D4 -->
                if (m.CMPL123CME__Expiration_Date__c.HasValue)
                    element_identifiedDevice.Element(ns + "identifiedDevice").Element(ns + "expirationTime").SetAttributeValue("value", m.CMPL123CME__Expiration_Date__c.Value.ToString("yyyyMMdd"));

                //fill <!-- Device Catalog Number, BOX D4 -->
                element_identifiedDevice.Element(ns + "identifiedDevice").Element(ns + "asManufacturedProduct")
                    .Element(ns + "id").SetAttributeValue("extension", m.CMPL123CME__Catalog_No__c ?? string.Empty);

                //TODO <!-- Manufacturer Name, City and State\Name, BOX D3 --> needs break down
                //TODO <!-- Evaluation Codes\Results, BOX H6 --> needs to implemnted
                //TODO <!-- Contact Office - Name\Address\Facility, BOX G1 --> G1 address neesd to break down
                //TODO
                /*      
                 *      <!-- Please submit MDR CONTACT information as the first address or address one -->
                        <!-- Please submit MANUFACTURING SITE information as the second address or address two  -->
                */

                //<!-- Manufacturer Phone Number, BOX G2 -->
                XElement element_asManufacturedProduct_First = element_identifiedDevice.Descendants(ns + "asManufacturedProduct").ToList()[0];
                element_asManufacturedProduct_First.Element(ns + "manufacturerOrReprocessor").Element(ns + "contactParty")
                    .Descendants(ns + "telecom").ToList()[2].SetAttributeValue("value", "tel:" + m.CMPL123CME__Mfg_Phone_Number__c ?? string.Empty);

                /*
                 *  TODO <!-- The manufacturer or Reprocessor element can appear twice -->
                      <!-- The second instance, within this example, refers to the reprocessor, BOX D9  -->*/

                XElement element_asManufacturedProduct_Second = element_identifiedDevice.Descendants(ns + "asManufacturedProduct").ToList()[1];

                //fill <!-- Device Model Number, BOX D4 -->
                element_identifiedDevice.Element(ns + "identifiedDevice").Element(ns + "inventoryItem")
                    .Element(ns + "manufacturedDeviceModel").Element(ns + "id").SetAttributeValue("extension", m.CMPL123CME__Model_No__c ?? string.Empty);

                //fill <!-- Common Device Name\Product Code, BOX D2 Populate with MAUDE Code FRN -->
                element_identifiedDevice.Element(ns + "identifiedDevice").Element(ns + "inventoryItem")
                    .Element(ns + "manufacturedDeviceModel").Element(ns + "code")
                    .Element(ns + "originalText").SetValue(m.CMPL123CME__Common_Device_Name__c ?? string.Empty);

                // fill <!-- Brand Name, BOX D1 -->
                element_identifiedDevice.Element(ns + "identifiedDevice").Element(ns + "inventoryItem")
                    .Element(ns + "manufacturedDeviceModel").Element(ns + "manufacturerModelName").SetValue(m.CMPL123CME__Brand_Name__c ?? string.Empty);

                //fill <!-- Premarket Number, BOX G5 -->
                element_identifiedDevice.Element(ns + "identifiedDevice").Element(ns + "inventoryItem")
                    .Element(ns + "manufacturedDeviceModel").Element(ns + "asRegulatedProduct")
                    .Element(ns + "id").SetAttributeValue("extension",m.CMPL123CME__PMA_510_k__c ?? string.Empty);

                //fill <!-- Device Available for Evaluation?, BOX D10  -->
                XElement element_subjectOf = element_identifiedDevice.Descendants(ns + "subjectOf").ToList()
                        .Where(p => p.Element(ns + "deviceObservation").Element(ns + "code")
                            .Attribute("codeSystemName").Value == "Device_available_for_evaluation").FirstOrDefault();

                code = _sfOHelper.GetCodeByValue(sfObjectName.MDR, "CMPL123CME__Device_available_for_Eval__c", m.CMPL123CME__Device_available_for_Eval__c, ref error);
                if (!string.IsNullOrEmpty(error))
                {
                    _errorLog.Add(error);
                    error = "";
                }
                else
                    element_subjectOf.Element(ns + "deviceObservation").Element(ns + "value").SetAttributeValue("value", code);
                //fill <!-- Device Available for Evaluation?, BOX D10  -->
                if (m.CMPL123CME__Date_returned_to_MFG__c.HasValue)
                    element_subjectOf.Element(ns + "deviceObservation").Element(ns + "effectiveTime").SetAttributeValue("value", m.CMPL123CME__Date_returned_to_MFG__c.Value.ToString("yyyyMMdd"));

                //fill <!-- Approximate Age of Device, BOX F9 Code is concept id for approximate_age_of_device -->
                if (m.CMPL123CME__Approximate_age_of_device__c.HasValue)
                {
                    element_subjectOf = element_identifiedDevice.Descendants(ns + "subjectOf").ToList()
                        .Where(p => p.Element(ns + "deviceObservation").Element(ns + "code")
                            .Attribute("codeSystemName").Value == "Approximate_Age_of_Device").FirstOrDefault();
                    element_subjectOf.Element(ns + "deviceObservation").Element(ns + "value").SetAttributeValue("value", m.CMPL123CME__Approximate_age_of_device__c.Value);
                }

                //fill <!-- Event Problem Codes\Device Code, BOX F10 --> TODO

                //fill <!-- Device Evaluated by Manufacturer?, BOX H3 -->
                code = _sfOHelper.GetCodeByValue(sfObjectName.MDR, "CMPL123CME__Device_Eval_By_Mfg__c", m.CMPL123CME__Device_Eval_By_Mfg__c, ref error);
                if (!string.IsNullOrEmpty(error))
                {
                    _errorLog.Add(error);
                    error = "";
                }
                else
                {
                    element_subjectOf = element_identifiedDevice.Descendants(ns + "subjectOf").ToList()
                        .Where(p => p.Element(ns + "deviceObservation").Element(ns + "code")
                            .Attribute("codeSystemName").Value == "Device_Evaluated_By_Manufacturer").FirstOrDefault();
                    element_subjectOf.Element(ns + "deviceObservation").Element(ns + "value").SetAttributeValue("value", code);
                }

                //fill <!-- Device Evaluated by Manufacturer\Returned to Manufacturer, BOX H3 -->
                element_subjectOf = element_identifiedDevice.Descendants(ns + "subjectOf").ToList()
                        .Where(p => p.Element(ns + "deviceObservation").Element(ns + "code")
                            .Attribute("codeSystemName").Value == "Device_Returned_to_Manufacturer_for_Evaluation").FirstOrDefault();
                if (m.CMPL123CME__Device_Eval_By_Mfg__c == "Not Returned to Manufacturer")
                    element_subjectOf.Element(ns + "deviceObservation").Element(ns + "value").SetAttributeValue("value", "false");
                else
                    element_subjectOf.Element(ns + "deviceObservation").Element(ns + "value").SetAttributeValue("value", "true");

                //fill <!-- Device Evaluated by Manufacturer\Evaluation Summary Attached, BOX H3 -->
                element_subjectOf = element_identifiedDevice.Descendants(ns + "subjectOf").ToList()
                        .Where(p => p.Element(ns + "deviceObservation").Element(ns + "code")
                            .Attribute("codeSystemName").Value == "Evaluation_Summary_Status").FirstOrDefault();
                if (m.CMPL123CME__Device_Eval_By_Mfg__c == "Evaluation Summary Attached")
                    element_subjectOf.Element(ns + "deviceObservation").Element(ns + "value").SetAttributeValue("value", "true");
                else
                    element_subjectOf.Element(ns + "deviceObservation").Element(ns + "value").SetAttributeValue("value", "false");

                //fill <!-- Device Evaluated by Manufacturer\Explain why not evaluated or provide code, BOX H3 --> TODO
                /*element_subjectOf = element_identifiedDevice.Descendants(ns + "subjectOf").ToList()
                        .Where(p => p.Element(ns + "deviceObservation").Element(ns + "code")
                            .Attribute("codeSystemName").Value == "Reason_for_Non-Evaluation").FirstOrDefault();*/

                //fill <!-- Labeled for Single Use?, BOX H5 -->
                element_subjectOf = element_identifiedDevice.Descendants(ns + "subjectOf").ToList()
                        .Where(p => p.Element(ns + "deviceObservation").Element(ns + "code")
                            .Attribute("codeSystemName").Value == "Device_Labeled_for_single_use").FirstOrDefault();
                if (m.CMPL123CME__Labeled_for_single_use__c.HasValue && m.CMPL123CME__Labeled_for_single_use__c.Value)
                    element_subjectOf.Element(ns + "deviceObservation").Element(ns + "value").SetAttributeValue("value", "true");
                else
                    element_subjectOf.Element(ns + "deviceObservation").Element(ns + "value").SetAttributeValue("value", "false");

                //fill <!-- If Remedial Action Initiated, Check Type, BOX H7 -->
                 element_subjectOf = element_identifiedDevice.Descendants(ns + "subjectOf").ToList()
                        .Where(p => p.Element(ns + "deviceObservation").Element(ns + "code")
                            .Attribute("codeSystemName").Value == "Type_of_Remedial_Action").FirstOrDefault();
                if (!string.IsNullOrEmpty(m.CMPL123CME__If_Remedical_Action_Init__c))
                {
                    string firstValue="";
                    int deviderIndex = m.CMPL123CME__If_Remedical_Action_Init__c.IndexOf(";");
                    if (deviderIndex == -1)
                        firstValue = m.CMPL123CME__If_Remedical_Action_Init__c;
                    else
                        firstValue = m.CMPL123CME__If_Remedical_Action_Init__c.Substring(0,deviderIndex);

                    code = _sfOHelper.GetCodeByValue(sfObjectName.MDR, "CMPL123CME__If_Remedical_Action_Init__c", firstValue, ref error);
                    if (!string.IsNullOrEmpty(error))
                    {
                        _errorLog.Add(error);
                        error = "";
                    }
                    else
                        element_subjectOf.Element(ns + "deviceObservation").Element(ns + "value").SetAttributeValue("code", code);
                }
                element_subjectOf.Element(ns + "deviceObservation").Element(ns + "value")
                    .Element(ns + "originalText").SetValue(m.CMPL123CME__Other_Remedial_Action__c ?? string.Empty);

                //fill <!-- Usage of Device, BOX H8 -->
                element_subjectOf = element_identifiedDevice.Descendants(ns + "subjectOf").ToList()
                .Where(p => p.Element(ns + "deviceObservation").Element(ns + "code")
                    .Attribute("codeSystemName").Value == "Usage_of_Device").FirstOrDefault();

                code = _sfOHelper.GetCodeByValue(sfObjectName.MDR, "CMPL123CME__Usage_of_Device__c", m.CMPL123CME__Usage_of_Device__c, ref error);
                if (!string.IsNullOrEmpty(error))
                {
                    _errorLog.Add(error);
                    error = "";
                }
                else
                    element_subjectOf.Element(ns + "deviceObservation").Element(ns + "value").SetAttributeValue("code", code);

                //<!-- If action reported under 21 USC 360i(f) list correction/removal reporting number, BOX H9 -->
                element_subjectOf = element_identifiedDevice.Descendants(ns + "subjectOf").ToList()
                    .Where(p => p.Element(ns + "deviceObservation").Element(ns + "code")
                        .Attribute("codeSystemName").Value == "Corrective_Action_Number").FirstOrDefault();
                element_subjectOf.Element(ns + "deviceObservation").Element(ns + "value").SetValue(m.CMPL123CME__Correction_Removal_Report_No__c ?? string.Empty);

                //fill <!-- Operator of Device, BOX D5 Code is Value for Other -->
                code = _sfOHelper.GetCodeByValue(sfObjectName.MDR, "CMPL123CME__Operator_of_Device__c", m.CMPL123CME__Operator_of_Device__c, ref error);
                if (!string.IsNullOrEmpty(error))
                {
                    _errorLog.Add(error);
                    error = "";
                }
                else
                    element_investigationEvent.Element(ns + "pertainsTo").Element(ns + "procedureEvent")
                        .Element(ns + "authorOrPerformer").Element(ns + "assignedEntity").Element(ns + "code")
                        .SetAttributeValue("code", code);

                element_investigationEvent.Element(ns + "pertainsTo").Element(ns + "procedureEvent")
                    .Element(ns + "authorOrPerformer").Element(ns + "assignedEntity").Element(ns + "code")
                    .Element(ns + "originalText").SetValue(m.CMPL123CME__Other_operator_of_Device__c ?? string.Empty);

                string message = doc.Element(ns + "root").Element(ns + "message").ToString().Replace("<message xmlns=\"urn:hl7-org:v3\">","<message>");
                result = result + message;
                messageID++;
            }
            return result;
        }

        /// <summary>
        /// Download report from SalesForece, save to local folder and return as base64 string
        /// </summary>
        /// <param name="mdrID">ID of the MDR</param>
        /// <returns></returns>
        private string getPDFReportAsBase64(string mdrID)
        {
            string filePath = _sConn.getMDRreportAsPDF(mdrID);
            return Convert.ToBase64String(File.ReadAllBytes(filePath));
        }

    }
}
