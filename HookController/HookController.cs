using System;
using System.Collections.Generic;
using System.Text;
using EasyHook;
using System.Windows.Forms;

namespace HookController
{
	public class HookController : MarshalByRefObject
	{
		public void OnInstalled(int pid)
		{
			HookForm.Instance.ExternalOnHookInstalled(pid);
		}

		public string TranslateCommandLine(string cmdLine)
		{
			HookForm.Instance.ExternalLog(cmdLine);
			return cmdLine;
		}

		public void ReportException(Exception ex)
		{
			HookForm.Instance.ExternalLog("Remote exception {0}", ex.Message);
		}

		public void Ping()
		{ }
	}
}
