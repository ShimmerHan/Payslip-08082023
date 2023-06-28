using System;
using System.Collections.Generic;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.Configuration;
using SL6.ECP;
using System.IO;
using System.Threading;
using System.ComponentModel;
using System.DirectoryServices;

namespace EWSCPOCHR
{
    public partial class cabinet : System.Web.UI.Page
    {
        protected void Page_Load(object sender, EventArgs e)
        {
            if (Request.UserAgent.IndexOf("Edge") > -1)
            {
                Response.Clear();
                Response.Write("<script language='javascript' type='text/javascript'>alert('This application only support Google Chrome & Microsoft Internet Explorer. Any questions, please contact IT Helpdesk.')</script>");
                Response.End();
            }
            else
            {
                string strFunction = GetParamOrQueryString("q");
                if (strFunction == "fnGetImage")
                {
                    fnGetImage();
                }
            }
        }

        public static void CopyStream(Stream input, Stream output)
        {
            byte[] buffer = new byte[32768];
            int read;
            while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, read);
            }
        }


        public void WriteLog(string msg)
        {
            string path = System.Configuration.ConfigurationManager.AppSettings["LogFile"];
            if (File.Exists(path))
            {
                wsLog m_log = new wsLog(path);
                m_log.AddLog(msg);
            }
        }

        public wsSession GetSession()
        {
            bool validSession = false;
            if (Session["loginSession"] != null) { if (((wsSession)Session["loginSession"]).IsValid()) { validSession = true; } }
            if (validSession)
            {
                return Session["loginSession"] as wsSession;
            }
            else
            {
                return null;
            }
        }

        private wsSession SysLogin()
        {
            SynchronizationContext synchronizationContext = AsyncOperationManager.SynchronizationContext;
            AsyncOperationManager.SynchronizationContext = new SynchronizationContext();
            string sysAdmin = ConfigurationManager.AppSettings["IntraSystemAdmin"];
            string sysAdminPassword = wsSecurity.DecryptString(ConfigurationManager.AppSettings["IntraSystemAdminP"], "SL6.ECP");
            wsServerAddrInfo wsinfo = new wsServerAddrInfo();
            wsinfo.Mode = wsConnectionMode.TCP;
            wsinfo.Port = wsConvert.ToInt32(ConfigurationManager.AppSettings["AppPort"]);
            wsinfo.Addr = ConfigurationManager.AppSettings["AppAddr"];
            wsClientInterface ws123 = wsClientManager.ConnectServer(wsinfo);
            wsLoginInfo loginData = new wsLoginInfo();
            loginData.LoginID = sysAdmin;
            loginData.Password = sysAdminPassword;
            loginData.Category = wsClientCategory.CustomApplication;
            AsyncOperationManager.SynchronizationContext = synchronizationContext;
            return ws123.Login(loginData);
        }

        public class UserInfo
        {
            public string U_ID = "";
            public string U_Name = "";
            public string U_email = "";
            public string U_loginType = "";
            public List<object> TabList = new List<object>();
        }


        private UserInfo GetADUser()
        {
            UserInfo MyUser = new UserInfo();
            if (Page.Request.IsAuthenticated && Request.LogonUserIdentity.IsAuthenticated) //is ad authenticated
            {


                string[] strArray = Request.LogonUserIdentity.Name.Split(new char[] { '\\' });
                string LogonName = strArray[strArray.Length - 1];

                wsServerAddrInfo wsinfo = new wsServerAddrInfo();
                wsinfo.Mode = wsConnectionMode.TCP;
                wsinfo.Port = Convert.ToInt32(ConfigurationManager.AppSettings.Get("port"));
                wsinfo.Addr = ConfigurationManager.AppSettings.Get("address");
                wsClientInterface ws123 = wsClientManager.ConnectServer(wsinfo);

                SL6.ECP.Service.wsServerConfig serverConfig = ws123.GetServerConfigure();
                DirectoryEntry rootDE = new DirectoryEntry();
                rootDE = new DirectoryEntry(ConfigurationManager.AppSettings["LDAPAddress"], serverConfig.LoginConfig.ADSSO_LoginID, wsSecurity.DecryptString(serverConfig.LoginConfig.ADSSO_Password, "SL6.ECP"));
                DirectorySearcher dirSearch = new DirectorySearcher(rootDE);
                dirSearch.Filter = "(&" + "(objectClass=user)" + "(sAMACCOUNTNAME=" + LogonName + ")" + ")";
                dirSearch.PropertiesToLoad.Add("mail");
                dirSearch.PropertiesToLoad.Add("name");
                dirSearch.PropertiesToLoad.Add("sAMACCOUNTNAME");
                dirSearch.PropertiesToLoad.Add("description");
                SearchResult adsr = dirSearch.FindOne();
                //searcher.SearchScope = SearchScope.Subtree;

                if (null == adsr)
                {
                    throw new Exception("AD User Not found. [name: " + LogonName + "]");
                }
                else
                {
                    string strAccName = ""; string strName = ""; string strMail = "";
                    strAccName = ((object)adsr.Properties["sAMACCOUNTNAME"][0]).ToString();
                    strName = ((object)adsr.Properties["name"][0]).ToString();

                    try
                    {
                        strMail = ((object)adsr.Properties["mail"][0]).ToString();
                    }
                    catch (Exception ex)
                    {
                        strMail = "";
                    }
                    MyUser.U_ID = LogonName;
                    MyUser.U_Name = strName;
                    MyUser.U_email = strMail;
                    MyUser.U_loginType = "ADSSO";
                }
            }
            return MyUser;
        }

        private class WebResponse
        {
            //1-success, 9-authentication fail,, 99-general exception
            public string status = "1";
            public string message = "";
            public string filepath = "";
            public string filename = "";
            public string type = "";
            public string exceptiondetail = "";
            public object obj;
        }

        private void fnGetImage()
        {
            WebResponse resp = new WebResponse();
            wsSession m_session = null;
            try
            {
                if (ConfigurationManager.AppSettings["ISAD"] == "1") { m_session = SysLogin(); }
                else
                {
                    m_session = GetSession();
                }
                string CurrentUserID = m_session.CurrentUser.User_LoginID;
                if (ConfigurationManager.AppSettings["ISAD"] == "1") { CurrentUserID = GetADUser().U_ID; }
                string objid = GetParamOrQueryString("objid");
                string type = GetParamOrQueryString("type");

                wsFolder folder = m_session.GetFolder(ConfigurationManager.AppSettings["MainDocDef"], ConfigurationManager.AppSettings["MainFolPathID"]);
                wsQueryObj obj = new wsQueryObj();
                obj.AddQueryItem("ds_objid", wsQueryOperator.EQUAL, objid);
                wsQueryResult result = folder.QueryDocuments(obj, false);
                wsDocument doc = result.ResultList[0] as wsDocument;
                string tempPath = doc.Session.LocalContentService.GetTempPath();

                string id = doc.C3Obj.OriginalContentList[0].FileID;
                wsContentOriginalFile cof = doc.C3Obj.GetOrignalContent(id);
                resp.filename = doc.GetDataValue("ds_ct_name");// cof.OrignalFileName;
                resp.filepath = doc.Session.LocalContentService.UnPackageFile(cof, tempPath);
                Dictionary<string, string> PDfCustomData = new Dictionary<string, string>();
                PDfCustomData.Add("ds_objid", objid);
                PDfCustomData.Add("c_acquire_name", "");
                PDfCustomData.Add("c_acquire_time", "");
                //string resppath = fnDownloadPDF(doc, resp.filepath, "", false, false, "", false, "", PDfCustomData);
                string resppath = doc.Session.LocalContentService.UnPackageFile(cof, tempPath);
                if (resppath != "") { resp.filepath = resppath; }
                doc.AddAudit("Download File", CurrentUserID, doc.ObjID.ToString(), "");
                if (type == "btnView") { resp.type = "inline"; } else if (type == "btnDownload") { resp.type = "attachment"; }
            }
            catch (Exception ex)
            {
                WriteLog(m_session.CurrentUser.User_LoginID + ": " + ex.Message);
                WriteLog(ex.StackTrace);
                WriteLog("");
                resp.status = "99";
                resp.message = ex.ToString();
            }
            if (ConfigurationManager.AppSettings["ISAD"] == "1")
            {
                if (resp.filepath != "")
                {
                    Response.Clear();
                    Response.ClearHeaders();
                    Response.ContentType = "application/octet-stream";//application/pdf
                    if (resp.filename.ToLower().EndsWith(".pdf"))
                    {
                        Response.ContentType = "application/pdf";
                    }
                    //Response.AddHeader("Content-Disposition", "attachment; filename=" + strFilename);
                    Response.AddHeader("Content-Disposition", "" + resp.type + "; filename=" + resp.filename);
                    //Response.AddHeader("Content-Disposition", "inline; filename=" + strFilename);
                    System.IO.FileStream fs = new System.IO.FileStream(resp.filepath, System.IO.FileMode.Open);
                    CopyStream(fs, Response.OutputStream);

                    try { m_session.Release(); }
                    catch (Exception ex) { }

                    m_session = null;
                    fs.Close();
                }
                else
                {
                    Response.Clear();
                    Response.Write("<script language='javascript' type='text/javascript'>alert(" + resp.message + ")</script>");
                    Response.End();
                }
            }
        }

        public string GetParamOrQueryString(string param)
        {
            string retVal = Request.Params[param];
            if (string.IsNullOrEmpty(retVal))
            {
                retVal = Request.QueryString[param];
                if (string.IsNullOrEmpty(retVal))
                {
                    return string.Empty;
                }
            }
            return retVal;
        }
    }
}