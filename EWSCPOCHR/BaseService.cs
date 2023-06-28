using System;
using System.Collections.Generic;
using System.Web;
using Newtonsoft.Json;
using SL6.ECP;
using System.Data;
using System.IO;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Configuration;
using System.Threading;

namespace EWSCPOCHR
{
    public abstract class BaseService: System.Web.UI.Page
    {

        //protected override void OnPreLoad(EventArgs e)
        //{
        //    WriteLog("OnLoadComplete");
        //    SysLogin(false);
        //    base.OnPreLoad(e);
        //}


        protected abstract void OnPageLoad(WebResponse resp, string strFunction);

        protected override void OnLoad(EventArgs e)
        {
            string strFunction = GetParamOrQueryString("q");
            try
            {
                WebResponse resp = new WebResponse();

                OnPageLoad(resp, strFunction);

                Response.Clear();
                if (resp.filepath != "")
                {
                    if (resp.message != "")
                    {
                        Response.Write("<script language='javascript' type='text/javascript'>alert(" + resp.message + ")</script>");
                    }
                    else
                    {
                        Response.ClearHeaders();
                        if (resp.filepath.ToLower().Contains(".pdf"))
                        {
                            Response.ContentType = "application/pdf";
                        }
                        else
                        {
                            Response.ContentType = "application/octet-stream";
                        }
                        Response.WriteFile(resp.filepath);
                        Response.AddHeader("Content-Disposition", resp.type + "; filename=\"" + resp.filename + "\"");
                    }
                }
                else
                {
                    Response.Write(JsonConvert.SerializeObject(resp));
                }
            }
            catch (Exception exp)
            {
                WriteLog(exp.Message);
                WriteLog(exp.StackTrace);
                WriteLog("");
                WebResponse resp = new WebResponse();
                resp.status = "9";
                resp.message = exp.Message;
                resp.exceptiondetail = exp.StackTrace;
                Response.Clear();
                if (resp.filepath != "") { Response.Write("<script language='javascript' type='text/javascript'>alert(" + resp.message + ")</script>"); }
                else
                {
                    Response.Write(JsonConvert.SerializeObject(resp));
                }
            }
            Response.End();
        }

        public void SysLogin(bool isexpired)
        {
            if(Application["AdminLoginSession"] == null || isexpired == true)
            {
                SynchronizationContext synchronizationContext = AsyncOperationManager.SynchronizationContext;

                try
                {
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
                    loginData.LoginMode = wsLoginMode.ByPassword;
                    loginData.Category = wsClientCategory.WebView;

                    WriteLog("Store login into AdminLoginSession..");
                    Application["AdminLoginSession"] = ws123.Login(loginData);
                    WriteLog("Store login into AdminLoginSession success.." + (Application["AdminLoginSession"] as wsSession).CurrentUser);
                }
                catch (Exception ex)
                {
                    WriteLog(ex.ToString());
                    throw new Exception(ex.ToString());
                }
                AsyncOperationManager.SynchronizationContext = synchronizationContext;
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

        public void WriteLog(string msg)
        {
            string path = System.Configuration.ConfigurationManager.AppSettings["LogFile"];
            if (File.Exists(path))
            {
                wsLog m_log = new wsLog(path);
                m_log.AddLog(msg);
            }
        }

        public void fnCheckSession(WebResponse resp)
        {
            WriteLog("Invalid Session. Please login again.");
            WriteLog("");
            resp.status = "9";
            resp.message = "Invalid Session. Please login again.";
        }

        public wsSession GetSession()
        {
            bool validSession = false;
            if (Session["loginSession"] != null) 
            {
                WriteLog("Session['loginSession'] is not null " + (Session["loginSession"] as wsSession).CurrentUser + ((wsSession)Session["loginSession"]).IsValid());
                if (((wsSession)Session["loginSession"]).IsValid()) { WriteLog("Session valid"); validSession = true; } 
            }
            if (validSession)
            {
                WriteLog("return Session['loginSession']: " + Session["loginSession"]);
                return Session["loginSession"] as wsSession;
            }
            else
            {
                return null;
            }
        }

        //private bool CheckSession()
        //{
        //    bool validSession = false;
        //    if (Session["loginSession"] != null) { if (((wsSession)Session["loginSession"]).IsValid()) { validSession = true; } }
        //    return validSession;
        //}

        public DateTime AddWorkDays(DateTime date, int workingDays)
        {
            int direction = workingDays < 0 ? -1 : 1;
            DateTime newDate = date;
            while (workingDays != 0)
            {
                newDate = newDate.AddDays(direction);
                if (newDate.DayOfWeek != DayOfWeek.Saturday &&
                    newDate.DayOfWeek != DayOfWeek.Sunday &&
                    !IsHoliday(newDate))
                {
                    workingDays -= direction;
                }
            }
            return newDate;
        }

        public bool IsHoliday(DateTime date)
        {
            wsSession m_session = GetSession();
            wsDataSet ds = m_session.GetObj("DATASET", "ds_name", "CPOC_Holidays") as wsDataSet;
            return ds.ValueList.Contains(date.ToString("yyyy-MM-dd"));
        }

        public class WebResponse
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

        public class MYWebForm
        {
            public string F_Name = "";
            public string F_Desc = "";
            public bool has_Upload = false;
            public List<MYWebFormClm> F_Clms = new List<MYWebFormClm>();
        }

        public class MYWebFormClm
        {
            public MYWebFormClm() { }
            public MYWebFormClm(string ColumnName, string Display, string Value, int ReadOnly, string InputType, List<PickupValues> PickupValues, int MaxLength, bool hideclm, bool Required, bool Isnumeric)
            {
                wfclmname = ColumnName;
                wfdisplay = Display;
                wfValuie = Value;
                wfreadonly = ReadOnly;
                wfinputtype = InputType;
                wfPickupValues = PickupValues;
                wfMaxLength = MaxLength;
                wfrequired = Required;
                wfIsNumeric = Isnumeric;
            }
            public string wfclmname = "";
            public string wfdisplay = "";
            public bool wfIsNumeric = false;
            public string wfValuie = "";
            public int wfreadonly = 0;
            public bool wfrequired = false;
            public string wfinputtype = "text";
            public List<PickupValues> wfPickupValues = new List<PickupValues>();
            public int wfMaxLength = 255;
        }

        public string TryConvertDT(string strDatetime, bool Withtime)
        {
            try
            {
                DateTime dtNow;
                if (DateTime.TryParse(strDatetime, out dtNow))
                {
                    strDatetime = dtNow.ToString("dd-MM-yyyy");
                    if (Withtime)
                    {
                        strDatetime = dtNow.ToString("dd-MM-yyyy HH:mm:ss");
                    }
                }
            }
            catch (Exception ex)
            {

            }
            return strDatetime;
        }

        public class PickupValues
        {
            public PickupValues(string p_Name, string p_Translate)
            {
                P_Name = p_Name;
                P_Translate = p_Translate;
            }
            public string P_Name = "";
            public string P_Translate = "";
        }

        #region get file type/file format
        public static string GetFileTypeDescription(string fileNameOrExtension)
        {
            SHFILEINFO shfi;
            if (IntPtr.Zero != SHGetFileInfo(
                                fileNameOrExtension,
                                FILE_ATTRIBUTE_NORMAL,
                                out shfi,
                                (uint)Marshal.SizeOf(typeof(SHFILEINFO)),
                                SHGFI_USEFILEATTRIBUTES | SHGFI_TYPENAME))
            {
                return shfi.szTypeName;
            }
            return null;
        }

        [DllImport("shell32")]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, out SHFILEINFO psfi, uint cbFileInfo, uint flags);

        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        private const uint FILE_ATTRIBUTE_READONLY = 0x00000001;
        private const uint FILE_ATTRIBUTE_HIDDEN = 0x00000002;
        private const uint FILE_ATTRIBUTE_SYSTEM = 0x00000004;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        private const uint FILE_ATTRIBUTE_ARCHIVE = 0x00000020;
        private const uint FILE_ATTRIBUTE_DEVICE = 0x00000040;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        private const uint FILE_ATTRIBUTE_TEMPORARY = 0x00000100;
        private const uint FILE_ATTRIBUTE_SPARSE_FILE = 0x00000200;
        private const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400;
        private const uint FILE_ATTRIBUTE_COMPRESSED = 0x00000800;
        private const uint FILE_ATTRIBUTE_OFFLINE = 0x00001000;
        private const uint FILE_ATTRIBUTE_NOT_CONTENT_INDEXED = 0x00002000;
        private const uint FILE_ATTRIBUTE_ENCRYPTED = 0x00004000;
        private const uint FILE_ATTRIBUTE_VIRTUAL = 0x00010000;

        private const uint SHGFI_ICON = 0x000000100;     // get icon
        private const uint SHGFI_DISPLAYNAME = 0x000000200;     // get display name
        private const uint SHGFI_TYPENAME = 0x000000400;     // get type name
        private const uint SHGFI_ATTRIBUTES = 0x000000800;     // get attributes
        private const uint SHGFI_ICONLOCATION = 0x000001000;     // get icon location
        private const uint SHGFI_EXETYPE = 0x000002000;     // return exe type
        private const uint SHGFI_SYSICONINDEX = 0x000004000;     // get system icon index
        private const uint SHGFI_LINKOVERLAY = 0x000008000;     // put a link overlay on icon
        private const uint SHGFI_SELECTED = 0x000010000;     // show icon in selected state
        private const uint SHGFI_ATTR_SPECIFIED = 0x000020000;     // get only specified attributes
        private const uint SHGFI_LARGEICON = 0x000000000;     // get large icon
        private const uint SHGFI_SMALLICON = 0x000000001;     // get small icon
        private const uint SHGFI_OPENICON = 0x000000002;     // get open icon
        private const uint SHGFI_SHELLICONSIZE = 0x000000004;     // get shell size icon
        private const uint SHGFI_PIDL = 0x000000008;     // pszPath is a pidl
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;     // use passed dwFileAttribute

        #endregion
    }
}