using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CredentialProviders;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Drawing;
using NotakeyIPCLibrary;
using System.Threading;
using System.Diagnostics;
using System.ComponentModel;
using System.IO;

namespace NotakeyNETProvider
{
    [ComConversionLoss]
    [Guid("fd672c54-40ea-4d6e-9b49-cfb1a7507bd7")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICredentialProviderCredential2 : ICredentialProviderCredential
    {
        void GetUserSid(out string ppsz);
    }

    [Guid("25D1B19F-C987-4F5D-8309-B319AE956F51")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    [ProgId("NotakeyNETProvider.NotakeyNETCredential")]
    public class NotakeyNETCredential: ICredentialProviderCredential2
    {
        private ICredentialProviderCredentialEvents Events = null;
        static string InstructionsText = "You will have to authorize the request using notaKey on your smartphone.";

        /// <summary>
        /// Placeholder for lazy-loaded service status text
        /// </summary>
        static string statusLabel = "Determining Status…";
        private CancellationTokenSource statusCheckTokenSource = null;

        public string PhoneNumber = "";
        public string Password = "";

        public void Advise(ICredentialProviderCredentialEvents pcpce)
        {
            this.Events = pcpce;
            BeginStatusPolling();
        }

        public void UnAdvise()
        {
            this.Events = null;
            StopStatusPolling();
        }

        public void CommandLinkClicked(uint dwFieldID)
        {
            throw new NotImplementedException();
        }

        public void GetBitmapValue(uint dwFieldID, IntPtr phbmp)
        {
            if (dwFieldID != 0)
            {
                throw new ArgumentException();
            }

            var asm = Assembly.GetExecutingAssembly();
            var imageStream = asm.GetManifestResourceStream("NotakeyNETProvider.notakey.png");
            var bmp = new Bitmap(imageStream);

            Marshal.WriteIntPtr(phbmp, bmp.GetHbitmap());
        }

        public void GetCheckboxValue(uint dwFieldID, out int pbChecked, out string ppszLabel)
        {
            throw new NotImplementedException();
        }

        public void GetComboBoxValueAt(uint dwFieldID, uint dwItem, out string ppszItem)
        {
            throw new NotImplementedException();
        }

        public void GetComboBoxValueCount(uint dwFieldID, out uint pcItems, out uint pdwSelectedItem)
        {
            throw new NotImplementedException();
        }

        public void GetFieldState(uint dwFieldID, out _CREDENTIAL_PROVIDER_FIELD_STATE pcpfs, out _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE pcpfis)
        {
            if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.BITMAP)
            {
                pcpfs = _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_BOTH;
                pcpfis = _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE.CPFIS_NONE;
            }
            else if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.TITLE)
            {
                pcpfs = _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_DESELECTED_TILE;
                pcpfis = _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE.CPFIS_READONLY;
            }
            else if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.PHONE_INPUT)
            {
                pcpfs = _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_SELECTED_TILE;
                pcpfis = _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE.CPFIS_FOCUSED;
            }
            else if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.PASS_INPUT)
            {
                pcpfs = _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_SELECTED_TILE;
                pcpfis = _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE.CPFIS_NONE;
            }
            else if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.STATUS_LABEL)
            {
                pcpfs = _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_BOTH;
                pcpfis = _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE.CPFIS_READONLY;
            }
            else if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.INSTRUCTION_LABEL)
            {
                pcpfs = _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_SELECTED_TILE;
                pcpfis = _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE.CPFIS_READONLY;
            }
            else if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.SUBMIT_BUTTON)
            {
                pcpfs = _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_SELECTED_TILE;
                pcpfis = _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE.CPFIS_READONLY;
            }
            else
            {
                throw new ArgumentException();
            }
        }

        private void ConfigureUIForWaiting()
        {
            Events.SetFieldString(this, (uint)NotakeyNETProvider.FIELDS.INSTRUCTION_LABEL, "Please wait ...");
            Events.SetFieldState(this, (uint)NotakeyNETProvider.FIELDS.PASS_INPUT, _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_HIDDEN);
            Events.SetFieldState(this, (uint)NotakeyNETProvider.FIELDS.PHONE_INPUT, _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_HIDDEN);
        }

        private void ConfigureUIForEditing()
        {
            Events.SetFieldString(this, (uint)NotakeyNETProvider.FIELDS.INSTRUCTION_LABEL, InstructionsText);
            Events.SetFieldState(this, (uint)NotakeyNETProvider.FIELDS.PASS_INPUT, _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_SELECTED_TILE);
            Events.SetFieldState(this, (uint)NotakeyNETProvider.FIELDS.PHONE_INPUT, _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_SELECTED_TILE);
        }
        
        public void GetSerialization(out _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE pcpgsr, 
            out _CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION pcpcs,
            out string ppszOptionalStatusText, 
            out _CREDENTIAL_PROVIDER_STATUS_ICON pcpsiOptionalStatusIcon)
        {
            ConfigureUIForWaiting();
                
            try
            {
                // pcpcs must always be assigned, even if we do not return any valid information.
                pcpcs = new _CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION();

                var c = new NotakeyBGServerClient();
                if (!("OK".Equals(c.StatusCheckMessage())))
                {
                    ppszOptionalStatusText = "The service is not available. Please try again in a bit. If the problem persists, contact +371 20 208 714.";
                    pcpgsr = _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE.CPGSR_NO_CREDENTIAL_NOT_FINISHED;
                    pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_WARNING;
                    return;
                }

                string uuid = null;
                bool failed = false;
                c.Execute((StreamReader sr) =>
                {
                    bool ok = "OK".Equals(sr.ReadLine());
                    if (ok)
                    {
                        uuid = sr.ReadLine();
                    }
                    else
                    {
                        failed = true;
                    }
                }, "REQUEST_AUTH", PhoneNumber, Password);

                if (failed /* REQUEST_AUTH */)
                {
                    ppszOptionalStatusText = "The specified phone number / password combination is not valid.";
                    pcpgsr = _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE.CPGSR_NO_CREDENTIAL_NOT_FINISHED;
                    pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_ERROR;
                    return;
                }

                bool status = false;
                while (true)
                {
                    bool pending = true;
                    c.Execute(
                        (StreamReader sr) =>
                        {
                            string res = sr.ReadLine();
                            Console.WriteLine("Status: {0}", res);
                            pending = "WAIT".Equals(res);
                            if (!pending)
                            {
                                status = "OK".Equals(res);
                            }
                        }, "STATUS_FOR_REQUEST", uuid);

                    if (!pending) break;
                    Thread.Sleep(1500);
                }

                string local_user = "gints";
                string local_password = "gints";
                if (!status)
                {
                    ppszOptionalStatusText = "The authorization request was denied or expired.";
                    pcpgsr = _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE.CPGSR_NO_CREDENTIAL_NOT_FINISHED;
                    pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_WARNING;
                    return;
                }

                if (PhoneNumber.Equals("20208714"))
                {
                    local_user = "notakey";
                    local_password = "notakey";
                }

                int inCredSize = 1024;
                IntPtr inCredBuffer = Marshal.AllocCoTaskMem(inCredSize);

                if (!LsaWrapper.CredPackAuthenticationBuffer(0, local_user, local_password,
                    inCredBuffer, ref inCredSize))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                //------- FAILED ATTEMPT FOLLOWS:
                uint abPackageId = RetrieveMSV10PackageId("NTLM");

                // TODO: auth
                //var package = new Ms10InteractiveLogon("DESKTOP-V9EBTFE", "notakey", "notakey");

                pcpgsr = _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE.CPGSR_RETURN_CREDENTIAL_FINISHED;
                pcpcs = new _CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION()
                {
                    ulAuthenticationPackage = abPackageId,
                    cbSerialization = (uint)inCredSize,//(uint)package.Length,
                    clsidCredentialProvider = new Guid("77E5F42E-B280-4219-B130-D48BB3932A04"),
                    rgbSerialization = inCredBuffer
                };

                ppszOptionalStatusText = "Done";
                pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_SUCCESS;
            }
            finally
            {
                ConfigureUIForEditing();
            }
        }

        public void GetStringValue(uint dwFieldID, out string ppsz)
        {
            ppsz = "";

            if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.TITLE)
            {
                ppsz = "Authorize with notaKey";
            }
            else if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.INSTRUCTION_LABEL)
            {
                ppsz = InstructionsText;
            }
            else if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.STATUS_LABEL ||
                false//dwFieldID == (uint)NotakeyNETProvider.FIELDS.STATUS_LABEL_DESELECTED
                )
            {
                ppsz = statusLabel;
            }
            else if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.PASS_INPUT)
            {
                ppsz = Password;
            }
            else if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.PHONE_INPUT)
            {
                ppsz = PhoneNumber;
            }
            
            if (dwFieldID >= (uint)NotakeyNETProvider.FIELDS.TOTAL_COUNT)
            {
                throw new ArgumentException();
            }
        }

        public void GetSubmitButtonValue(uint dwFieldID, out uint pdwAdjacentTo)
        {
            if (dwFieldID != (uint)NotakeyNETProvider.FIELDS.SUBMIT_BUTTON)
            {
                throw new ArgumentException();
            }

            pdwAdjacentTo = (uint)NotakeyNETProvider.FIELDS.PASS_INPUT;
        }

        public void ReportResult(int ntsStatus, 
            int ntsSubstatus, 
            out string ppszOptionalStatusText, 
            out _CREDENTIAL_PROVIDER_STATUS_ICON pcpsiOptionalStatusIcon)
        {
            int winError = LsaWrapper.LsaNtStatusToWinError(ntsStatus);
            string errorMessage = new Win32Exception(winError).Message;

            ppszOptionalStatusText = string.Format("Status: {0} - {1}: {2}", ntsStatus, ntsSubstatus, errorMessage);
            if (ntsStatus != 0)
            {
                pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_ERROR;
            }
            else
            {
                pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_SUCCESS;
            }
        }

        public void SetCheckboxValue(uint dwFieldID, int bChecked)
        {
            throw new NotImplementedException();
        }

        public void SetComboBoxSelectedValue(uint dwFieldID, uint dwSelectedItem)
        {
            throw new NotImplementedException();
        }

        public void SetDeselected()
        {
           // throw new NotImplementedException();
            // TODO: remove
            StopStatusPolling();
            
        }

        public void SetSelected(out int pbAutoLogon)
        {
            pbAutoLogon = 0;

        }

        public void SetStringValue(uint dwFieldID, string psz)
        {
            if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.PHONE_INPUT)
            {
                PhoneNumber = psz;
            } else if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.PASS_INPUT)
            {
                Password = psz;
            }
            else
            {
                throw new ArgumentException();
            }
        }

        public void GetUserSid(out string ppsz)
        {
            ppsz = "ThisIsTempUserSID";
        }

        private void BeginStatusPolling()
        {
            statusCheckTokenSource = new CancellationTokenSource();
            var cancellationToken = statusCheckTokenSource.Token;
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    try
                    {
                        var c = new NotakeyBGServerClient();
                        statusLabel = string.Format("Service Status: {0}", c.StatusCheckMessage());
                    }
                    catch (TimeoutException)
                    {
                        statusLabel = "Service Status: network request timed out";
                    }
                    catch (Exception e)
                    {
                        statusLabel = string.Format("Service Status: error ({0})", e.Message);
                    }

                    Debug.Assert(Events != null);
                    if (Events != null)
                    {
                        Events.SetFieldString(this, (uint)NotakeyNETProvider.FIELDS.STATUS_LABEL, statusLabel);
                    }

                    await Task.Delay(5000, cancellationToken);
                }
            }, cancellationToken);
        }

        private void StopStatusPolling()
        {
            statusCheckTokenSource.Cancel();
            statusCheckTokenSource = null;
        }

        private uint RetrieveMSV10PackageId(string name = "MICROSOFT_AUTHENTICATION_PACKAGE_V1_0")
        {
            IntPtr lsaHandle;
            int ntcode;

            ntcode = LsaWrapper.LsaConnectUntrusted(out lsaHandle);
            if (ntcode != 0)
                throw new Win32Exception(LsaWrapper.LsaNtStatusToWinError(ntcode));

            var packageName = new LsaWrapper.LsaStringWrapper(name);
            uint packageId;
            ntcode = LsaWrapper.LsaLookupAuthenticationPackage(lsaHandle, ref packageName._string, out packageId);
            if (ntcode != 0) 
                throw new Win32Exception(LsaWrapper.LsaNtStatusToWinError(ntcode));

            ntcode = LsaWrapper.LsaDeregisterLogonProcess(lsaHandle);
            if (ntcode != 0)
            {
                throw new Win32Exception(LsaWrapper.LsaNtStatusToWinError(ntcode));
            }

            return packageId;
        }
    }
}
