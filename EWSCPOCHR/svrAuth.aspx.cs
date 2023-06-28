using System;
using System.Collections.Generic;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using SL6.ECP;
using System.ComponentModel;
using System.Configuration;
using System.Threading;
using System.Reflection;
using System.DirectoryServices;

namespace EWSCPOCHR
{
    public partial class svrAuth : BaseService
    {
        protected override void OnPageLoad(BaseService.WebResponse resp, string strFunction)
        {
            if (strFunction == "Login")
            {
                Login(resp);
            }
            else if (strFunction == "GetUser")
            {
                GetUser(resp);
            }
            else if (strFunction == "Logout")
            {
                Logout(resp);
            }
        }

        private void Login(WebResponse resp)
        {
            SynchronizationContext synchronizationContext = AsyncOperationManager.SynchronizationContext;
            wsSession m_Session;
            try
            {
                AsyncOperationManager.SynchronizationContext = new SynchronizationContext();
                wsServerAddrInfo wsinfo = new wsServerAddrInfo();

                string uid = GetParamOrQueryString("userid");
                string pass = GetParamOrQueryString("pass");
                bool isRetry = Convert.ToBoolean(GetParamOrQueryString("isRetry"));

                wsinfo.Mode = wsConnectionMode.TCP;
                wsinfo.Port = Convert.ToInt32(ConfigurationManager.AppSettings.Get("port"));
                wsinfo.Addr = ConfigurationManager.AppSettings.Get("address");
                wsClientInterface ws123 = wsClientManager.ConnectServer(wsinfo);

                SL6.ECP.Service.wsServerConfig serverConfig = ws123.GetServerConfigure();


                wsLoginInfo loginData = new wsLoginInfo();
                loginData.LoginID = "SERVICE_ENGINE";
                loginData.LoginMode = wsLoginMode.SSOByServer;
                loginData.Category = wsClientCategory.SystemApplication;
                loginData.Password = wsSecurity.DecryptString(serverConfig.RuntimeConfig.ServicePassword, "SL6.ECP");

                try
                {
                    m_Session = ws123.Login(loginData);
                }
                catch (Exception ex)
                {
                    resp.status = "9";
                    if (ex.Message == "Invalid UserID or Password.")
                        resp.message = "Invalid SERVICE_ENGINE Password. Please check config file.";
                    else
                        resp.message = ex.Message;
                    return;
                }

                // do checking
                try
                {
                    wsUser loginUser = m_Session.GetObj("USER", "ds_loginid", uid) as wsUser;
                    if (loginUser != null)
                    {
                        wsUserPreference userpef = loginUser.GetUserPreference();
                        string policyID = userpef.AppData.Get("profile");

                        if (!string.IsNullOrEmpty(policyID))
                        {
                            wsFolder fol = m_Session.GetFolder("USERPOLICY", ".");
                            wsQueryObj qobj = new wsQueryObj();
                            qobj.AddQueryItem("ds_docid", wsQueryOperator.EQUAL, policyID);
                            wsQueryResult res = fol.QueryDocuments(qobj, false);

                            if (res.ResultList.Count != 0)
                            {
                                wsDocument doc = res.ResultList[0] as wsDocument;
                                string timeout = doc.GetDataValue("u_timeout");
                                string concurrentLimit = doc.GetDataValue("u_concurrent");

                                int concurrentSessionCount = 0;
                                List<wsSessionInfo> sessionList = m_Session.ServiceAdmin.GetSessionList();
                                sessionList.Sort((x, y) => x.ActiveTime.CompareTo(y.ActiveTime));
                                List<wsSessionInfo> currentUserSessionList = new List<wsSessionInfo>();

                                foreach (wsSessionInfo si in sessionList)
                                {
                                    if (si.Category == wsClientCategory.WebView && si.UserID == loginUser.ObjID)
                                    {
                                        concurrentSessionCount++;
                                        currentUserSessionList.Add(si);
                                    }
                                }

                                if (concurrentLimit.Contains("-"))
                                {
                                    concurrentLimit = "99999";
                                }

                                // if insist login, kill previous old session;
                                if (isRetry)
                                {
                                    if (concurrentSessionCount != 0)
                                    {
                                        while (concurrentSessionCount >= Convert.ToInt32(concurrentLimit))
                                        {
                                            m_Session.ServiceAdmin.ReleaseSession(currentUserSessionList[0].SessionID);
                                            currentUserSessionList.RemoveAt(0);
                                            concurrentSessionCount--;
                                        }
                                    }
                                }

                                if (concurrentSessionCount < Convert.ToInt32(concurrentLimit))
                                {
                                    // release service engine login session
                                    m_Session.Release();

                                    wsLoginInfo loginInfo = new wsLoginInfo();
                                    loginInfo.Category = wsClientCategory.WebView;
                                    loginInfo.LoginID = uid;
                                    loginInfo.Password = pass;
                                    //ws123.Timeout = Convert.ToInt32(timeout);
                                    m_Session = ws123.Login(loginInfo);

                                    if (m_Session.CurrentUser.NeedChangePassword)
                                    {
                                        m_Session.Release();
                                        resp.status = "5";
                                        //resp.message = "First-time login. Password change required. <a target='_blank' href='chgpass.html?user=" + loginData.LoginID + "'>Click Here</a>";
                                    }
                                    else
                                    {
                                        //===========================
                                        // Passowrd Policy check
                                        int PwdExpireDays = 99999;
                                        //wsUserPreference userpef = m_Session.CurrentUser.GetUserPreference();
                                        string pwdTimeString = userpef.AppData.Get("passwordtime");
                                        if (pwdTimeString != null || pwdTimeString != "")
                                        {
                                            // Check Policy
                                            if (loginInfo.LoginMode == wsLoginMode.ByPassword)
                                            {
                                                if (doc.GetDataValue("u_duration") != "" && doc.GetDataValue("u_duration") != null)
                                                {
                                                    if (doc.GetDataValue("u_duration").Contains("-"))
                                                        PwdExpireDays = 99999;
                                                    else
                                                        PwdExpireDays = Convert.ToInt32(doc.GetDataValue("u_duration"));
                                                }
                                                else
                                                    PwdExpireDays = 99999;

                                                DateTime passwordTime = Convert.ToDateTime(pwdTimeString);
                                                DateTime currentTime = DateTime.Now;
                                                TimeSpan duration = currentTime - passwordTime;
                                                TimeSpan remainingDays = passwordTime.AddDays(PwdExpireDays) - DateTime.Now;

                                                if (duration.Days >= PwdExpireDays && remainingDays <= new TimeSpan(0, 0, 0, 0, 0))
                                                {
                                                    m_Session.Release();
                                                    resp.status = "7";
                                                    //resp.message = "Password Expired. Password change required. <a target='_blank' href='chgpass.html?user=" + loginData.LoginID + "'>Click Here</a>";
                                                }
                                                else
                                                {
                                                    Session.Timeout = Convert.ToInt32(timeout);
                                                    Session["loginSession"] = m_Session;
                                                    string name = m_Session.CurrentUser.Name;
                                                    resp.status = "1";
                                                    resp.message = "Login Success. Welcome " + name;
                                                }
                                            }
                                            else
                                            {
                                                Session.Timeout = Convert.ToInt32(timeout);
                                                Session["loginSession"] = m_Session;
                                                string name = m_Session.CurrentUser.Name;
                                                resp.status = "1";
                                                resp.message = "Login Success. Welcome " + name;
                                            }
                                        }
                                        else
                                        {
                                            Session.Timeout = Convert.ToInt32(timeout);
                                            Session["loginSession"] = m_Session;
                                            string name = m_Session.CurrentUser.Name;
                                            resp.status = "1";
                                            resp.message = "Login Success. Welcome " + name;
                                        }
                                        //===========================
                                    }

                                }
                                else
                                {
                                    m_Session.Release();
                                    resp.status = "2";
                                    resp.message = "Concurrent login limit has reached.";
                                }
                            }
                            else
                            {
                                // release service engine login session
                                m_Session.Release();

                                wsLoginInfo loginInfo = new wsLoginInfo();
                                loginInfo.Category = wsClientCategory.WebView;
                                loginInfo.LoginID = uid;
                                loginInfo.Password = pass;
                                m_Session = ws123.Login(loginInfo);

                                Session.Timeout = Convert.ToInt32(ConfigurationManager.AppSettings.Get("timeout"));
                                Session["loginSession"] = m_Session;
                                string name = m_Session.CurrentUser.Name;
                                resp.status = "1";
                                resp.message = "Login Success. Welcome " + name;
                            }
                        }
                        else
                        {
                            // release service engine login session
                            m_Session.Release();

                            wsLoginInfo loginInfo = new wsLoginInfo();
                            loginInfo.Category = wsClientCategory.WebView;
                            loginInfo.LoginID = uid;
                            loginInfo.Password = pass;
                            m_Session = ws123.Login(loginInfo);

                            Session.Timeout = Convert.ToInt32(ConfigurationManager.AppSettings.Get("timeout"));
                            Session["loginSession"] = m_Session;
                            string name = m_Session.CurrentUser.Name;
                            resp.status = "1";
                            resp.message = "Login Success. Welcome " + name;
                        }
                    }
                    else
                    {
                        m_Session.Release();
                        resp.status = "9";
                        resp.message = "Invalid UserID or Password.";
                    }
                }
                catch (Exception exp)
                {
                    try { m_Session.Release(); }
                    catch (Exception ex) { }

                    resp.status = "99";
                    resp.message = exp.Message;
                    resp.exceptiondetail = exp.StackTrace;
                }
            }
            catch (Exception exp)
            {
                resp.status = "99";
                resp.message = exp.Message;
                resp.exceptiondetail = exp.StackTrace;
            }
            AsyncOperationManager.SynchronizationContext = synchronizationContext;
        }


        private void GetUser(WebResponse resp)
        {            
                //string result = string.Join("|", list.ToArray());
            if (ConfigurationManager.AppSettings["ISAD"] == "1")
            {
                try
                {
                    UserInfo MyUser = new UserInfo();
                    if (Page.Request.IsAuthenticated && Request.LogonUserIdentity.IsAuthenticated) //is ad authenticated
                    {
                         //SynchronizationContext synchronizationContext = AsyncOperationManager.SynchronizationContext;
                         //AsyncOperationManager.SynchronizationContext = new SynchronizationContext();
           
                        string[] strArray = Request.LogonUserIdentity.Name.Split(new char[] { '\\' });
                        string LogonName = strArray[strArray.Length - 1];

                        //wsServerAddrInfo wsinfo = new wsServerAddrInfo();
                        //wsinfo.Mode = wsConnectionMode.TCP;
                        //wsinfo.Port = Convert.ToInt32(ConfigurationManager.AppSettings.Get("port"));
                        //wsinfo.Addr = ConfigurationManager.AppSettings.Get("address");
                        //wsClientInterface ws123 = wsClientManager.ConnectServer(wsinfo);

                        //SL6.ECP.Service.wsServerConfig serverConfig = ws123.GetServerConfigure();
                        DirectoryEntry rootDE = new DirectoryEntry();
                        string ADID = ConfigurationManager.AppSettings["ADID"];
                        string ADPass = wsSecurity.DecryptString(ConfigurationManager.AppSettings["ADPass"], "SL6.ECP");
                        rootDE = new DirectoryEntry(ConfigurationManager.AppSettings["LDAPAddress"], ADID, ADPass);
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

                            List<object> lstObj = new List<object>();
                            WebTab wt = new WebTab();
                            wt.text = "<br/>e-EA";
                            wt.foldersorting = 1;
                            wt.url = "EA_form.html";
                            wt.imageUrl = "images/folder.png";
                            lstObj.Add(wt);
                            wt = new WebTab();
                            wt.text = "<br/>e-Payslip";
                            wt.foldersorting = 2;
                            wt.url = "payslip.html";
                            wt.imageUrl = "images/document.png";
                            lstObj.Add(wt);
                            // Include letter tab list @timi-2021.04.08
                            wt = new WebTab();
                            wt.text = "<br/>e-Letter";
                            wt.foldersorting = 2;
                            wt.url = "ELetter.html";
                            wt.imageUrl = "images/document.png";
                            lstObj.Add(wt);

                            MyUser.TabList = lstObj;
                        }
                        //AsyncOperationManager.SynchronizationContext = synchronizationContext;
                    }
                    resp.obj = MyUser;
                }
                catch (Exception ex)
                {
                    resp.status = "99";
                    resp.message = ex.Message;
                }
            }
            else
            {

                wsSession m_session = Session["loginSession"] as wsSession;
                if (m_session == null)
                {
                    fnCheckSession(resp);
                }
                else
                {
                    try
                    {
                        m_session = Session["loginSession"] as wsSession;
                        UserInfo MyUser = new UserInfo();
                        MyUser.U_ID = m_session.CurrentUser.User_LoginID;
                        //                    MyUser.U_Name = m_session.CurrentUser.DisplayName + " | Last Login: " + m_session.CurrentUser.SysInfo.LastLoginTime;

                        MyUser.U_Name = m_session.CurrentUser.DisplayName;
                        MyUser.U_email = m_session.CurrentUser.User_Email;

                        wsSessionInfo sessionInfo = m_session.ServiceAdmin.GetSessionList().Find(x => x.SessionID == m_session.SessionID);
                        MyUser.U_loginType = sessionInfo.LoginMode.ToString();
                        List<object> lstObj = new List<object>();
                        List<WebTab> lstWebTab = new List<WebTab>();
                        List<string> lstTab = new List<string>();
                        List<wsFolder> lstTabFol = new List<wsFolder>();
                        foreach (wsRole r in m_session.CurrentUser.GetRoleList())
                        {
                            if (r.Name.StartsWith("WF_"))
                            {
                                foreach (wsFolder fol in r.TargetList)
                                {
                                    bool IsExist = false;
                                    foreach (string tt in lstTab) { if (tt == fol.Name) { IsExist = true; break; } }
                                    if (!IsExist)
                                    {
                                        if (fol.PathID != ".")
                                        {
                                            lstTabFol.Add(fol); lstTab.Add(fol.Name);
                                        }
                                    }
                                }
                            }
                        }

                        lstTabFol.Sort(delegate(wsFolder x, wsFolder y)
                        {
                            return x.SortOrder.CompareTo(y.SortOrder);
                        });

                        foreach (wsFolder fol in lstTabFol)
                        {
                            if (fol.GetChildList().Count > 0)
                            {
                                string strSub = "";
                                WebTabWC wmo = new WebTabWC();
                                wmo.items = new List<SubTab>();
                                wmo.text = "<br/>" + fol.Name;
                                wmo.imageUrl = fol.GetDataValue("ds_app_d3");
                                wmo.foldersorting = fol.SortOrder;
                                lstTab.Add(fol.Name);
                                foreach (wsFolder subfol in fol.GetChildList())
                                {
                                    if (strSub != "") { strSub += ","; }
                                    SubTab st = new SubTab();
                                    lstTab.Add(subfol.Name);
                                    st.text = subfol.Name;
                                    st.url = subfol.GetDataValue("ds_app_d2");
                                    strSub += "{ text: '" + st.text + "', url: '" + st.url + "' }";
                                    wmo.items.Add(st);
                                }
                                lstObj.Add(wmo);
                            }
                            else
                            {
                                lstTab.Add(fol.Name);
                                WebTab wt = new WebTab();
                                wt.text = "<br/>" + fol.Name;
                                wt.foldersorting = fol.SortOrder;
                                wt.url = fol.GetDataValue("ds_app_d2");
                                wt.imageUrl = fol.GetDataValue("ds_app_d3");
                                lstObj.Add(wt);
                            }
                        }

                        // timi-2021.04.08 Only For Development Purpose
                        //==========================================================                        
                        
                        //WebTab wt2 = new WebTab();
                        //wt2.text = "<br/>e-EA";
                        //wt2.foldersorting = 1;
                        //wt2.url = "EA_form.html";
                        //wt2.imageUrl = "images/folder.png";
                        //lstObj.Add(wt2);
                        //wt2 = new WebTab();
                        //wt2.text = "<br/>e-Payslip";
                        //wt2.foldersorting = 2;
                        //wt2.url = "payslip.html";
                        //wt2.imageUrl = "images/document.png";
                        //lstObj.Add(wt2);
                        //// timi-2021.04.08
                        //wt2 = new WebTab();
                        //wt2.text = "<br/>e-Letter";
                        //wt2.foldersorting = 2;
                        //wt2.url = "ELetter.html";
                        //wt2.imageUrl = "images/document.png";
                        //lstObj.Add(wt2);
                        
                        //==========================================================

                        MyUser.TabList = lstObj;
                        //MyUser.RPT_Token = System.Configuration.ConfigurationManager.AppSettings["reportURL"] + "?uid=" + m_session.CurrentUser.User_LoginID + "&token=" + m_session.GetToken();
                        resp.obj = MyUser;
                    }
                    catch (Exception ex)
                    {
                        resp.status = "99";
                        resp.message = ex.Message;
                    }
                }
            }
            //resp.message = bb; // timi-2021.04.08 - dont know why this code is here
        }

        private void Logout(WebResponse resp)
        {

            wsSession sess = base.GetSession();
            if (sess == null)
            {
                resp.status = "9";
                resp.message = "Invalid session!";
            }
            else
            {
                sess.Release();
                Session["loginSession"] = null;
                Session.Clear();
                Session.RemoveAll();
                Session.Abandon();

                resp.message = "Logout success!";
            }
        }

        public class WebTab
        {
            public string text = "";
            public string url = "";
            public string imageUrl = "";
            public int foldersorting = 0;
            public bool encoded = false;
        }
        public class WebTabWC
        {
            public string text = "";
            public string imageUrl = "";
            public bool encoded = false;
            public int foldersorting = 0;
            public List<SubTab> items = new List<SubTab>();
        }

        public class SubTab
        {
            public string text = "";
            public string url = "";
            public string cssClass = "childtab";
        }


        public class UserInfo
        {
            public string U_ID = "";
            public string U_Name = "";
            public string U_email = "";
            public string U_loginType = "";
            public List<object> TabList = new List<object>();
        }
        private bool SyncAD(string loginid, string loginpass)
        {
            bool loginIs = false;
            try
            {
                DirectoryEntry rootDE = new DirectoryEntry();
                try
                {
                    rootDE = new DirectoryEntry("LDAP://bkrm.com.my/DC=bkrm,DC=com,DC=my", loginid, loginpass);
                    loginIs = true;
                    //DC=mnrb,DC=com,mnrb.com [VAD08.mnrb.com]
                    DirectorySearcher dirSearch = new DirectorySearcher(rootDE);
                    dirSearch.Filter = "(&" + "(objectClass=user)" + "(sAMACCOUNTNAME=" + loginid + ")" + ")";
                    dirSearch.PropertiesToLoad.Add("mail"); //azwan@kenanga.com.my
                    dirSearch.PropertiesToLoad.Add("name"); // norazwan bin nordin
                    dirSearch.PropertiesToLoad.Add("sAMACCOUNTNAME"); // azwan
                    dirSearch.PropertiesToLoad.Add("description");
                    dirSearch.PropertiesToLoad.Add("distinguishedName"); //CN=BIBI AFFIDAH ABDUL RAZAK 2013110,OU=Kuching 054,OU=Teller,DC=bkrm,DC=com,DC=my
                    SearchResult resultList = dirSearch.FindOne();
                    if (null == resultList)
                    {
                        throw new Exception("User not found!");
                    }
                }
                catch (Exception ex) { throw new Exception(ex.Message); }
                rootDE.Close();
            }
            catch (Exception exception)
            {
                throw new Exception(exception.Message);
            }
            return loginIs;
        }
    }
}