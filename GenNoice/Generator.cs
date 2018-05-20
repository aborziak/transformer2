using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Configuration;
using System.Net;
using System.Data;
using MySql.Data.MySqlClient;

namespace GenNoice
{
    class Generator
    {
        //section data 
        class IniSect
        {
            public string name; //title
            public List<string> lst = new List<string>(); //list of values
            public int[] done = { -1, -1, -1 };
        }

        //trasfer data info 
        public class Transf
        {
            public List<int> lstInt = null; //order used in transform 
            public char sp_in = ',';    //input separator
            public char sp_out = ',';   //output separator
            public Regex regexp = null; //input regexp 
            public string regout = "";	//output format
            public int modifonly = 0;	//1-use only modified
            public int all = 0;         //1-join lines
            public MatchEvaluator myEvaluator = null;
            public string file = "", enc = "", dir = "", regrep = "", connection = "";
            public int ng = 50;

            public string MyReplace(Match m)
            {
                if (regrep != "")
                {
                    if (regrep != "+") return regrep;
                    else return "";
                }
                string fmt = regout; 
                for (int i = 0; i < m.Groups.Count; i++)
                {
                    string q = "{" + string.Format("{0}", i) + "}";
                    fmt = fmt.Replace(q, m.Groups[i].Value);
                }
                return fmt;
            }
        }

        //section with templates 
        IniSect templates = new IniSect();
        IniSect scMale = null, scFem = null, scNeutral = null;
        List<IniSect> lstSect = new List<IniSect>();
        //sections with special meaning
        String NAME = "[NAME]", NAME_M = "[NAME_M]", NAME_F = "[NAME_F]";
        Random rnd = new Random(Guid.NewGuid().GetHashCode());
        //DB
        MySqlConnection conn_my = null;
        MySqlCommand cmd_my = null;
        MySqlDataReader rs_my = null;

        //find section
        IniSect FindSect(string param)
        {
            for (int i = 0; i < lstSect.Count; i++)
            {
                if (lstSect[i].name == param)
                    return lstSect[i];
            }
            return null;
        }

        //find value
        string FindValue(IniSect sc, string key)
        {
            int i;
            if (sc == null || sc.lst.Count == 0) return "";
            for (i = 0; i < sc.lst.Count; i++)
            {
                string s = sc.lst[i];
                int ip = s.IndexOf("=");
                if (ip > 0 && s.Substring(0, ip).Trim().ToLower() == key.Trim().ToLower() && ip < s.Length - 1)
                    return s.Substring(ip + 1).Trim();
            }
            return "";
        }

        //get random value from section 
        string ParamValue(IniSect ii, int sex)
        {
            if (ii == null || ii.lst.Count == 0)
                return "";
            //generate unique in array
            int j, i, k = 0;
            bool bGood = false;
            int nchk = Math.Min(ii.done.Length, lstSect.Count);
            do
            {
                j = rnd.Next(ii.lst.Count);
                for (i = 0; i < nchk - 1; i++)
                {
                    if (ii.done[i] == j) break;
                }
                if (i == nchk - 1) bGood = true;
                k++;
                if (k > 10) break;
            } while (!bGood);
            //shift
            for (i = nchk - 1; i > 0; i--)
            {
                ii.done[i] = ii.done[i - 1];
            }
            ii.done[0] = j;

            string s = ii.lst[j];
            return s.Replace("(а)", sex == 1 ? "" : "а");
        }

        //generate one string from random template 
        public string NoiceGen1()
        {
            if (templates.lst.Count == 0) return ""; 
            string s = "?", sReplc;
            int i, j;
            i = rnd.Next(templates.lst.Count);
            s = templates.lst[i];

            int sex = 0; //0-neutral, 1-male, 2-female 
            if (s.IndexOf(NAME) >= 0)
            {
                sex = 1 + rnd.Next(2);
                if (scNeutral != null)
                {
                    sReplc = ParamValue(scNeutral, sex);
                }
                else
                {
                    if (sex == 1)
                        sReplc = ParamValue(scMale, sex);
                    else sReplc = ParamValue(scFem, sex);
                }
                s = s.Replace(NAME, sReplc);
            }
            else if (s.IndexOf(NAME_M) >= 0)
            {
                sex = 1;
                sReplc = ParamValue(scMale, sex);
                s = s.Replace(NAME_M, sReplc);
            }
            else if (s.IndexOf(NAME_F) >= 0)
            {
                sex = 2;
                sReplc = ParamValue(scFem, sex);
                s = s.Replace(NAME_F, sReplc);
            }

            for (j = 0; j < lstSect.Count; j++)
            {
                IniSect ii = lstSect[j];
                if (ii.name == NAME || ii.name == NAME_M || ii.name == NAME_F) continue;
                sReplc = ParamValue(ii, sex);
                s = s.Replace(ii.name, sReplc);
            }
            return s;
        }

        //transform init
        public Transf TransformInit(string sectname)
        {
            int i;
            IniSect scTransf = FindSect(sectname);
            if (scTransf != null && scTransf.lst.Count != 0)
            {
                Transf tf = new Transf();
                tf.lstInt = new List<int>();
                string ord = FindValue(scTransf, "order");
                if (ord != "")
                {
                    string[] arr = ord.Split(',');
                    for (i = 0; i < arr.Length; i++)
                        tf.lstInt.Add(int.Parse(arr[i]));
                }

                ord = FindValue(scTransf, "sp_in");
                if (ord != "") tf.sp_in = ord.ToCharArray()[0];

                ord = FindValue(scTransf, "sp_out");
                if (ord != "") tf.sp_out = ord.ToCharArray()[0];

                ord = FindValue(scTransf, "all");
                if (ord == "1") tf.all = 1;

                ord = FindValue(scTransf, "regexp");
                if (ord != "")
                {
                    tf.regexp = new Regex(ord, RegexOptions.Singleline);
                    tf.myEvaluator = new MatchEvaluator(tf.MyReplace);
                }

                ord = FindValue(scTransf, "regout");
                if (ord != "") tf.regout = ord;

                ord = FindValue(scTransf, "modifonly");
                if (ord != "") int.TryParse(ord, out tf.modifonly);

                ord = FindValue(scTransf, "file");
                if (ord != "") tf.file = ord;

                ord = FindValue(scTransf, "enc");
                if (ord != "") tf.enc = ord;

                ord = FindValue(scTransf, "dir");
                if (ord != "") tf.dir = ord;

                ord = FindValue(scTransf, "regrep");
                if (ord != "") tf.regrep = ord;

                ord = FindValue(scTransf, "ng");
                if (ord != "") int.TryParse(ord, out tf.ng);

                ord = FindValue(scTransf, "connection");
                if (ord != "") tf.connection = ord;
                return tf;
            }
            return null;
        }

        //transform one line
        public string Transform1(Transf tf, string s)
        {
            int i, k;
            if (tf != null && tf.connection != "")
                return DoSql(tf, s);

            if (tf == null || (tf.lstInt.Count == 0 && tf.regexp == null) || s == "")
                return s;

            string q = "";
            if (tf.regexp == null)
            {   //simple change order
                string[] arr = s.Split(tf.sp_in);
                for (i = 0; i < tf.lstInt.Count; i++)
                {
                    if (i > 0)
                        q = q + tf.sp_out;
                    k = tf.lstInt[i];
                    if (k >= arr.Length) continue;
                    q = q + arr[k];
                }
            }
            else
            {   //complex approach with Regex
                q = tf.regexp.Replace(s, tf.myEvaluator);
                if (tf.modifonly == 1 && q == s)
                    q = "";
            }
            return q;
        }

        //load templates and other sections from ini-file 
        public bool LoadNoiceIni(string sFile)
        {
            int i;
            string[] lines = File.ReadAllLines(sFile, Encoding.UTF8);
            bool bParseTempl = false;
            templates.name = "[TEMPLATE]";
            for (i = 0; i < lines.Length; i++)
            {
                string s = lines[i].TrimEnd();
                //Console.WriteLine(s); 
                if (s.Length == 0 || s.Substring(0, 1) == "#") continue;

                if (s == templates.name)
                {
                    bParseTempl = true;
                    continue;
                }
                if (s.Substring(0, 1) == "[")
                {
                    bParseTempl = false;
                    IniSect ii = new IniSect();
                    ii.name = s;
                    lstSect.Add(ii);
                    if (s == NAME) scNeutral = ii;
                    else if (s == NAME_M) scMale = ii;
                    else if (s == NAME_F) scFem = ii;
                    continue;
                }

                if (bParseTempl)
                {
                    templates.lst.Add(s.Trim());
                }
                else if (lstSect.Count > 0)
                {
                    IniSect ii = lstSect[lstSect.Count - 1];
                    ii.lst.Add(s);
                }
            }
            //test ini load
            /*for (j = -1; j < lstSect.Count; j++)
            {
                IniSect ii = j == -1 ? templates : lstSect[j];
                Console.WriteLine(ii.name);
                for (i = 0; i < ii.lst.Count; i++)
                    Console.WriteLine(" " + ii.lst[i]);
            }*/
            return (lstSect.Count != 0);
        }

        public string[] Unique(string[] inp)
        {
            int i, j, iBad = 0;
            for (i = 1; i < inp.Length; i++)
            {
                for (j = 0; j < i; j++)
                {
                    if (inp[j] == null) continue;
                    if (inp[j] == inp[i])
                    {
                        inp[i] = null;
                        iBad++;
                        break;
                    }
                }
            }

            if (iBad == 0) return null;
            string[] outp = new string[inp.Length - iBad];
            j = 0;
            for (i = 0; i < inp.Length; i++)
            {
                if (inp[i] == null) continue;
                outp[j] = inp[i];
                j++;
            }

            return outp;
        }

        public string[] EmptyDel(string[] inp)
        {
            int i, j, iBad = 0;
            for (i = 0; i < inp.Length; i++)
            {
                if (inp[i] == null || inp[i].Trim() == "")
                {
                    inp[i] = null;
                    iBad++;
                }
            }

            if (iBad == 0) return null;
            string[] outp = new string[inp.Length - iBad];
            j = 0;
            for (i = 0; i < inp.Length; i++)
            {
                if (inp[i] == null) continue;
                outp[j] = inp[i];
                j++;
            }

            return outp;
        }

        public string[] Sort(string[] inp, bool bAsc)
        {
            int i;
            List<string> lst = new List<string>();
            for (i = 0; i < inp.Length; i++)
            {
                lst.Add(inp[i]);
            }
            if (bAsc) lst.Sort(CompareAsc);
            else lst.Sort(CompareDesc);

            string[] outp = new string[inp.Length];
            for (i = 0; i < lst.Count; i++)
            {
                outp[i] = lst[i];
            }
            return outp;
        }

        private static int CompareAsc(string x, string y)
        {
            int rc = x.CompareTo(y);
            return rc;
        }
        private static int CompareDesc(string x, string y)
        {
            int rc = x.CompareTo(y);
            return -rc;
        }

        public string[] GetFile(string sFile, Encoding enc)
        {
            if (sFile.IndexOf("http:") == 0 || sFile.IndexOf("http:") == 0)
                return GetUrl(sFile, enc).Split('\n');

            string[] lines = File.ReadAllLines(sFile, enc);
            return lines;
        }

        public void SaveFile(string sFile, string[] lines, Encoding enc)
        {
            string s = sFile;
            if (sFile.IndexOf("http:") == 0 || sFile.IndexOf("http:") == 0)
            {
                s = s.Replace("http:", "").Replace("https:", "").Replace("/", "_");
                s += ".txt";
            }
            File.WriteAllLines(s, lines, enc);
        }

        string GetUrl(string url, Encoding enc)
        {
            string s = "";
            try
            {
                WebRequest request = WebRequest.Create(url);
                request.Proxy.Credentials = CredentialCache.DefaultCredentials;
                WebResponse response = request.GetResponse();
                Stream data = response.GetResponseStream();
                using (StreamReader sr = new StreamReader(data, enc))
                {
                    s = sr.ReadToEnd();
                }
            }
            catch (Exception ex)
            {
                logError(url + ", " + ex.ToString());
                s = "";
            }
            return s;
        }

        public string DoSql(Transf tf, string s)
        {
            OpenConnectionMy(tf.connection, true);
            cmd_my.CommandText = s;// AdjustSql(s);
            cmd_my.ExecuteNonQuery();
            CloseConnectionMy();
            return s;
        }
        //DB support
        DataTable GetDataTableMy(MySqlCommand cmd)
        {
            //SqlDataAdapter da = new SqlDataAdapter(cmd);
            MySqlDataAdapter da = new MySqlDataAdapter(cmd);
            DataTable dt = new DataTable();
            da.Fill(dt);
            return dt;
        }

        IDataParameter CreateParameterMy(string sName, DbType typ, Object val)
        {
            IDataParameter pOperid = cmd_my.CreateParameter();
            //for MSSQL it could be "@" + sName;
            pOperid.ParameterName = "?" + sName;
            pOperid.DbType = typ;
            pOperid.Direction = ParameterDirection.Input;
            pOperid.Value = val;
            return pOperid;
        }

        static string AdjustSql(string sql)
        {
            //for MSSQL it could be return sql.Replace("?", "@");

            //for mySQL
            string s = sql.Replace("GETDATE()", "NOW()");
            return s.Replace("@", "?");
        }

        void OpenConnectionMy(String sName, bool bCo)
        {
            string connStr = (bCo != null ? sName : getConnString(sName != null ? sName : "auction"));
            conn_my = new MySqlConnection(connStr);
            conn_my.Open();

            cmd_my = new MySqlCommand();
            cmd_my.Connection = conn_my;

            //!! charset=utf8; in connection string
            cmd_my.CommandText = "SET NAMES 'utf8';";  //very important first command
            cmd_my.ExecuteNonQuery();
        }

        void CloseConnectionMy()
        {
            if (rs_my != null && !rs_my.IsClosed)
            {
                rs_my.Close();
            }

            if (conn_my != null && conn_my.State == ConnectionState.Open)
            {
                conn_my.Close();
            }
        }

        string getConnString(String sName)
        {
            string connStr = null;
            if (ConfigurationManager.ConnectionStrings.Count == 0 ||
                ConfigurationManager.ConnectionStrings[sName] == null ||
                ConfigurationManager.ConnectionStrings[sName].ConnectionString.Trim() == "")
            {
                logError("Connection string not defined.");
            }
            else
            {
                connStr = ConfigurationManager.ConnectionStrings[sName].ConnectionString;
            }
            return connStr;
        }

        static void logError(String s)
        {
            bool bResponseFailed = false;
            try
            {
                String sFile;
                sFile = "log.txt";
                StreamWriter sw = new StreamWriter(sFile, true, Encoding.UTF8);
                sw.WriteLine(string.Format("{0:yyyy-MM-dd HH:mm:ss} {1}", DateTime.Now, s));
                sw.Close();
            }
            catch (Exception se)
            {
                if (bResponseFailed == false)
                {
                    Console.WriteLine(se.Message);
                }
            }
        }

    }
}
