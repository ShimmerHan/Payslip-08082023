using System;
using System.Collections.Generic;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.IO;
using System.ComponentModel;
using SL6.ECP;
using System.Threading;
using System.Configuration;
using Newtonsoft.Json;

namespace EWSCPOCHR
{
    public partial class adssologin : System.Web.UI.Page
    {
        //wvLog log = new wvLog("adssologin");
        protected void Page_Load(object sender, EventArgs e)
        {
            string IsPost = Request.QueryString["IsPost"];
            string errorMsg = "";
            string[] strArray = Request.LogonUserIdentity.Name.Split(new char[] { '\\' });
            string LogonName = strArray[strArray.Length - 1];
            if (IsPost == "true")
            {
                Response.Clear();
                Response.Write(LogonName);
                Response.End();
            }
            else
            {
                bool isAuth = false;
                //log.WriteLine("AD SSO Login Begin... Page Auth : " + Page.Request.IsAuthenticated + ", User Auth : " + Request.LogonUserIdentity.IsAuthenticated);
                if (Page.Request.IsAuthenticated && Request.LogonUserIdentity.IsAuthenticated) //is ad authenticated
                {
                    //Do AD Authentication

                    //log.WriteLine("Logon Name : " + LogonName);
                    try
                    {
                        wsClientInterface cli = ConnectServer();

                        //Init Login Data
                        wsLoginInfo loginData = new wsLoginInfo();
                        loginData.LoginID = strArray[strArray.Length - 1];
                        loginData.Token = Guid.NewGuid().ToString();
                        loginData.LoginMode = wsLoginMode.SSOByWEB;
                        loginData.Category = wsClientCategory.WebView;

                        //log.WriteLine("Token :" + loginData.Token);
                        //Put token and ID pair into webapplication for token verification
                        Application.Add(loginData.Token, loginData.LoginID);
                        try
                        {
                            wsSession session = cli.Login(loginData);

                            //log.WriteLine("Session valid :" + session.IsValid());
                            if (session.IsValid())
                            {
                                //Init WebView webservice helper
                                Session["loginSession"] = session;
                                isAuth = true;
                                //errorMsg = "User do not have permission to access. Please contact with admin on ecms user permission.";
                                //RedirectPage(true, errorMsg);
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ex.Message == "Invalid UserID or Password.")
                            {
                                errorMsg = "AD User [" + LogonName + "] do not have permission to access. Please contact with admin on ecms user permission.";
                            }
                            else
                            {
                                errorMsg = ex.Message;
                            }
                        }
                    }
                    catch (Exception exp)
                    {
                        //EWS failed AD Login, do nothing and direct to normal mode  
                        //log.WriteLine("Error : " + exp.Message);
                        errorMsg = exp.Message;
                    }
                }
                else
                {
                    //Fallback to normal mode if AD authentication failed.  
                    errorMsg = "Login with AD Account [" + LogonName + "] is not authenticated.";
                }
                RedirectPage(isAuth, errorMsg);
            }            
        }

        private class responWeb
        {
            public string bstatus = "1";
            public string msg = "";
        }

        private void RedirectPage(bool bAuthSuccess, string message)
        {
            //log.WriteLine("End AD SSO Login... Success :" + bAuthSuccess);
            responWeb resp = new responWeb();
            if (bAuthSuccess)
            {
                Response.Clear();
                resp.bstatus = "1";
                resp.msg = "main.html";
                Response.Write(JsonConvert.SerializeObject(resp));
                Response.End();
                //Response.Redirect("main.html");
            }
            else
            {
                Response.Clear();
                resp.bstatus = "9";
                resp.msg = message;
                Response.Write(JsonConvert.SerializeObject(resp));
                Response.End();
                //Response.Redirect("login.html");
            }
        }

        private wsClientInterface ConnectServer()
        {
            wsClientInterface interface2;
            SynchronizationContext synchronizationContext = AsyncOperationManager.SynchronizationContext;
            try
            {
                AsyncOperationManager.SynchronizationContext = new SynchronizationContext();
                wsServerAddrInfo serverAddr = new wsServerAddrInfo();
                serverAddr.Addr = ConfigurationManager.AppSettings.Get("address");
                serverAddr.Port = Convert.ToInt32(ConfigurationManager.AppSettings.Get("port"));
                interface2 = wsClientManager.ConnectServer(serverAddr);
            }
            finally
            {
                AsyncOperationManager.SynchronizationContext = synchronizationContext;
            }
            return interface2;
        }
    }

    //public class wvLog
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