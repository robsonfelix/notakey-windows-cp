using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NotakeyNETProvider
{
    static public class LsaWrapper
    {
        public class LsaStringWrapper : IDisposable
        {
            public LSA_STRING _string;

            public LsaStringWrapper(string value)
            {
                _string = new LSA_STRING();
                _string.Length = (ushort)value.Length;
                _string.MaximumLength = (ushort)value.Length;
                _string.Buffer = Marshal.StringToHGlobalAnsi(value);
            }

            ~LsaStringWrapper()
            {
                Dispose(false);
            }

            private void Dispose(bool disposing)
            {
                if (_string.Buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_string.Buffer);
                    _string.Buffer = IntPtr.Zero;
                }
                if (disposing)
                    GC.SuppressFinalize(this);
            }

            #region IDisposable Members

            public void Dispose()
            {
                Dispose(true);
            }

            #endregion
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LSA_STRING
        {
            public UInt16 Length;
            public UInt16 MaximumLength;
            public /*PCHAR*/ IntPtr Buffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct LSA_UNICODE_STRING
        {
            public UInt16 Length;
            public UInt16 MaximumLength;
            public IntPtr Buffer;
        }

        [DllImport("advapi32.dll", CharSet = CharSet.Auto, SetLastError = false)]
        public static extern int LsaNtStatusToWinError(int status);

        [DllImport("secur32.dll", SetLastError = false)]
        public static extern int LsaDeregisterLogonProcess([In] IntPtr LsaHandle);

        [DllImport("secur32.dll", SetLastError = false)]
        public static extern int LsaLookupAuthenticationPackage([In] IntPtr LsaHandle, [In] ref LSA_STRING PackageName, [Out] out UInt32 AuthenticationPackage);

        [DllImport("secur32.dll", SetLastError = false)]
        public static extern int LsaConnectUntrusted([Out] out IntPtr LsaHandle);

        [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern Boolean CredPackAuthenticationBuffer(
          int dwFlags,
          string pszUserName,
          string pszPassword,
          IntPtr pPackedCredentials,
          ref int pcbPackedCredentials);

    }
}
