using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Runtime.InteropServices;
using EasyHook;
using System.Windows.Forms;

namespace CompilerHookLib
{
	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
	internal struct STARTUPINFOA
	{
		public Int32 cb;
		public string lpReserved;
		public string lpDesktop;
		public string lpTitle;
		public Int32 dwX;
		public Int32 dwY;
		public Int32 dwXSize;
		public Int32 dwYSize;
		public Int32 dwXCountChars;
		public Int32 dwYCountChars;
		public Int32 dwFillAttribute;
		public Int32 dwFlags;
		public Int16 wShowWindow;
		public Int16 cbReserved2;
		public IntPtr lpReserved2;
		public IntPtr hStdInput;
		public IntPtr hStdOutput;
		public IntPtr hStdError;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
	internal struct STARTUPINFOW
	{
		public Int32 cb;
		public string lpReserved;
		public string lpDesktop;
		public string lpTitle;
		public Int32 dwX;
		public Int32 dwY;
		public Int32 dwXSize;
		public Int32 dwYSize;
		public Int32 dwXCountChars;
		public Int32 dwYCountChars;
		public Int32 dwFillAttribute;
		public Int32 dwFlags;
		public Int16 wShowWindow;
		public Int16 cbReserved2;
		public IntPtr lpReserved2;
		public IntPtr hStdInput;
		public IntPtr hStdOutput;
		public IntPtr hStdError;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct PROCESS_INFORMATION
	{
		public IntPtr hProcess;
		public IntPtr hThread;
		public int dwProcessId;
		public int dwThreadId;
	}


	public class Hook : EasyHook.IEntryPoint
	{
		HookController.HookController m_controller;
		LocalHook m_createProcessHook;
		LocalHook m_createProcessHookAnsi;

		public Hook(RemoteHooking.IContext context, string channelName)
		{
			m_controller = RemoteHooking.IpcConnectClient<HookController.HookController>(channelName);
		}

		[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		delegate bool DCreateProcessW(string lpApplicationName,
		   string lpCommandLine, IntPtr lpProcessAttributes,
		   IntPtr lpThreadAttributes, bool bInheritHandles,
		   uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory,
		   [In] ref STARTUPINFOW lpStartupInfo,
		   out PROCESS_INFORMATION lpProcessInformation);

		[UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		delegate bool DCreateProcessA(string lpApplicationName,
		   string lpCommandLine, IntPtr lpProcessAttributes,
		   IntPtr lpThreadAttributes, bool bInheritHandles,
		   uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory,
		   [In] ref STARTUPINFOA lpStartupInfo,
		   out PROCESS_INFORMATION lpProcessInformation);

		[DllImport("kernel32.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Ansi, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool CreateProcessA(string lpApplicationName,
		   string lpCommandLine, IntPtr lpProcessAttributes,
		   IntPtr lpThreadAttributes, bool bInheritHandles,
		   uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory,
		   [In] ref STARTUPINFOA lpStartupInfo,
		   out PROCESS_INFORMATION lpProcessInformation);

		[DllImport("kernel32.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
		[return: MarshalAs(UnmanagedType.Bool)]
		static extern bool CreateProcessW(string lpApplicationName,
		   string lpCommandLine, IntPtr lpProcessAttributes,
		   IntPtr lpThreadAttributes, bool bInheritHandles,
		   uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory,
		   [In] ref STARTUPINFOW lpStartupInfo,
		   out PROCESS_INFORMATION lpProcessInformation);

		public void Run(RemoteHooking.IContext context, string channelName)
		{
			try
			{
				m_createProcessHook = LocalHook.Create(
					LocalHook.GetProcAddress("kernel32.dll", "CreateProcessW"),
					new DCreateProcessW(CreateProcess_Hooked_Wide), this);
				m_createProcessHook.ThreadACL.SetExclusiveACL(new int[] { 0 });

				m_createProcessHookAnsi = LocalHook.Create(
					LocalHook.GetProcAddress("kernel32.dll", "CreateProcessA"),
					new DCreateProcessA(CreateProcess_Hooked_Ansi), this);
				m_createProcessHookAnsi.ThreadACL.SetExclusiveACL(new int[] { 0 });
			}
			catch (Exception ex)
			{
				m_controller.ReportException(ex);
			}

			m_controller.OnInstalled(RemoteHooking.GetCurrentProcessId());

			try
			{
				while (true)
				{
					Thread.Sleep(500);
					m_controller.Ping();
				}
			}
			catch (Exception)
			{
			}
		}

		bool CreateProcess_Hooked_Wide(string lpApplicationName,
		   string lpCommandLine, IntPtr lpProcessAttributes,
		   IntPtr lpThreadAttributes, bool bInheritHandles,
		   uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory,
		   [In] ref STARTUPINFOW lpStartupInfo,
		   out PROCESS_INFORMATION lpProcessInformation)
		{
			m_controller.TranslateCommandLine(lpCommandLine);
			return CreateProcessW(lpApplicationName, lpCommandLine, lpProcessAttributes, lpThreadAttributes, bInheritHandles, dwCreationFlags, lpEnvironment, lpCurrentDirectory, ref lpStartupInfo, out lpProcessInformation);
		}

		bool CreateProcess_Hooked_Ansi(string lpApplicationName,
		   string lpCommandLine, IntPtr lpProcessAttributes,
		   IntPtr lpThreadAttributes, bool bInheritHandles,
		   uint dwCreationFlags, IntPtr lpEnvironment, string lpCurrentDirectory,
		   [In] ref STARTUPINFOA lpStartupInfo,
		   out PROCESS_INFORMATION lpProcessInformation)
		{
			m_controller.TranslateCommandLine(lpCommandLine);
			return CreateProcessA(lpApplicationName, lpCommandLine, lpProcessAttributes, lpThreadAttributes, bInheritHandles, dwCreationFlags, lpEnvironment, lpCurrentDirectory, ref lpStartupInfo, out lpProcessInformation);
		}
	}
}
