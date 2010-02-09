using System;
using System.Collections.Generic;
using System.Text;
using EasyHook;
using System.Windows.Forms;
using System.IO;

namespace HookController
{
    public class CommandLine
    {
        public List<string> responseFiles = new List<string>();
        public List<string> tokens = new List<string>();

        public CommandLine()
        {
        }

        public bool Drop(params string[] s)
        {
            for (int i = 0; i < tokens.Count; ++i)
            {
                int matchCount = 0;
                for (int j = 0; j < s.Length && i + j < tokens.Count; ++j)
                {
                    if (tokens[i + j].Equals(s[j], StringComparison.InvariantCultureIgnoreCase))
                        ++matchCount;
                    else
                        break;
                }

                if (matchCount == s.Length)
                {
                    tokens.RemoveRange(i, s.Length);
                    return true;
                }
            }

            return false;
        }

        public static string[] TokenizeCommandLine(string s)
        {
            bool inQuote = false;
            List<string> result = new List<string>();
            StringBuilder token = new StringBuilder();

            foreach (char ch in s)
            {
                switch (ch)
                {
                    case '"':
                        inQuote = !inQuote;
                        break;
                    case '\n':
                    case '\r':
                    case ' ':
                        if (!inQuote)
                        {
                            if (token.Length > 0)
                            {
                                result.Add(token.ToString());
                                token.Length = 0;
                            }
                        }
                        else
                        {
                            token.Append(' ');
                        }
                        break;
                    default:
                        token.Append(ch);
                        break;
                }
            }

            if (token.Length > 0)
                result.Add(token.ToString());

            return result.ToArray();
        }

        public string GetNewAgeCommandLine()
        {
            StringBuilder result = new StringBuilder();
            foreach (string token in tokens)
            {
                if (result.Length > 0)
                    result.Append(' ');

                if (-1 == token.IndexOf(' '))
                    result.Append(token);
                else
                    result.Append('"').Append(token).Append('"');
            }
            return result.ToString();
        }

        public void AddString(string s)
        {
            List<string> temp = new List<string>();
            temp.AddRange(TokenizeCommandLine(s));
            tokens.AddRange(ExpandResponseFiles(temp));
        }

        private List<string> ExpandResponseFiles(List<string> tokens)
        {
            List<string> result = new List<string>();
            foreach (string s in tokens)
            {
                if (s.StartsWith("@"))
                {
                    string responseFile = s.Substring(1);
                    responseFiles.Add(responseFile);
                    result.AddRange(TokenizeCommandLine(File.ReadAllText(responseFile)));
                }
                else
                    result.Add(s);
            }
            return result;
        }
    }

	public class HookController : MarshalByRefObject
	{
		public void OnInstalled(int pid)
		{
			HookForm.Instance.ExternalOnHookInstalled(pid);
		}

		public void ReportException(Exception ex)
		{
			HookForm.Instance.ExternalLog("Remote exception {0}", ex.Message);
		}

		public void Ping()
		{ }

        public void AdjustCommandLine(ref string lpApplicationName, ref string lpCommandLine)
        {
            HookForm.Instance.ExternalLog("IN: App: {0}, CmdLine: {1}", lpApplicationName, lpCommandLine);

            // Only hook VCBuildHelper calls
            if (!lpApplicationName.EndsWith("\\VcBuildHelper.exe", StringComparison.InvariantCultureIgnoreCase))
                return;

            CommandLine l = new CommandLine();
            l.AddString(lpCommandLine);

            l.Drop("/VERBOSE");
            bool isNewage = l.Drop("/NEWAGE");

            if (isNewage)
            {
                string firstToken = l.tokens[0];
                string newageDir = Environment.GetEnvironmentVariable("NEWAGE");

                if (firstToken.Equals("cl.exe", StringComparison.InvariantCultureIgnoreCase))
                {
                    lpApplicationName = Path.Combine(newageDir, @"ToolChain\RunTime\VCIntegration\NewAgeCl.exe");
                }
                else if (firstToken.Equals("lib.exe", StringComparison.InvariantCultureIgnoreCase))
                {
                    l.tokens.Add("/LIB");
                    lpApplicationName = Path.Combine(newageDir, @"ToolChain\RunTime\VCIntegration\NewAgeLink.exe");
                }
                else if (firstToken.Equals("link.exe", StringComparison.InvariantCultureIgnoreCase))
                {
                    lpApplicationName = Path.Combine(newageDir, @"ToolChain\RunTime\VCIntegration\NewAgeLink.exe");
                }
                else
                {
                    lpApplicationName = "cmd /c echo unsupported";
                }

                lpCommandLine = l.GetNewAgeCommandLine();
            }
            else
            {
                foreach (string responseFile in l.responseFiles)
                {
                    string data = File.ReadAllText(responseFile);
                    data = data.Replace("/VERBOSE", "");
                    File.WriteAllText(responseFile, data);
                }

                lpCommandLine = lpCommandLine.Replace("/VERBOSE", "");
            }

            HookForm.Instance.ExternalLog("OUT: App: {0}, CmdLine: {1}", lpApplicationName, lpCommandLine);
        }
    }
}
