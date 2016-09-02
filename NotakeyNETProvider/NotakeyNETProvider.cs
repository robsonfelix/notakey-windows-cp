using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CredentialProviders;
using System.Runtime.InteropServices;

namespace NotakeyNETProvider
{
    [Guid("77E5F42E-B280-4219-B130-D48BB3932A04")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    [ProgId("NotakeyNETProvider.NotakeyNETProvider")]
    public class NotakeyNETProvider : ICredentialProvider
    {
        static Guid PHONE_NUMBER_GUID = new Guid("DF06ADD3-83B3-410C-9E03-68C6C92E171D");
        static Guid INSTRUCTION_LABEL_GUID = new Guid("D866B989-0202-46F1-93C0-AE53BCA3F32C");
        static Guid BITMAP_GUID = new Guid("2d837775-f6cd-464e-a745-482fd0b47493");
        static Guid TITLE_GUID = new Guid("7B2A2605-E8D9-4D16-80E6-A0F1FAB5B0A3");
        static Guid SUBMIT_BUTTON_GUID = new Guid("321ED3B2-EDD3-4567-8265-A76173A6ABC0");
        static Guid STATUS_LABEL_GUID = new Guid("12DBC936-9817-4818-9061-1A62C79A28A0");
        static Guid PASS_INPUT_GUID = new Guid("70A6CCF0-D1BE-46DD-83A4-55C8D0566E9D");
        
        public enum FIELDS
        {
            BITMAP,
            TITLE,
            STATUS_LABEL,

            USERNAME_INPUT,
            PASS_INPUT, 
            INSTRUCTION_LABEL,
            
            SUBMIT_BUTTON,
            TOTAL_COUNT
        };

        private ICredentialProviderEvents Events { get; set; }
        
        public void Advise(ICredentialProviderEvents pcpe, uint upAdviseContext)
        {
            Events = pcpe;
        }

        public void UnAdvise()
        {
            Events = null;
        }

        public void GetCredentialAt(uint dwIndex, out ICredentialProviderCredential ppcpc)
        {
            if (dwIndex != 0)
            {
                throw new ArgumentException();
            }
            ppcpc = new NotakeyNETCredential();
        }

        public void GetCredentialCount(out uint pdwCount, out uint pdwDefault, out int pbAutoLogonWithDefault)
        {
            pdwCount = 1;
            pdwDefault = 0;
            pbAutoLogonWithDefault = 0;
        }

        public void GetFieldDescriptorAt(uint dwIndex, IntPtr ppcpfd)
        {
            if (dwIndex < (uint)FIELDS.TOTAL_COUNT && ppcpfd != IntPtr.Zero)
            {
                var result = new _CREDENTIAL_PROVIDER_FIELD_DESCRIPTOR();
                
                if (dwIndex == (uint)FIELDS.BITMAP)
                {
                    result.cpft = _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_TILE_IMAGE;
                    result.dwFieldID = dwIndex;
                    result.pszLabel = "Notakey";
                    result.guidFieldType = BITMAP_GUID;
                }
                else if (dwIndex == (uint)FIELDS.TITLE)
                {
                    result.cpft = _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_LARGE_TEXT;
                    result.dwFieldID = dwIndex;
                    result.pszLabel = "Title";
                    result.guidFieldType = TITLE_GUID;
                }
                else if (dwIndex == (uint)FIELDS.INSTRUCTION_LABEL)
                {
                    result.cpft = _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_SMALL_TEXT;
                    result.dwFieldID = dwIndex;
                    result.pszLabel = "Instructions";
                    result.guidFieldType = INSTRUCTION_LABEL_GUID;
                }
                else if (dwIndex == (uint)FIELDS.STATUS_LABEL ||
                    false//dwIndex == (uint)FIELDS.STATUS_LABEL_DESELECTED
                    )
                {
                    result.cpft = _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_SMALL_TEXT;
                    result.dwFieldID = dwIndex;
                    result.pszLabel = "Server status";
                    result.guidFieldType = STATUS_LABEL_GUID;
                }
                else if (dwIndex == (uint)FIELDS.USERNAME_INPUT)
                {
                    result.cpft = _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_EDIT_TEXT;
                    result.dwFieldID = dwIndex;
                    result.pszLabel = "Username";
                    result.guidFieldType = PHONE_NUMBER_GUID;
                }
                else if (dwIndex == (uint)FIELDS.SUBMIT_BUTTON)
                {
                    result.cpft = _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_SUBMIT_BUTTON;
                    result.dwFieldID = dwIndex;
                    result.pszLabel = "Submit";
                    result.guidFieldType = SUBMIT_BUTTON_GUID;
                }
                else if (dwIndex == (uint)FIELDS.PASS_INPUT)
                {
                    result.cpft = _CREDENTIAL_PROVIDER_FIELD_TYPE.CPFT_PASSWORD_TEXT;
                    result.dwFieldID = dwIndex;
                    result.pszLabel = "Password";
                    result.guidFieldType = PASS_INPUT_GUID;
                }
                
                IntPtr structAddr = Marshal.AllocHGlobal(Marshal.SizeOf(result));
                Marshal.StructureToPtr(result, structAddr, false);
                
                Marshal.WriteIntPtr(ppcpfd, structAddr);
            }
            else
            {
                throw new ArgumentException();
            }
        }

        public void GetFieldDescriptorCount(out uint pdwCount)
        {
            pdwCount = (uint)FIELDS.TOTAL_COUNT;
        }

        public void SetSerialization(ref _CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION pcpcs)
        {
            throw new NotImplementedException();
        }

        public void SetUsageScenario(_CREDENTIAL_PROVIDER_USAGE_SCENARIO cpus, uint dwFlags)
        {
            switch (cpus)
            {
                case _CREDENTIAL_PROVIDER_USAGE_SCENARIO.CPUS_CREDUI:
                case _CREDENTIAL_PROVIDER_USAGE_SCENARIO.CPUS_LOGON:
                case _CREDENTIAL_PROVIDER_USAGE_SCENARIO.CPUS_UNLOCK_WORKSTATION:
                    return;
            }
            throw new NotImplementedException();
        }
    }
}
