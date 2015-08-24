using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace NotakeyNETProvider
{
    public class Ms10InteractiveLogon : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct MSV1_0_INTERACTIVE_LOGON
        {
            public Int32 MessageType;    // Should be 2
            public LsaWrapper.LSA_UNICODE_STRING LogonDomainName;
            public LsaWrapper.LSA_UNICODE_STRING UserName; 
            public LsaWrapper.LSA_UNICODE_STRING Password; 
        }

        public Ms10InteractiveLogon(string domain, string user, string password)
        {
            int domainLen = (domain == null) ? 0 : domain.Length;
            int userLen = (user == null) ? 0 : user.Length;
            int passLen = (password == null) ? 0 : password.Length;

            _bufferLength = Marshal.SizeOf(typeof(MSV1_0_INTERACTIVE_LOGON)) + 2 * (domainLen + userLen + passLen);
            _bufferContent = Marshal.AllocHGlobal(_bufferLength);
            if (_bufferContent == IntPtr.Zero)
                throw new OutOfMemoryException("Could not allocate memory for Ms10InteractiveLogon structure");
            try
            {
                int ptrOffset = Marshal.SizeOf(typeof(MSV1_0_INTERACTIVE_LOGON));
                IntPtr curPtr = IntPtr.Add(_bufferContent, ptrOffset);

                MSV1_0_INTERACTIVE_LOGON baseStructure = new MSV1_0_INTERACTIVE_LOGON();
                baseStructure.MessageType = 2;   
                
                baseStructure.LogonDomainName.Length = (UInt16)(2*domainLen);
                baseStructure.LogonDomainName.MaximumLength = (UInt16)(2*domainLen);
                if (domainLen > 0) {
                    baseStructure.LogonDomainName.Buffer = curPtr;
                    Marshal.Copy(domain.ToCharArray(), 0, curPtr, domainLen);
                    curPtr = IntPtr.Add(curPtr, domainLen * 2);
                } else {
                    baseStructure.LogonDomainName.Buffer = IntPtr.Zero;
                }

                baseStructure.UserName.Length = (UInt16)(2*userLen);
                baseStructure.UserName.MaximumLength = (UInt16)(2*userLen);
                if (userLen > 0) {
                    baseStructure.UserName.Buffer = curPtr;
                    Marshal.Copy(user.ToCharArray(), 0, curPtr, userLen);
                    curPtr = IntPtr.Add(curPtr, userLen * 2);
                } else {
                    baseStructure.UserName.Buffer = IntPtr.Zero;
                }

                baseStructure.Password.Length = (UInt16)(2*passLen);
                baseStructure.Password.MaximumLength = (UInt16)(2*passLen);
                if (passLen > 0) {
                    baseStructure.Password.Buffer = curPtr;
                    Marshal.Copy(password.ToCharArray(), 0, curPtr, passLen);
                    curPtr = IntPtr.Add(curPtr, passLen* 2);
                } else {
                    baseStructure.Password.Buffer = IntPtr.Zero;
                }

                Marshal.StructureToPtr(baseStructure, _bufferContent, false);
            }
            catch
            {
                Dispose(true);
                throw;
            }
        }

        private IntPtr _bufferContent;
        private int _bufferLength;

        public IntPtr Ptr
        {
            get { return _bufferContent; }
        }

        public int Length
        {
            get { return _bufferLength; }
        }

        private void Dispose(bool disposing)
        {
            // TODO: is this memory supposed to be freed?
            /*if (_bufferContent != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_bufferContent);
                _bufferContent = IntPtr.Zero;
            }
            if (disposing)
                GC.SuppressFinalize(this);*/
        }

        ~Ms10InteractiveLogon()
        {
            Dispose(false);
        }

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }

}
