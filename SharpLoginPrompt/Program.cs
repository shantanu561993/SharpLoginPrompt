using System;
using System.Net;
using System.DirectoryServices.AccountManagement;
using System.Runtime.InteropServices;
using System.Text;

namespace SharpLoginPrompt
{
    class Program
    {

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
        private static extern int CredUIPromptForWindowsCredentials(ref CREDUI_INFO notUsedHere,
                                                             int authError,
                                                             ref uint authPackage,
                                                             IntPtr InAuthBuffer,
                                                             uint InAuthBufferSize,
                                                             out IntPtr refOutAuthBuffer,
                                                             out uint refOutAuthBufferSize,
                                                             ref bool fSave,
                                                             int flags);
        [DllImport("credui.dll", CharSet = CharSet.Auto)]
        private static extern bool CredUnPackAuthenticationBuffer(int dwFlags,
                                                               IntPtr pAuthBuffer,
                                                               uint cbAuthBuffer,
                                                               StringBuilder pszUserName,
                                                               ref int pcchMaxUserName,
                                                               StringBuilder pszDomainName,
                                                               ref int pcchMaxDomainame,
                                                               StringBuilder pszPassword,
                                                               ref int pcchMaxPassword);



        static void Main(string[] args)
        {
            try {  
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

                int result = CredUIPromptForWindowsCredentials(ref credui,
                                                               0,
                                                               ref authPackage,
                                                               IntPtr.Zero,
                                                               0,
                                                               out outCredBuffer,
                                                               out outCredSize,
                                                               ref save,
                                                               0x1

    /* Generic */);
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
        }
    }
}
