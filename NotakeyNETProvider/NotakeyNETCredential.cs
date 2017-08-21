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
using System.IO.Pipes;

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
        static string InstructionsText = "You will have to authorize the request using Notakey Authenticator on your smartphone.";

        /// <summary>
        /// Placeholder for lazy-loaded service status text
        /// </summary>
        static string statusLabel = "Determining Status…";
        private CancellationTokenSource statusCheckTokenSource = null;

        public string Username = "";
        public string Password = "";

        /// <summary>
        /// This is validated in the constructor.
        /// </summary>
        ICredentialProviderCredential2 _parentCredentialNeverNull;

        public NotakeyNETCredential(ICredentialProviderCredential2 parentCredential)
        {
            _parentCredentialNeverNull = parentCredential ?? throw new ArgumentNullException(nameof(parentCredential));
        }

        public void Advise(ICredentialProviderCredentialEvents pcpce)
        {
            this.Events = pcpce;
            BeginStatusPolling();
        }

        public void UnAdvise()
        {
            StopStatusPolling();

            // Do not set Events to null before stopping status polling,
            // as status poll callbacks might want to use it.
            this.Events = null;
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
            if (dwFieldID == (uint)NotakeyNETProvider_Impl.FIELDS.BITMAP)
            {
                pcpfs = _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_BOTH;
                pcpfis = _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE.CPFIS_NONE;
            }
            else if (dwFieldID == (uint)NotakeyNETProvider_Impl.FIELDS.TITLE)
            {
                pcpfs = _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_DESELECTED_TILE;
                pcpfis = _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE.CPFIS_READONLY;
            }
            else if (dwFieldID == (uint)NotakeyNETProvider_Impl.FIELDS.USERNAME_INPUT)
            {
                pcpfs = _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_SELECTED_TILE;
                pcpfis = _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE.CPFIS_FOCUSED;
            }
            else if (dwFieldID == (uint)NotakeyNETProvider_Impl.FIELDS.PASS_INPUT)
            {
                pcpfs = _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_SELECTED_TILE;
                pcpfis = _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE.CPFIS_NONE;
            }
            else if (dwFieldID == (uint)NotakeyNETProvider_Impl.FIELDS.STATUS_LABEL)
            {
                pcpfs = _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_BOTH;
                pcpfis = _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE.CPFIS_READONLY;
            }
            else if (dwFieldID == (uint)NotakeyNETProvider_Impl.FIELDS.INSTRUCTION_LABEL)
            {
                pcpfs = _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_SELECTED_TILE;
                pcpfis = _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE.CPFIS_READONLY;
            }
            else if (dwFieldID == (uint)NotakeyNETProvider_Impl.FIELDS.SUBMIT_BUTTON)
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
            Events.SetFieldString(this, (uint)NotakeyNETProvider_Impl.FIELDS.INSTRUCTION_LABEL, "Please wait ...");
            Events.SetFieldState(this, (uint)NotakeyNETProvider_Impl.FIELDS.PASS_INPUT, _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_HIDDEN);
            Events.SetFieldState(this, (uint)NotakeyNETProvider_Impl.FIELDS.USERNAME_INPUT, _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_HIDDEN);
        }

        private void ConfigureUIForEditing()
        {
            Events.SetFieldString(this, (uint)NotakeyNETProvider_Impl.FIELDS.INSTRUCTION_LABEL, InstructionsText);
            Events.SetFieldState(this, (uint)NotakeyNETProvider_Impl.FIELDS.PASS_INPUT, _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_SELECTED_TILE);
            Events.SetFieldState(this, (uint)NotakeyNETProvider_Impl.FIELDS.USERNAME_INPUT, _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_SELECTED_TILE);
        }
        
        public void GetSerialization(out _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE pcpgsr, 
            out _CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION pcpcs,
            out string ppszOptionalStatusText, 
            out _CREDENTIAL_PROVIDER_STATUS_ICON pcpsiOptionalStatusIcon)
        {
            _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE tempPcpgsr;
            _parentCredentialNeverNull.GetSerialization(
                out tempPcpgsr, 
                out pcpcs, 
                out ppszOptionalStatusText, 
                out pcpsiOptionalStatusIcon);

            if (tempPcpgsr != _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE.CPGSR_RETURN_CREDENTIAL_FINISHED)
            {
                // Failure scenario
                pcpgsr = tempPcpgsr;
                return;
            }

            /* Now append Notakey authentication (only after the parent credential
             * has succeeded */

			Debug.WriteLine("Entering GetSerialization. Configuring UI...");
            ConfigureUIForWaiting();
			Debug.WriteLine("... configured UI");
                
            try
            {
                var c = new NotakeyPipeClient();
				Debug.WriteLine("... created client");

				var statusCheck = c.StatusCheckMessage();
                if (!("OK".Equals(statusCheck)))
                {
					Debug.WriteLine($"... status check not OK: {statusCheck}");
                    ppszOptionalStatusText = "The service is not available. Please try again in a bit.";
                    pcpgsr = _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE.CPGSR_NO_CREDENTIAL_NOT_FINISHED;
                    pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_WARNING;
                    return;
                }

				Debug.WriteLine("... status check OK");

                string computerName = System.Environment.MachineName;
                string description = string.Format("Do you wish to authenticate user '{0}' on computer '{1}'?", Username, computerName);

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
                }, "REQUEST_AUTH", Username, "Windows Login", description);

                if (failed /* REQUEST_AUTH */)
                {
                    // Setting all values - even pcpcs - so that no information
                    // can leak through from the successfull call.
					pcpgsr = _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE.CPGSR_NO_CREDENTIAL_NOT_FINISHED;
					pcpcs = new _CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION();
                    ppszOptionalStatusText = "The specified username / password combination is not valid.";
                    pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_ERROR;
                    return;
                }

                bool status = false;
                string errorMessage = null;
                
                c.Execute(
                    (StreamReader sr) =>
                    {
                        string res = sr.ReadLine();
                        if (res == "OK")
                        {
                            status = (sr.ReadLine() == "TRUE");
                        }
                        else
                        {
                            errorMessage = sr.ReadLine();
                        }
                    }, "SYNC_REQUEST_STATUS", uuid);

                if (!status /* SYNC_REQUEST_STATUS */)
                {
					// Setting all values - even pcpcs - so that no information
					// can leak through from the successfull call.
					pcpcs = new _CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION();
                    pcpgsr = _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE.CPGSR_NO_CREDENTIAL_NOT_FINISHED;
					ppszOptionalStatusText = errorMessage ?? "The authorization request could not be processed.";
					pcpsiOptionalStatusIcon = _CREDENTIAL_PROVIDER_STATUS_ICON.CPSI_WARNING;
                    return;
                }

                // Success. Return the pcpgsr from the parent-credential call.
                pcpgsr = tempPcpgsr;
            }
            finally
            {
                ConfigureUIForEditing();
            }
        }

        public void GetStringValue(uint dwFieldID, out string ppsz)
        {
            ppsz = "";

            if (dwFieldID == (uint)NotakeyNETProvider_Impl.FIELDS.TITLE)
            {
                ppsz = "Authorize with Notakey Authenticator";
            }
            else if (dwFieldID == (uint)NotakeyNETProvider_Impl.FIELDS.INSTRUCTION_LABEL)
            {
                ppsz = InstructionsText;
            }
            else if (dwFieldID == (uint)NotakeyNETProvider_Impl.FIELDS.STATUS_LABEL ||
                false//dwFieldID == (uint)NotakeyNETProvider.FIELDS.STATUS_LABEL_DESELECTED
                )
            {
                ppsz = statusLabel;
            }
            else if (dwFieldID == (uint)NotakeyNETProvider_Impl.FIELDS.PASS_INPUT)
            {
                ppsz = Password;
            }
            else if (dwFieldID == (uint)NotakeyNETProvider_Impl.FIELDS.USERNAME_INPUT)
            {
                ppsz = Username;
            }
            
            if (dwFieldID >= (uint)NotakeyNETProvider_Impl.FIELDS.TOTAL_COUNT)
            {
                throw new ArgumentException();
            }
        }

        public void GetSubmitButtonValue(uint dwFieldID, out uint pdwAdjacentTo)
        {
            if (dwFieldID != (uint)NotakeyNETProvider_Impl.FIELDS.SUBMIT_BUTTON)
            {
                throw new ArgumentException();
            }

            pdwAdjacentTo = (uint)NotakeyNETProvider_Impl.FIELDS.PASS_INPUT;
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
            if (dwFieldID == (uint)NotakeyNETProvider_Impl.FIELDS.USERNAME_INPUT)
            {
                Username = psz;
            } else if (dwFieldID == (uint)NotakeyNETProvider_Impl.FIELDS.PASS_INPUT)
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
			Debug.WriteLine($"Entered BeginStatusPolling");

            statusCheckTokenSource = new CancellationTokenSource();
            var cancellationToken = statusCheckTokenSource.Token;

            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    NotakeyPipeClient c;
                    try
                    {
						Debug.WriteLine($"BeginStatusPolling - creating client");
                        c = new NotakeyPipeClient();
						Debug.WriteLine($"BeginStatusPolling - created client. Verifying status ...");
                        statusLabel = string.Format("Service Status: {0}", c.StatusCheckMessage());
                    }
                    catch (TimeoutException)
                    {
                        statusLabel = "Service Status: health-check request timed out. Is the background service running?";
                    }
                    catch (Exception e)
                    {
                        statusLabel = string.Format("Service Status: error ({0})", e.Message);
                    }

                    if (Events != null)
                    {
                        Events.SetFieldString(this, (uint)NotakeyNETProvider_Impl.FIELDS.STATUS_LABEL, statusLabel);
                    }

                    await Task.Delay(5000, cancellationToken);
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                }
            }, cancellationToken);
        }

        private void StopStatusPolling()
        {
            if (statusCheckTokenSource != null)
            {
                statusCheckTokenSource.Cancel();
                statusCheckTokenSource = null;
            }
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
