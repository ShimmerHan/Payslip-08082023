using System;
using System.Collections.Generic;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.IO;
using SL6.ECP;
using System.Configuration;
using SL6.ECP.Flow;
using Newtonsoft.Json;
using Ionic.Zip;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.ComponentModel;
using System.DirectoryServices;


namespace EWSCPOCHR
{
    public partial class svrPayroll : BaseService
    {
        protected override void OnPageLoad(BaseService.WebResponse resp, string strFunction)
        {
            if (strFunction == "columnReady")
            {
                columnReady(resp);
            }
            if (strFunction == "getDataSource")
            {
                getDataSource(resp); // not using
            }
            if (strFunction == "fnGetImage")
            {
                fnGetImage(resp);
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
                SynchronizationContext synchronizationContext = AsyncOperationManager.SynchronizationContext;
                AsyncOperationManager.SynchronizationContext = new SynchronizationContext();
           

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
                AsyncOperationManager.SynchronizationContext = synchronizationContext;
            }
            return MyUser;
        }

        private void columnReady(WebResponse resp)
        {
            wsSession m_session = null;
            WriteLog("");
            WriteLog("Start Get Data");
            try
            {
                if (ConfigurationManager.AppSettings["ISAD"] == "1")
                {
                    //SysLogin();
                    if(Application["AdminLoginSession"] == null || ((wsSession)Application["AdminLoginSession"]).IsValid() == false)
                    {
                        WriteLog("AdminLoginSession expired, recall syslogin..");
                        SysLogin(true);
                    }
                    WriteLog("copy application to session.." + (Application["AdminLoginSession"] as wsSession).CurrentUser + ((wsSession)Application["AdminLoginSession"]).IsValid());
                    Session["loginSession"] = Application["AdminLoginSession"];
                    m_session = GetSession();

                }
                else
                {
                    m_session = GetSession();
                }
                string CurrentUserEmail = m_session.CurrentUser.User_Email;
                if (ConfigurationManager.AppSettings["ISAD"] == "1") {

                    //CurrentUserEmail = GetADUser().U_email;
                    string[] strArray = Request.LogonUserIdentity.Name.Split(new char[] { '\\' });
                    string LogonName = strArray[strArray.Length - 1];
                    CurrentUserEmail = LogonName + "@cpoc.com.my";
                    WriteLog(LogonName);
                }
                
                string myform = GetParamOrQueryString("form");
                wsForm formObj = m_session.GetObj("FORM", "ds_name", myform) as wsForm;
                wsFolder fol = m_session.GetFolder(ConfigurationManager.AppSettings["MainDocDef"], ConfigurationManager.AppSettings["MainFolPathID"]);
                List<ColumnType> lstForm = new List<ColumnType>();
                List<ListDoc> listDoc = new List<ListDoc>();
                WriteLog("Getform");
                GridData gData = new GridData();
                DataTable dtble = new DataTable();
                foreach (wsFormData fd in formObj.FormDataList)
                {
                    if (!fd.GetAttributeFlag(wsFormDataAttribute.Hidden))
                    {
                        dtble.Columns.Add(fd.MetaDataName);
                        var width = fd.Parameter.MaxLength;
                        ColumnType clm = new ColumnType();
                        clm.field = fd.MetaDataName;
                        clm.title = fd.DisplayName;
                        clm.width = width + "px";

                        lstForm.Add(clm);
                    }
                }
                gData.Header = lstForm;
                DateTime myDateTime = DateTime.Now;
                string myDateTimeString = myDateTime.ToString("dd-MM-yyyy"); ;
                WriteLog("GetData...");
                if (myform == "EA_form")
                {
                    wsQueryObj qobj = new wsQueryObj();

                    qobj.AddQueryItem("d_employee_email", wsQueryOperator.EQUAL, CurrentUserEmail);
                    qobj.AddQueryItem("doc_type", wsQueryOperator.EQUAL, "3"); //ea form

                    //qobj.OrderBy = "docdate asc";
                    wsQueryResult rslt = fol.QueryDocuments(qobj, false);
                    WriteLog("Got");
                    foreach (wsDocument doc in rslt.ResultList)
                    {

                        ListDoc lstDoc = new ListDoc();
                        lstDoc.objID = doc.ObjID.ToString();
                        listDoc.Add(lstDoc);
                        DataRow dr = dtble.NewRow();
                        foreach (wsFormData dta in formObj.FormDataList)
                        {
                            if (dta.MetaDataName == "ds_ct_name")
                                dr[dta.MetaDataName] = doc.GetDataValue("ds_ct_name");
                            else if (dta.MetaDataName == "d_view" || dta.MetaDataName == "d_download")
                                dr[dta.MetaDataName] = doc.GetDataValue("ds_objid");
                            else if (dta.MetaDataName == "doc_date")
                            {
                                var time = doc.GetDataValue("ds_up_time");
                                dr[dta.MetaDataName] = doc.GetDataValue("ds_up_time");
                                doc.SetDataValue("doc_date", time);
                                doc.Save();
                            }
                            else if (dta.MetaDataName == "d_pay_period")
                            {
                                dr[dta.MetaDataName] = doc.GetDataValue("d_pay_period");
                            }
                            else
                            {
                                dr[dta.MetaDataName] = doc.GetDataValue(dta.MetaDataName);
                            }
                        }
                        dtble.Rows.Add(dr);
                    }
                }
                else if (myform == "ELetter_form")// include letter form @timi-2021.04.08
                {
                    wsQueryObj qobj = new wsQueryObj();

                    qobj.AddQueryItem("d_employee_email", wsQueryOperator.EQUAL, CurrentUserEmail);
                    qobj.AddQueryItem("doc_type", wsQueryOperator.EQUAL, "4"); // ELetter

                    //qobj.OrderBy = "docdate asc";
                    wsQueryResult rslt = fol.QueryDocuments(qobj, false);
                    WriteLog("Got");
                    foreach (wsDocument doc in rslt.ResultList)
                    {

                        ListDoc lstDoc = new ListDoc();
                        lstDoc.objID = doc.ObjID.ToString();
                        listDoc.Add(lstDoc);
                        DataRow dr = dtble.NewRow();
                        foreach (wsFormData dta in formObj.FormDataList)
                        {
                            if (dta.MetaDataName == "ds_ct_name")
                            {
                                dr[dta.MetaDataName] = doc.GetDataValue("ds_ct_name");
                            }
                            else if (dta.MetaDataName == "d_view" || dta.MetaDataName == "d_download")
                            {
                                dr[dta.MetaDataName] = doc.GetDataValue("ds_objid");
                            }
                            else if (dta.MetaDataName == "doc_date")
                            {
                                var time = doc.GetDataValue("ds_up_time");
                                dr[dta.MetaDataName] = doc.GetDataValue("ds_up_time");
                                doc.SetDataValue("doc_date", time);
                                doc.Save();
                            }
                            else if (dta.MetaDataName == "d_pay_period")
                            {
                                dr[dta.MetaDataName] = doc.GetDataValue("d_pay_period");
                            }
                            else
                            {
                                dr[dta.MetaDataName] = doc.GetDataValue(dta.MetaDataName);
                            }
                        }
                        dtble.Rows.Add(dr);
                    }
                } 
                else
                {
                    wsQueryObj qobj = new wsQueryObj();
                    qobj.AddQueryItem("d_employee_email", wsQueryOperator.EQUAL, CurrentUserEmail);
                    qobj.AddQueryItem("doc_type", wsQueryOperator.NOT_EQUAL, "3"); //payslip
                    qobj.AddQueryItem("doc_type", wsQueryOperator.NOT_EQUAL, "4"); //letter @timi-2021.04.08
                    //qobj.OrderBy = "docdate asc";
                    wsQueryResult rslt = fol.QueryDocuments(qobj, false);
                    WriteLog("Got");
                    foreach (wsDocument doc in rslt.ResultList)
                    {
                        ListDoc lstDoc = new ListDoc();
                        lstDoc.objID = doc.ObjID.ToString();
                        listDoc.Add(lstDoc);
                        DataRow dr = dtble.NewRow();
                        foreach (wsFormData dta in formObj.FormDataList)
                        {
                            if (dta.MetaDataName == "ds_ct_name")
                            {
                                dr[dta.MetaDataName] = doc.GetDataValue("ds_ct_name");
                            }
                            else if (dta.MetaDataName == "d_view" || dta.MetaDataName == "d_download")
                            {
                                dr[dta.MetaDataName] = doc.GetDataValue("ds_objid");
                            }
                            else if (dta.MetaDataName == "doc_date")
                            {
                                dr[dta.MetaDataName] = myDateTimeString;
                                doc.SetDataValue("doc_date", myDateTimeString);
                                doc.Save();
                            }
                            else if (dta.MetaDataName == "d_pay_period")
                            {
                                var temp = doc.GetDataValue("d_pay_period");
                                var year = temp.Substring(0, 4);
                                var month = temp.Substring(4, 2);
                                var name = year + "/" + month;

                                dr[dta.MetaDataName] = name;
                            }
                            else if (dta.MetaDataName == "doc_type")
                            {
                                var type = doc.GetDataValue("doc_type");
                                if (type == "1")
                                {
                                    dr[dta.MetaDataName] = "Normal Cycle";
                                }
                                else
                                {
                                    dr[dta.MetaDataName] = "Off Cycle";
                                }

                            }
                            else
                            {
                                dr[dta.MetaDataName] = doc.GetDataValue(dta.MetaDataName);
                            }
                            
                        }
                        dtble.Rows.Add(dr);
                    }

                }
                gData.ListDoc = listDoc;
                gData.Rows = dtble;
                resp.obj = gData;
                resp.status = "1";
            }
            catch (Exception ex)
            {
                resp.status = "99";
                resp.message = ex.Message;
                WriteLog("Error: " + ex.Message);
                WriteLog(ex.StackTrace);
                WriteLog("");
            }
            //if (ConfigurationManager.AppSettings["ISAD"] == "1") { try { m_session.Release(); Session["loginSession"] = null; } catch (Exception ex) { } }
      
        }


        private void fnGetImage(WebResponse resp)
        {
            wsSession m_session = null;
            try
            {
                WriteLog("syslogin");
                if (ConfigurationManager.AppSettings["ISAD"] == "1")
                {
                    //SysLogin();
                    if (Application["AdminLoginSession"] == null || ((wsSession)Application["AdminLoginSession"]).IsValid() == false)
                    {
                        WriteLog("AdminLoginSession expired, recall syslogin..");
                        SysLogin(true);
                    }
                    Session["loginSession"] = Application["AdminLoginSession"];
                    m_session = GetSession();
                }
                else
                {
                    m_session = GetSession();
                }
                string CurrentUserID = m_session.CurrentUser.User_LoginID;
                WriteLog("GetADUSER");
                if (ConfigurationManager.AppSettings["ISAD"] == "1") { CurrentUserID = GetADUser().U_ID; }
                string objid = GetParamOrQueryString("objid");
                string type = GetParamOrQueryString("type");
                WriteLog("query file");
                wsFolder folder = m_session.GetFolder(ConfigurationManager.AppSettings["MainDocDef"], ConfigurationManager.AppSettings["MainFolPathID"]);
                wsQueryObj obj = new wsQueryObj();
                obj.AddQueryItem("ds_objid", wsQueryOperator.EQUAL, objid);
                wsQueryResult result = folder.QueryDocuments(obj, false);
                wsDocument doc = result.ResultList[0] as wsDocument;
                string tempPath = doc.Session.LocalContentService.GetTempPath();
                WriteLog("after get temp path");
                string id = doc.C3Obj.OriginalContentList[0].FileID;
                WriteLog("get original file");
                wsContentOriginalFile cof = doc.C3Obj.GetOrignalContent(id);
                resp.filename = doc.GetDataValue("ds_ct_name");// cof.OrignalFileName;
                resp.filepath = doc.Session.LocalContentService.UnPackageFile(cof, tempPath);
                Dictionary<string, string> PDfCustomData = new Dictionary<string, string>();
                PDfCustomData.Add("ds_objid", objid);
                PDfCustomData.Add("c_acquire_name", "");
                PDfCustomData.Add("c_acquire_time", "");
                //string resppath = fnDownloadPDF(doc, resp.filepath, "", false, false, "", false, "", PDfCustomData);
                WriteLog("unpackage");
                string resppath = doc.Session.LocalContentService.UnPackageFile(cof, tempPath);
                WriteLog("after");
                WriteLog("resppath" + resppath);
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
            if (ConfigurationManager.AppSettings["ISAD"] == "1") {
                if (resp.filepath != "")
                {
                    Session["loginSession"] = null;
                    WriteLog("response clear");
                    Response.Clear();
                    Response.ClearHeaders();
                    Response.ContentType = "application/octet-stream";
                    //Response.AddHeader("Content-Disposition", "attachment; filename=" + strFilename);
                    Response.AddHeader("Content-Disposition", "" + resp.type + "\"; filename=" + resp.filename + "\"");
                    System.IO.FileStream fs = new System.IO.FileStream(resp.filepath, System.IO.FileMode.Open);
                    WriteLog("copy stream");
                    CopyStream(fs, Response.OutputStream);

                    try { m_session.Release(); }
                    catch (Exception ex) { WriteLog("session relear ERROR"); }

                    m_session = null;
                    WriteLog("fs close");
                    fs.Close();
                    WriteLog("responce end");
                    Response.End();
                }
                else
                {
                    Response.Clear();
                    Response.Write("<script language='javascript' type='text/javascript'>alert(" + resp.message + ")</script>");
                    Response.End();
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

        private void getDataSource(WebResponse resp)
        {
            string cur_usrlogin = "";
            wsSession m_session = null;
            try
            {
                if (ConfigurationManager.AppSettings["ISAD"] == "1") 
                {
                    //SysLogin(); 
                    Session["loginSession"] = Application["AdminLoginSession"];
                    m_session = GetSession(); 
                }
                else
                {
                    m_session = GetSession();
                }
                cur_usrlogin = m_session.CurrentUser.User_LoginID;
                if (ConfigurationManager.AppSettings["ISAD"] == "1") { cur_usrlogin = GetADUser().U_ID; }
                string myform = GetParamOrQueryString("form");
                wsFolder fol = m_session.GetFolder(ConfigurationManager.AppSettings["MainDocDef"], ".");
                List<ColumnType> lstForm = new List<ColumnType>();
                DataTable dtble = new DataTable();
                GridData gData = new GridData();
                wsForm formObj = m_session.GetObj("FORM", "ds_name", myform) as wsForm;

                wsQueryObj qobj = new wsQueryObj(); 
                qobj.AddQueryItem("c_employee_id", wsQueryOperator.EQUAL, "101");
                qobj.AddQueryItem("c_doctype", wsQueryOperator.EQUAL, "EA");
                //qobj.OrderBy = "docdate asc";
                wsQueryResult rslt = fol.QueryDocuments(qobj, false);

                foreach (wsDocument doc in rslt.ResultList)
                {
                    DataRow dr = dtble.NewRow();
                    foreach (wsFormData dta in formObj.FormDataList)
                    {

                        dr[dta.MetaDataName] = doc.GetDataValue(dta.MetaDataName);
                    }
                    dtble.Rows.Add(dr);
                }

                gData.Rows = dtble;
                gData.Total = rslt.TotalCount;
                gData.fetchTotal = qobj.FetchResultCount;

                resp.obj = gData;
                resp.status = "1";

            }
            catch (Exception ex)
            {
                resp.status = "99";
                resp.message = ex.Message;
                WriteLog("Error: " + ex.Message);
                WriteLog(ex.StackTrace);
                WriteLog("");
            }

        }


        public class ColumnType
        {
            public string field = "";
            public string title = "";
            public string width = "";
        }

        public class ListDoc
        {
            public string objID = "";
        }

        public class GridData
        {
            public List<ColumnType> Header;
            public DataTable Rows;
            public List<ListDoc> ListDoc;
            public long fetchTotal;
            public long Total;
        }



    }
}