using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Configuration;
using Salesforce2FDA;
using Salesforce2FDA.Sforce;

namespace WinForms
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            InitControlalues();
        }

        private void InitControlalues()
        {
            this.txtUserName.Text = ConfigurationManager.AppSettings["username"] == null ? "" : ConfigurationManager.AppSettings["username"].ToString();
            this.txtPassword.Text = ConfigurationManager.AppSettings["password"] ==null ? "" : ConfigurationManager.AppSettings["password"].ToString();
            this.txtFileName.Text = ConfigurationManager.AppSettings["xmlFilePath"] ==null ? "" : ConfigurationManager.AppSettings["xmlFilePath"].ToString();
        }
        private void button1_Click(object sender, EventArgs e)
        {
            this.btnGenerate.Text = "Running ... ";
            this.btnGenerate.Enabled = false;
            this.Cursor = Cursors.WaitCursor;

            try
            {
                sfObjectHelper sfOHelper = new sfObjectHelper();

                saleForceConnector c = new saleForceConnector(txtUserName.Text, txtPassword.Text);

                string csvFields = sfOHelper.GetCSV_MDR_Fields("") + "," + sfOHelper.GetCSV_Complain_Fields("CMPL123CME__Complaint__r.");
                List<CMPL123CME__MDR__c> mdrs = c.getMDRs(csvFields);
                if (mdrs.Count == 0)
                {
                    MessageBox.Show("No MRR to submit.");
                    return;
                }
                fdaConnector fdaC = new fdaConnector(c, mdrs, sfOHelper);

                string result = fdaC.getXML();

                System.IO.File.WriteAllText(this.txtFileName.Text, result);

                c.updateMDRstatus(mdrs);

                MessageBox.Show("XML file saved!");
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Arrow;
                MessageBox.Show("Critical error" + ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.btnGenerate.Text = "Generage";
                this.btnGenerate.Enabled = true;
                this.Cursor = Cursors.Arrow;
            }
        }

        private void btnFileSearch_Click(object sender, EventArgs e)
        {
            // Set filter options and filter index.
            saveFileDialog1.Filter = "XML Files (.xml)|*.xml";
            saveFileDialog1.FilterIndex = 1;
            saveFileDialog1.InitialDirectory= "c:\\";
            

            // Call the ShowDialog method to show the dialog box.
            DialogResult userClickedOK = saveFileDialog1.ShowDialog();

            // Process input if the user clicked OK.
            if (userClickedOK == System.Windows.Forms.DialogResult.OK )
            {
                // Open the selected file to read.
                this.txtFileName.Text = saveFileDialog1.FileName;
            }
        }
        /*
        private void txtPassword_VisibleChanged(object sender, EventArgs e)
        {
            execLikeConsile();
        }

        private void execLikeConsile()
        {
            string logFile ="Application.log";
            System.IO.File.AppendAllText(logFile, "Applciation start" + Environment.NewLine);
            try
            {
                sfObjectHelper sfOHelper = new sfObjectHelper();

                saleForceConnector c = new saleForceConnector(txtUserName.Text, txtPassword.Text);

                string csvFields = sfOHelper.GetCSV_MDR_Fields("") + "," + sfOHelper.GetCSV_Complain_Fields("CMPL123CME__Complaint__r.");
                List<CMPL123CME__MDR__c> mdrs = c.getMDRs(csvFields);
                if (mdrs.Count == 0)
                {
                    System.IO.File.AppendAllText(logFile, "No MRR to submit" + Environment.NewLine);
                    // MessageBox.Show("No MRR to submit.");
                    Application.Exit();
                    return;
                }

                fdaConnector fdaC = new fdaConnector(c, mdrs, sfOHelper);

                string result = fdaC.getXML();

                System.IO.File.WriteAllText(ConfigurationManager.AppSettings["xmlFilePath"].ToString(), result);

                c.updateMDRstatus(mdrs);

                //MessageBox.Show("XML file saved!");
                System.IO.File.AppendAllText(logFile, "XML file saved!" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                this.Cursor = Cursors.Arrow;
                //MessageBox.Show("Critical error" + ex.Message, "ERROR", MessageBoxButtons.OK, MessageBoxIcon.Error);
                System.IO.File.AppendAllText(logFile, "Critical error" + ex.Message + Environment.NewLine);
            }
            finally
            {
                this.btnGenerate.Text = "Generage";
                this.btnGenerate.Enabled = true;
                this.Cursor = Cursors.Arrow;
                Application.Exit();
            }
        }

       */




         


    }
}
