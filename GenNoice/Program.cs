using System;
using System.Collections.Generic; 
using System.IO; 
using System.Text; 
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Configuration;

//generate random output using ini-file with templates and other data lists
namespace GenNoice
{
    class Program
    {
        static int NG = 50; 
        static string iniFile;  // @"C:\Andrei\sana2\utils\GenNoice\gen1.ini";
        static string inputFile, outputFile, act;

        static void Main(string[] args)
        {
            int i, j ; 
            string s;
            iniFile = ConfigurationManager.AppSettings.Get("ini"); 
            inputFile = ConfigurationManager.AppSettings.Get("input"); 
            outputFile = ConfigurationManager.AppSettings.Get("output"); 
            int.TryParse(ConfigurationManager.AppSettings.Get("NG"), out NG);
            act = ConfigurationManager.AppSettings.Get("act");
            if (args.Length > 0) act = args[0];
            if (args.Length > 1) iniFile = args[1];

            Generator van = new Generator();
            bool bReady = van.LoadNoiceIni(iniFile);
            if (!bReady)
            {
                Console.WriteLine("Bad ini!");
                Console.ReadKey() ; 
                return;
            }

            string [] cmd = act.Split(';');
            string [] lines = null;

            //check input
            List<string> lstFiles = new List<string>();
            if (inputFile != null && inputFile.IndexOf("*") >= 0)
            {
                int ip = inputFile.LastIndexOf("\\");
                string sDir = inputFile.Substring(0, ip);
                string[] Files = Directory.GetFiles(sDir, inputFile.Substring(ip + 1));
                foreach (string file in Files)
                {
                    lstFiles.Add(file);
                }
            }
            else lstFiles.Add(inputFile != null ? inputFile : "fake");

//            bool bNextFile = false;
            int cmdNext = 0;
            for (int kk = 0; kk < lstFiles.Count; kk++)
            {
                string currentFile = lstFiles[kk];
                for (j = cmdNext; j < cmd.Length; j++)
                {
                    /*if (bNextFile)
                    {
                        bNextFile = false;
                        break;
                    }*/
                    if (cmd[j] == "IN")
                    {
                        lines = van.GetFile(inputFile, Encoding.UTF8);
                        continue;
                    }
                    else if (cmd[j] == "END")
                    {
                        if (kk == lstFiles.Count - 1)
                        {   //all files processed, go next command
                            lstFiles.Add(inputFile != null ? inputFile : "fake");
                            cmdNext = j + 1;
                        }
                        break;
                    }
                    else if (cmd[j] == "LIST")
                    {
                        for (int mm = 0; mm < lines.Length; mm++ )
                        {   //add lines to files processed, go next command
                            if (string.IsNullOrEmpty(lines[mm])) continue;
                            lstFiles.Add(lines[mm]);
                        }
                        cmdNext = j + 1;
                        break;
                    }
                    else if (cmd[j] == "GEN" && NG > 0)
                    {
                        lines = new string[NG];
                        for (i = 0; i < NG; i++)
                        {
                            lines[i] = van.NoiceGen1();
                        }
                        continue;
                    }

                    if (lines == null && ((cmd[j] == "OUT") || (cmd[j] == "UNIQUE") || (cmd[j] == "SORT") || (cmd[j] == "SORTDESC") || (cmd[j] == "EMPTY_DEL")))
                    {
                        Console.WriteLine("Bad act!");
                        Console.ReadKey();
                        return;
                    }
                    if (cmd[j] == "OUT")
                    {
                        van.SaveFile(outputFile, lines, Encoding.UTF8);
                        continue;
                    }
                    else if (cmd[j] == "UNIQUE")
                    {
                        string[] lns = van.Unique(lines);
                        if (lns != null)
                            lines = lns;
                        continue;
                    }
                    else if (cmd[j] == "SORT")
                    {
                        string[] lns = van.Sort(lines, true);
                        lines = lns;
                        continue;
                    }
                    else if (cmd[j] == "SORTDESC")
                    {
                        string[] lns = van.Sort(lines, false);
                        lines = lns;
                        continue;
                    }
                    else if (cmd[j] == "EMPTY_DEL")
                    {
                        string[] lns = van.EmptyDel(lines);
                        if (lns != null)
                            lines = lns;
                        continue;
                    }
 
                    //tranform
                    Generator.Transf tf = van.TransformInit(cmd[j]);
                    if (tf == null) continue;

                    if (tf.file != "")
                    {   //data source
                        int enc = 0;
                        int.TryParse(tf.enc, out enc);
                        if (tf.dir.ToLower() == "in")
                        {
                            if (tf.file.Length > 1 && tf.file.IndexOf("*") >= 0)
                            {   //create list of files
                                int ip = tf.file.LastIndexOf("\\");
                                string sDir = tf.file.Substring(0, ip);
                                string[] Files = Directory.GetFiles(sDir, tf.file.Substring(ip + 1));
                                foreach (string file in Files)
                                {
                                    lstFiles.Add(file);
                                }
                                //bNextFile = true;
                                cmdNext = kk + 1;
                                break;
                            }

                            string ff = tf.file == "*" ? currentFile : tf.file;
                            lines = van.GetFile(ff, enc > 0 ? Encoding.GetEncoding(enc) : Encoding.UTF8);
                        }
                        else if (tf.dir.ToLower() == "out")
                        {
                            string ff = tf.file == "*" ? currentFile : tf.file;
                            van.SaveFile(ff, lines, enc > 0 ? Encoding.GetEncoding(enc) : Encoding.UTF8);
                        }
                        else if (tf.dir.ToLower() == "gen")
                        {
                            lines = new string[tf.ng];
                            for (i = 0; i < tf.ng; i++)
                            {
                                lines[i] = van.NoiceGen1();
                            }
                        }
                        continue;
                    }

                    if (lines == null)
                    {
                        Console.WriteLine("Bad act!");
                        Console.ReadKey();
                        return;
                    }
                    if (tf.all == 0)
                    {
                        for (i = 0; i < lines.Length; i++)
                        {
                            s = lines[i].TrimEnd();
                            lines[i] = van.Transform1(tf, s);
                            Console.WriteLine(lines[i]);
                        }
                    }
                    else
                    {   //mrege first
                        s = "";
                        for (i = 0; i < lines.Length; i++)
                            s += (i > 0 ? "\n" : "") + lines[i];
                        //replace
                        s = van.Transform1(tf, s);
                        Console.WriteLine(s);
                        //split back
                        lines = s.Split('\n');
                    }
                }
            }

            Console.WriteLine("Done!") ; 
            Console.ReadKey();
        } 
    }
}