using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Net;

namespace CredUIInvokerNET
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
        private static extern bool CredUnPackAuthenticationBuffer(int dwFlags,
                                                                   IntPtr pAuthBuffer,
                                                                   uint cbAuthBuffer,
                                                                   StringBuilder pszUserName,
                                                                   ref int pcchMaxUserName,
                                                                   StringBuilder pszDomainName,
                                                                   ref int pcchMaxDomainame,
                                                                   StringBuilder pszPassword,
                                                                   ref int pcchMaxPassword);

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



        
        static void Main(string[] args)
        {
            bool save = false;
             
            uint authPackage = 10;

            var credUiInfo = new CREDUI_INFO();
            credUiInfo.pszCaptionText = "Authentication screen invoker test";
            credUiInfo.pszMessageText = "Please enter authentication information";
            credUiInfo.cbSize = Marshal.SizeOf(credUiInfo);
            credUiInfo.hbmBanner = IntPtr.Zero;
            credUiInfo.hwndParent = IntPtr.Zero;

            IntPtr outCredBuffer = new IntPtr();
            uint outCredSize;

            CredUIPromptForWindowsCredentials(ref credUiInfo, 0, ref authPackage, IntPtr.Zero, 0, out outCredBuffer, out outCredSize, ref save, 0);

            Console.WriteLine("Auth package: {0}", authPackage);
            Console.WriteLine("Out cred size: {0}", outCredSize);

            Console.ReadKey();
        }
    }
}
