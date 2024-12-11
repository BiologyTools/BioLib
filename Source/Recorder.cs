using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace BioLib
{
    public static class Recorder
    {
        public static bool Internal = false;
        static List<string> rec = new List<string>();
        public static bool outputConsole = true;
        public static void Clear()
        {
            rec.Clear();
        }
        public static void Record(MethodInfo inf, bool isInternal, params object[] args)
        {
            if (!isInternal || Internal)
            {
                string name = inf.DeclaringType.FullName.Replace("<", "").Replace("+",".");
                if(name.Contains('<'))
                name = name.Substring(0, name.LastIndexOf('>'));
                string code = name + "." + inf.Name + "(";
                for (int i = 0; i < args.Length; i++)
                {
                    Type t = args[i].GetType();
                    if (args[i] is Array)
                    {
                        Array ar = (Array)args[i];
                        code += "";
                        for (int a = 0; a < ar.Length; a++)
                        {
                            if (t == typeof(string))
                            {
                                if (i == 0)
                                    code += "new " + t.Name + "[]{\"" + ar.GetValue(a).ToString() + "\"";
                                else
                                    code += ",\"" + ar.GetValue(a).ToString() + "\"";
                                if (i == ar.Length - 1)
                                    code += ";";
                            }
                            else
                            if (t == typeof(bool))
                            {
                                if (i == 0)
                                    code += "new " + "bool" + "[]{" + ar.GetValue(a).ToString().ToLower();
                                else
                                    code += ",\"" + ar.GetValue(a).ToString() + "\"";
                                if (i == ar.Length - 1)
                                    code += "}";
                            }
                            else
                            {
                                if (i == 0)
                                    code += "new " + t.Name + "{\"" + ar.GetValue(a).ToString() + "\"";
                                else
                                    code += "," + ar.GetValue(a).ToString();
                                if (i == ar.Length - 1)
                                    code += "}";
                            }
                        }
                    }
                    else
                    if (t == typeof(string))
                    {
                        if (i == 0)
                            code += "\"" + args[i].ToString() + "\"";
                        else
                            code += ",\"" + args[i].ToString() + "\"";
                    }
                    else
                    if (t == typeof(bool))
                    {
                        if (i == 0)
                            code += args[i].ToString().ToLower();
                        else
                            code += "," + args[i].ToString().ToLower();
                    }
                    else
                    if (i == 0)
                        code += args[i];
                    else
                        code += "," + args[i];
                }
                code += ");";
                Console.WriteLine(code);
                rec.Add(code);
            }
        }

        public static void Record(string code)
        {
            rec.Add(code);
        }
        public static string[] Lines
        {
            get
            {
                return rec.ToArray();
            }
        }
        public static void AddLine(string s, bool isInternal)
        {
            if (!isInternal || Internal)
                rec.Add(s);
        }
        public static MethodInfo GetCurrentMethodInfo()
        {
            StackTrace stackTrace = new StackTrace();
            StackFrame stackFrame = stackTrace.GetFrame(1); // 1 to get the current method, 0 would get the GetCurrentMethodInfo itself
            MethodBase methodBase = stackFrame.GetMethod();
            return methodBase as MethodInfo;
        }
    }
}
