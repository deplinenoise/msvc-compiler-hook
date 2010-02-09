using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Runtime.Remoting;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using EasyHook;

namespace HookController
{
	public partial class HookForm : Form
	{
		private static HookForm s_instance = null;

		public static HookForm Instance
		{ get { return s_instance; } }

		class HookedProcessInfo
		{
			public int pid;
			public string name;
			public bool hooked;
		}

		HookController m_controller = new HookController();

		int[] m_pidList = new int[4096];

		List<HookedProcessInfo> m_processes = new List<HookedProcessInfo>();
		Dictionary<int, HookedProcessInfo> m_pids = new Dictionary<int, HookedProcessInfo>();

        bool first = true;

        protected override void OnShown(EventArgs e)
        {
            if (!first)
                base.OnShown(e);
            else
            {
                first = false;
                Hide();
            }
        }

		public HookForm()
		{
			s_instance = this;

			InitializeComponent();

			this.ProcessList.RetrieveVirtualItem += new RetrieveVirtualItemEventHandler(ProcessList_RetrieveVirtualItem);

			OnLog("ready..");
		}

		void ProcessList_RetrieveVirtualItem(object sender, RetrieveVirtualItemEventArgs e)
		{
			HookedProcessInfo info = m_processes[e.ItemIndex];
			e.Item = new ListViewItem(info.pid.ToString());
			e.Item.SubItems.Add(info.name.ToString());
			e.Item.SubItems.Add(info.hooked.ToString());
		}

		[Flags()]
		public enum ProcessAccess : int
		{
			/// <summary>Specifies all possible access flags for the process object.</summary>
			AllAccess = CreateThread | DuplicateHandle | QueryInformation | SetInformation | Terminate | VMOperation | VMRead | VMWrite | Synchronize,
			/// <summary>Enables usage of the process handle in the CreateRemoteThread function to create a thread in the process.</summary>
			CreateThread = 0x2,
			/// <summary>Enables usage of the process handle as either the source or target process in the DuplicateHandle function to duplicate a handle.</summary>
			DuplicateHandle = 0x40,
			/// <summary>Enables usage of the process handle in the GetExitCodeProcess and GetPriorityClass functions to read information from the process object.</summary>
			QueryInformation = 0x400,
			/// <summary>Enables usage of the process handle in the SetPriorityClass function to set the priority class of the process.</summary>
			SetInformation = 0x200,
			/// <summary>Enables usage of the process handle in the TerminateProcess function to terminate the process.</summary>
			Terminate = 0x1,
			/// <summary>Enables usage of the process handle in the VirtualProtectEx and WriteProcessMemory functions to modify the virtual memory of the process.</summary>
			VMOperation = 0x8,
			/// <summary>Enables usage of the process handle in the ReadProcessMemory function to' read from the virtual memory of the process.</summary>
			VMRead = 0x10,
			/// <summary>Enables usage of the process handle in the WriteProcessMemory function to write to the virtual memory of the process.</summary>
			VMWrite = 0x20,
			/// <summary>Enables usage of the process handle in any of the wait functions to wait for the process to terminate.</summary>
			Synchronize = 0x100000
		}

		[DllImport("psapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
		public static extern bool EnumProcesses(int[] processIds, int size, out int needed);

		[DllImport("kernel32.dll")]
		static extern IntPtr OpenProcess(ProcessAccess dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

		[DllImport("kernel32.dll", SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool CloseHandle(IntPtr hObject);

		[DllImport("psapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
		static extern int GetProcessImageFileName(IntPtr hProcess, StringBuilder lpImageFileName, ref int nSize);

		private void RefreshTimer_Tick(object sender, EventArgs e)
		{
			int needed = 0;
			while (!EnumProcesses(m_pidList, sizeof(int) * m_pidList.Length, out needed))
			{
				m_pidList = new int[needed / sizeof(int) + 64];
			}

			bool changed = false;
			int count = needed / sizeof(int);

			for (int i = 0; i < count; ++i)
			{
				int pid = m_pidList[i];
				if (m_pids.ContainsKey(pid))
					continue;

				m_pids[pid] = null;

				IntPtr handle = IntPtr.Zero;
				try
				{
					handle = OpenProcess(ProcessAccess.QueryInformation, false, pid);
					if (handle != IntPtr.Zero)
					{
						int capacity = 256;
						StringBuilder name = new StringBuilder(capacity);

						if (0 != GetProcessImageFileName(handle, name, ref capacity))
						{
							string nameString = name.ToString();
							if (-1 != nameString.IndexOf("devenv", StringComparison.InvariantCulture))
							{
								HookedProcessInfo info = new HookedProcessInfo();
								info.pid = pid;
								info.name = nameString;
								info.hooked = true;
								m_pids[pid] = info;
                                Hook(pid);
								changed = true;
							}
						}
					}
				}
				catch (Exception)
				{
				}
				finally
				{
					if (handle != IntPtr.Zero)
						CloseHandle(handle);
				}
			}

			foreach (int oldPid in new List<int>(m_pids.Keys))
			{
				bool found = false;
				for (int i = 0; i < count; ++i)
				{
					int pid = m_pidList[i];
					if (oldPid == pid)
					{
						found = true;
						break;
					}
				}

				if (!found)
				{
					m_pids.Remove(oldPid);
					changed = true;
				}
			}

			if (!changed)
				return;

			m_processes.Clear();
			foreach (KeyValuePair<int, HookedProcessInfo> kv in m_pids)
			{
				if (null != kv.Value)
					m_processes.Add(kv.Value);
			}
			ProcessList.VirtualListSize = m_processes.Count;
			ProcessList.Refresh();
		}

        private bool Hook(int pid)
        {
			try
			{
				OnLog(string.Format("hooking pid {0}..", pid));
				Config.Register("CompilerHook", "HookController.exe", "CompilerHookLib.dll");
				string channelName = null;
				RemoteHooking.IpcCreateServer<HookController>(ref channelName, System.Runtime.Remoting.WellKnownObjectMode.SingleCall);
				RemoteHooking.Inject(pid, "CompilerHookLib.dll", "CompilerHookLib.dll", channelName);
				OnLog(string.Format("injecting using channel {0}..", channelName));
                return true;
			}
			catch (Exception ex)
			{
				OnLog(string.Format("error! {0}", ex.Message));
                return false;
			}
        }

		private delegate void DLog(string text);
		private delegate void DHookInstalled(int pid);

		private void OnHookInstalled(int pid)
		{
			m_pids[pid].hooked = true;
			OnLog(string.Format("pingback from hooked pid {0}", pid));
			ProcessList.Refresh();
		}

		private void OnLog(string text)
		{
			if (logBox.Items.Count >= 10)
			{
				logBox.Items.RemoveAt(0);
			}
			logBox.Items.Add(text);
			logBox.Refresh();
		}

		internal void ExternalOnHookInstalled(int pid)
		{
			this.Invoke(new DHookInstalled(OnHookInstalled), new object[] { pid });
		}

		internal void ExternalLog(string format, params object[] args)
		{
			// Invoke(new DLog(OnLog), string.Format(format, args));
		}

		private void HookForm_Resize(object sender, EventArgs e)
		{
			if (FormWindowState.Minimized == this.WindowState)
			{
				Hide();
			}
		}

		private void m_notifyIcon1_DoubleClick(object sender, EventArgs e)
		{
			Show();
			this.WindowState = FormWindowState.Normal;
		}

		private void quitToolStripMenuItem_Click(object sender, EventArgs e)
		{
			Application.Exit();
		}

		private void configureToolStripMenuItem_Click(object sender, EventArgs e)
		{

		}
	}
}
