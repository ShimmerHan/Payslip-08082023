using System;
using System.Collections.Generic;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using SL6.ECP;
using System.Configuration;
using System.IO;

namespace EWSCPOCHR
{
    public partial class adssoverify : System.Web.UI.Page
    {
        //wvLog log = new wvLog("adssoverify");
        protected void Page_Load(object sender, EventArgs e)
        {

            string requestToken = Request.QueryString["token"];
            string requestUserID = Request.QueryString["user"];
            //log.WriteLine("Begin AD SSO Verify... Token :" + requestToken + ", UserID : " + requestUserID);

            wsWebPackage result = new wsWebPackage();

            bool bAuth = false;
            try
            {
                //Should have the matching pair of token and userid that added during adlogin initiation.
                string strUserID = Application[requestToken].ToString();
                //log.WriteLine("Checking Token User ID ... App :" + strUserID + ", Verify : " + requestUserID);
                if (requestUserID.ToLower() == strUserID.ToLower())
                {
                    //Remove token from web application after used
                    Application.Remove(requestToken);

                    //Do additional AD authentication with server here if required and set bAuth true/false.
                    bAuth = true;
                }
            }
            catch
            {
            }

            result.Success = bAuth;
            //log.WriteLine("End AD SSO Verify... Success :" + bAuth);
            Response.Clear();
            Response.Write(result.ToXML());
            Response.End();

        }


        //private class wvLog
        //{
        //    string m_pagename = "";
        //    public wvLog(string PageName)
        //    {
        //        m_pagename = PageName;
        //    }
        //    public void WriteLine(string msg)
        //    {
        //        bool bWriteLog = Convert.ToBoolean(ConfigurationManager.AppSettings["writeLog"]); //change this to true/false for write debug log
        //        if (bWriteLog)
        //        {
        //            StreamWriter sw = new StreamWriter(ConfigurationManager.AppSettings["logPath"] + "log_" + m_pagename + ".txt", true);
        //            sw.AutoFlush = true;
        //            sw.WriteLine(DateTime.Now.ToString("yyyyMMdd HH:mm:ss") + ":" + msg);
        //            sw.Close();
        //        }
        //    }
        //}
    }
}