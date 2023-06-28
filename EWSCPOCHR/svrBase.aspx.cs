using System;
using System.Collections.Generic;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.ComponentModel;
using System.Threading;
using SL6.ECP;
using System.Configuration;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace EWSCPOCHR
{
    public partial class svrBase : BaseService
    {
        protected override void OnPageLoad(BaseService.WebResponse resp, string strFunction)
        {
            if (strFunction == "ChangePassword")
            {
                ChangePassword(resp);
            }
            else if (strFunction == "CheckPasswordExpiring")
            {
                CheckPasswordExpiring(resp);
            }
        }

        private void CheckPasswordExpiring(WebResponse resp)
        {
             wsSession m_Session = GetSession();
             if (m_Session == null)
             {
                 fnCheckSession(resp);
             }
             else
             {
                 try
                 {
                     wsSessionInfo sessionInfo = m_Session.ServiceAdmin.GetSessionList().Find(x => x.SessionID == m_Session.SessionID);

                     wsUserPreference userpef = m_Session.CurrentUser.GetUserPreference();
                     string policyID = userpef.AppData.Get("profile");
                     //string[] userdatastring = m_Session.CurrentUser.GetDataValue("ds_app_d2").Split('|');
                     //string policyID = userdatastring[0].Replace("sp:", "");

                     if (string.IsNullOrEmpty(policyID))
                         policyID = "1";

                     wsFolder fol = m_Session.GetFolder("USERPOLICY", ".");
                     wsQueryObj qobj = new wsQueryObj();
                     qobj.AddQueryItem("ds_docid", wsQueryOperator.EQUAL, policyID);
                     wsQueryResult res = fol.QueryDocuments(qobj, false);

                     if (res.ResultList.Count > 0)
                     {
                         // Passowrd Policy check
                         int PwdExpireDays = 99999;
                         //wsUserPreference userpef = m_Session.CurrentUser.GetUserPreference();
                         string pwdTimeString = userpef.AppData.Get("passwordtime");
                         if (pwdTimeString != null || pwdTimeString != "")
                         {
                             // Check Policy
                             if (sessionInfo.LoginMode == wsLoginMode.ByPassword)
                             {
                                 wsDocument doc = res.ResultList[0] as wsDocument;
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
                                 TimeSpan remainingTime = passwordTime.AddDays(PwdExpireDays) - DateTime.Now;

                                 resp.status = "1";
                                 if (doc.GetDataValue("u_reminder") != null && doc.GetDataValue("u_reminder") != "")
                                 {
                                     if (doc.GetDataValue("u_reminder").Contains("-"))
                                     {
                                         resp.obj = "-1";
                                     }
                                     else
                                     {
                                         int remainderDay = Convert.ToInt32(doc.GetDataValue("u_reminder"));

                                         if (remainingTime.Days <= remainderDay)
                                             resp.obj = remainingTime.Days;
                                         else
                                             resp.obj = "-1";
                                     }
                                 }
                                 else
                                 {
                                     resp.obj = "-1";
                                 }
                             }
                             else
                             {
                                 resp.status = "1";
                                 resp.obj = "-1";
                             }
                         }
                         else
                         {
                             resp.status = "1";
                             resp.obj = "-1";
                         }
                     }
                     else
                     {
                         resp.status = "1";
                         resp.obj = "-1";
                     }
                 }
                 catch (Exception ex)
                 {
                     resp.status = "99";
                     resp.message = ex.Message;
                 }
             }
        }

        // before login
        private void ChangePassword(WebResponse resp)
        {
            wsSession session = null;
            try
            {
                wsServerAddrInfo wsinfo = new wsServerAddrInfo();
                string uid = GetParamOrQueryString("userid");
                string pass = GetParamOrQueryString("pass");
                string newpass = GetParamOrQueryString("newpass");
                string serverAdd = ConfigurationManager.AppSettings["address"];
                string port = ConfigurationManager.AppSettings["port"];

                wsinfo.Mode = wsConnectionMode.TCP;
                wsinfo.Addr = serverAdd;
                wsinfo.Port = int.Parse(port);

                wsClientInterface ws123 = wsClientManager.ConnectServer(wsinfo);

                wsLoginInfo loginData = new wsLoginInfo();
                loginData.Category = wsClientCategory.CustomApplication;
                loginData.LoginID = uid;
                loginData.Password = pass;

                session = ws123.Login(loginData);

                if (session.CurrentUser.VerifyPassword(pass))
                {
                    //string[] userdatastring = session.CurrentUser.GetDataValue("ds_app_d2").Split('|');
                    //string policyID = userdatastring[0].Replace("sp:", "");
                    wsUserPreference userpef = session.CurrentUser.GetUserPreference();
                    string policyID = userpef.AppData.Get("profile");

                    if (string.IsNullOrEmpty(policyID))
                        policyID = "1";

                    wsFolder fol = session.GetFolder("USERPOLICY", ".");
                    wsQueryObj qobj = new wsQueryObj();
                    qobj.AddQueryItem("ds_docid", wsQueryOperator.EQUAL, policyID);
                    wsQueryResult result = fol.QueryDocuments(qobj, false);

                    if (result.ResultList.Count != 0)
                    {
                        PasswordPolicy pwdPolicy = new PasswordPolicy();

                        foreach (wsDocument doc in result.ResultList)
                        {
                            if (doc.GetDataValue("u_p_length") != "" && doc.GetDataValue("u_p_length") != null)
                            {
                                if (doc.GetDataValue("u_p_length").Contains("-"))
                                    pwdPolicy.pwdLength = 0;
                                else
                                {
                                    try { pwdPolicy.pwdLength = Convert.ToInt32(doc.GetDataValue("u_p_length")); }
                                    catch (Exception ex) { pwdPolicy.pwdLength = 0; }
                                }
                            }
                            else
                                pwdPolicy.pwdLength = 0;

                            if (doc.GetDataValue("u_p_numeric") != "" && doc.GetDataValue("u_p_numeric") != null)
                            {
                                try { pwdPolicy.pwdNumeric = Convert.ToBoolean(doc.GetDataValue("u_p_numeric")); }
                                catch (Exception ex) { pwdPolicy.pwdNumeric = false; }
                            }
                            else
                                pwdPolicy.pwdNumeric = false;

                            if (doc.GetDataValue("u_p_alpha") != "" && doc.GetDataValue("u_p_alpha") != null)
                            {
                                try { pwdPolicy.pwdAlpha = Convert.ToBoolean(doc.GetDataValue("u_p_alpha")); }
                                catch (Exception ex) { pwdPolicy.pwdAlpha = false; }
                            }
                            else
                                pwdPolicy.pwdAlpha = false;

                            if (doc.GetDataValue("u_p_case") != "" && doc.GetDataValue("u_p_case") != null)
                            {
                                try { pwdPolicy.pwdCase = Convert.ToBoolean(doc.GetDataValue("u_p_case")); }
                                catch (Exception ex) { pwdPolicy.pwdCase = false; }
                            }
                            else
                                pwdPolicy.pwdCase = false;

                            if (doc.GetDataValue("u_p_nospace") != "" && doc.GetDataValue("u_p_nospace") != null)
                            {
                                try { pwdPolicy.pwdNoSpace = Convert.ToBoolean(doc.GetDataValue("u_p_nospace")); }
                                catch (Exception ex) { pwdPolicy.pwdNoSpace = false; }
                            }
                            else
                                pwdPolicy.pwdCase = false;

                            if (doc.GetDataValue("u_p_nousername") != "" && doc.GetDataValue("u_p_nousername") != null)
                            {
                                try { pwdPolicy.pwdNoUsername = Convert.ToBoolean(doc.GetDataValue("u_p_nousername")); }
                                catch (Exception ex) { pwdPolicy.pwdNoUsername = false; }
                            }
                            else
                                pwdPolicy.pwdCase = false;

                            if (doc.GetDataValue("u_unique") != "" && doc.GetDataValue("u_unique") != null)
                            {
                                if (doc.GetDataValue("u_unique").Contains("-"))
                                    pwdPolicy.pwdUnique = 0;
                                else
                                {
                                    try { pwdPolicy.pwdUnique = Convert.ToInt32(doc.GetDataValue("u_unique")); }
                                    catch (Exception ex) { pwdPolicy.pwdUnique = 0; }
                                }
                            }
                            else
                                pwdPolicy.pwdUnique = 0;

                        }
                        PasswordCheckResult pwdCheckResult = CheckPasswordPolicy(pwdPolicy, session.CurrentUser, newpass);

                        if (!pwdCheckResult.isValid)
                        {
                            resp.status = "2";
                            resp.message = "New Password did not meet the password policy requirement." + pwdCheckResult.rules;
                            //resp.obj = pwdCheckResult.rules;
                        }
                        else
                        {
                            // Change password
                            session.SetPassword(newpass);
                            session.CurrentUser.NeedChangePassword = false;
                            session.CurrentUser.IsReadOnly = false;

                            // Store / update the passwordTime in user preference
                            //wsUserPreference userpef = session.CurrentUser.GetUserPreference();
                            string passwordTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                            userpef.AppData.Set("passwordtime", passwordTime);

                            // Store / update the passwordHistory in user preference
                            if (pwdPolicy.pwdUnique != 0)
                            {
                                string passwordHistoryString = userpef.AppData.Get("passwordHistory");
                                List<string> passwordHistoryList = null;

                                if (passwordHistoryString != null)
                                {
                                    passwordHistoryList = JsonConvert.DeserializeObject<List<string>>(passwordHistoryString);
                                }
                                else
                                {
                                    passwordHistoryList = new List<string>();
                                }
                                passwordHistoryList.Add(wsSecurity.EncryptString(newpass, "SL6.ECP"));

                                while (passwordHistoryList.Count > pwdPolicy.pwdUnique)
                                {
                                    passwordHistoryList.RemoveAt(0);
                                }

                                string passHistoryString = JsonConvert.SerializeObject(passwordHistoryList);
                                userpef.AppData.Set("passwordHistory", passHistoryString);
                            }
                            userpef.SaveToRawData();
                            session.CurrentUser.SaveUserPreference();

                            resp.status = "1";
                            resp.message = "Password has been changed sucessfully!";
                        }
                    }
                    else
                    {
                        // Change password
                        session.SetPassword(newpass);
                        session.CurrentUser.NeedChangePassword = false;
                        session.CurrentUser.IsReadOnly = false;

                        // Store / update the passwordTime in user preference
                        //wsUserPreference userpef = session.CurrentUser.GetUserPreference();
                        string passwordTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        userpef.AppData.Set("passwordtime", passwordTime);
                        userpef.SaveToRawData();
                        session.CurrentUser.SaveUserPreference();

                        resp.status = "1";
                        resp.message = "Password has been changed sucessfully!";
                    }
                }
                else
                {
                    resp.status = "2";
                    resp.message = "Old password is wrong, password did not change";
                }
                session.Release();
            }
            catch (Exception exp)
            {
                resp.status = "99";
                resp.message = exp.Message;

                if (session != null)
                    session.Release();
            }
        }

        // Functions
        private PasswordCheckResult CheckPasswordPolicy(PasswordPolicy pwdPolicy, wsUser user, string newpass)
        {
            PasswordCheckResult passwordResult = new PasswordCheckResult();

            bool meetPasswordLength = false;
            bool meetPassUnique = false;
            bool hasAlphabet = false;
            bool hasUpperCaseLetter = false;
            bool hasDecimalDigit = false;
            bool hasNoUsername = false;
            bool hasNoSpace = false;

            string message = "<br />" + "<br />" + "The password must be: " + "<br />" + " - At least " + pwdPolicy.pwdLength + " characters.";

            // Password Length
            if (newpass.Length >= pwdPolicy.pwdLength)
            {
                meetPasswordLength = true;
            }

            // Password Uniqueness
            if (pwdPolicy.pwdUnique == 0)
            {
                meetPassUnique = true;
            }
            else
            {
                message += "<br />" + " - Unique from the last " + pwdPolicy.pwdUnique + " password.";
                wsUserPreference userpef = user.GetUserPreference();
                string passwordHistoryString = userpef.AppData.Get("passwordHistory");
                List<string> passwordHistoryList = null;

                if (passwordHistoryString != null)
                {
                    passwordHistoryList = JsonConvert.DeserializeObject<List<string>>(passwordHistoryString);

                    bool isUnique = true;

                    foreach (string password in passwordHistoryList)
                    {
                        string test = wsSecurity.DecryptString(password, "SL6.ECP");
                        if (wsSecurity.DecryptString(password, "SL6.ECP") == newpass)
                        {
                            isUnique = false;
                            break;
                        }
                    }

                    if (isUnique)
                    {
                        meetPassUnique = true;
                    }
                    else
                    {
                        meetPassUnique = false;
                    }
                }
                else
                {
                    meetPassUnique = true;
                }
            }

            // Contains Alphabets
            if (pwdPolicy.pwdAlpha)
            {
                message += "<br />" + " - Contains alphabet.";
                foreach (char c in newpass)
                {
                    if (char.IsUpper(c) || char.IsLower(c))
                        hasAlphabet = true;
                }
            }
            else
            {
                hasAlphabet = true;
            }

            // Contains Uppercase and lowercase
            if (pwdPolicy.pwdCase)
            {
                message += "<br />" + " - Contains uppercase and lowercase.";
                foreach (char c in newpass)
                {
                    if (char.IsUpper(c))
                        hasUpperCaseLetter = true;
                }
            }
            else
            {
                hasUpperCaseLetter = true;
            }

            // Contains Numbers
            if (pwdPolicy.pwdNumeric)
            {
                message += "<br />" + " - Contains number.";
                foreach (char c in newpass)
                {
                    if (char.IsDigit(c))
                        hasDecimalDigit = true;
                }
            }
            else
            {
                hasDecimalDigit = true;
            }

            // Contains No Username
            if (pwdPolicy.pwdNoUsername)
            {
                message += "<br />" + " - Contains no login id.";
                if (newpass.ToLower().Contains(user.User_LoginID.ToLower()))
                    hasNoUsername = false;
                else
                    hasNoUsername = true;
            }
            else
            {
                hasNoUsername = true;
            }

            // Contains Space
            if (pwdPolicy.pwdNoSpace)
            {
                message += "<br />" + " - Contains no space.";
                if (newpass.ToLower().Contains(" "))
                    hasNoSpace = false;
                else
                    hasNoSpace = true;
            }
            else
            {
                hasNoSpace = true;
            }

            passwordResult.isValid = meetPasswordLength && hasAlphabet && hasUpperCaseLetter && hasDecimalDigit && hasNoUsername && hasNoSpace && meetPassUnique;
            passwordResult.rules = message;

            return passwordResult;
        }
    }

    public class PasswordPolicy
    {
        public int pwdLength;
        public int pwdUnique;
        public bool pwdNumeric;
        public bool pwdAlpha;
        public bool pwdCase;
        public bool pwdNoUsername;
        public bool pwdNoSpace;
        public string pwdRegex;
    }

    public class PasswordCheckResult
    {
        public bool isValid;
        public string rules;
    }
}