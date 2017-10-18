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
using System.Security;

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
        
        /// <summary>
        /// Use SecureString, so that secrets are not kept around in memory.
        /// 
        /// Before instantiating with a new value, call DisposePasswordIfNecessary().
        /// 
        /// This is set in the constructor, and should be guaranteed not null afterwards.
        /// </summary>
        public SecureString Password = null;

        /// <summary>
        /// This is validated in the constructor.
        /// </summary>
        ICredentialProviderCredential2 parentCredentialNeverNull;

        /// <summary>
        /// If true, delegate all calls to the parent credential. Otherwise, only
        /// delegate username/password verification.
        /// </summary>
        bool delegateAllToParent;

        public NotakeyNETCredential(ICredentialProviderCredential2 parentCredential, bool delegateAllToParent)
        {
            parentCredentialNeverNull = parentCredential;
            if (parentCredentialNeverNull == null)
            {
                throw new ArgumentNullException(nameof(parentCredential));
            }

            this.delegateAllToParent = delegateAllToParent;

            SafeResetPasswordMemory();
        }

        public void Advise(ICredentialProviderCredentialEvents pcpce)
        {
            if (this.delegateAllToParent)
            {
                try
                {
                    parentCredentialNeverNull.Advise(pcpce);
                } catch (NotImplementedException)
                {
                    // valid scenario
                }
            }
            else
            {
                this.Events = pcpce;
                BeginStatusPolling();
            }
        }

        public void UnAdvise()
        {
            if (this.delegateAllToParent)
            {
                try
                {
                    parentCredentialNeverNull.UnAdvise();
                }
                catch (NotImplementedException)
                {
                    // valid scenario
                }
            } else
            {
                StopStatusPolling();

                // Do not set Events to null before stopping status polling,
                // as status poll callbacks might want to use it.
                this.Events = null;
            }
        }

        public void CommandLinkClicked(uint dwFieldID)
        {
            if (delegateAllToParent)
            {
                parentCredentialNeverNull.CommandLinkClicked(dwFieldID);
            } else
            {
                throw new NotImplementedException();
            }
        }

        public void GetBitmapValue(uint dwFieldID, IntPtr phbmp)
        {
            if (delegateAllToParent)
            {
                parentCredentialNeverNull.GetBitmapValue(dwFieldID, phbmp);
                return;
            }

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
            if (delegateAllToParent)
            {
                parentCredentialNeverNull.GetCheckboxValue(dwFieldID, out pbChecked, out ppszLabel);
                return;
            }

            throw new NotImplementedException();
        }

        public void GetComboBoxValueAt(uint dwFieldID, uint dwItem, out string ppszItem)
        {
            if (delegateAllToParent)
            {
                parentCredentialNeverNull.GetComboBoxValueAt(dwFieldID, dwItem, out ppszItem);
                return;
            }

            throw new NotImplementedException();
        }

        public void GetComboBoxValueCount(uint dwFieldID, out uint pcItems, out uint pdwSelectedItem)
        {
            if (delegateAllToParent)
            {
                parentCredentialNeverNull.GetComboBoxValueCount(dwFieldID, out pcItems, out pdwSelectedItem);
                return;
            }

            throw new NotImplementedException();
        }

        public void GetFieldState(uint dwFieldID, out _CREDENTIAL_PROVIDER_FIELD_STATE pcpfs, out _CREDENTIAL_PROVIDER_FIELD_INTERACTIVE_STATE pcpfis)
        {
            if (delegateAllToParent)
            {
                parentCredentialNeverNull.GetFieldState(dwFieldID, out pcpfs, out pcpfis);
                return;
            }

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
            else if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.USERNAME_INPUT)
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

        public void GetSerialization(out _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE pcpgsr, 
            out _CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION pcpcs,
            out string ppszOptionalStatusText, 
            out _CREDENTIAL_PROVIDER_STATUS_ICON pcpsiOptionalStatusIcon)
        {
            if (delegateAllToParent)
            {
                parentCredentialNeverNull.GetSerialization(
                    out pcpgsr,
                    out pcpcs,
                    out ppszOptionalStatusText,
                    out pcpsiOptionalStatusIcon);
                return;
            }

            /* NOTE: string values for Username and Password are forwarded to the parent credential
             * in the SetStringValue methods (with hardcoded field index values). */
            
            _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE tempPcpgsr;
            parentCredentialNeverNull.GetSerialization(
                out tempPcpgsr, 
                out pcpcs, 
                out ppszOptionalStatusText, 
                out pcpsiOptionalStatusIcon);

            if (tempPcpgsr != _CREDENTIAL_PROVIDER_GET_SERIALIZATION_RESPONSE.CPGSR_RETURN_CREDENTIAL_FINISHED)
            {
                // This does NOT indicate the username/password combination was incorrect. Only that
                // no serialization was returned.
                pcpgsr = tempPcpgsr;
                return;
            }

            // TODO: we should somehow verify if the provided information was valid. Maybe with a call to LsaLogonUser.

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
                }, "REQUEST_AUTH", Username);

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
            if (delegateAllToParent)
            {
                parentCredentialNeverNull.GetStringValue(dwFieldID, out ppsz);
                return;
            }

            ppsz = "";

            if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.TITLE)
            {
                ppsz = "Authorize with Notakey Authenticator";
            }
            else if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.INSTRUCTION_LABEL)
            {
                ppsz = InstructionsText;
            }
            else if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.STATUS_LABEL)
            {
                ppsz = statusLabel;
            }
            else if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.PASS_INPUT)
            {
                ppsz = new System.Net.NetworkCredential(string.Empty, Password).Password;
            }
            else if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.USERNAME_INPUT)
            {
                ppsz = Username;
            }
            
            if (dwFieldID >= (uint)NotakeyNETProvider.FIELDS.TOTAL_COUNT)
            {
                throw new ArgumentException();
            }
        }

        public void GetSubmitButtonValue(uint dwFieldID, out uint pdwAdjacentTo)
        {
            if (delegateAllToParent)
            {
                parentCredentialNeverNull.GetSubmitButtonValue(dwFieldID, out pdwAdjacentTo);
                return;
            }

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
            if (delegateAllToParent)
            {
                parentCredentialNeverNull.ReportResult(ntsStatus, ntsSubstatus, out ppszOptionalStatusText, out pcpsiOptionalStatusIcon);
                return;
            }

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
            if (delegateAllToParent)
            {
                parentCredentialNeverNull.SetCheckboxValue(dwFieldID, bChecked);
                return;
            }

            throw new NotImplementedException();
        }

        public void SetComboBoxSelectedValue(uint dwFieldID, uint dwSelectedItem)
        {
            if (delegateAllToParent)
            {
                parentCredentialNeverNull.SetComboBoxSelectedValue(dwFieldID, dwSelectedItem);
                return;
            }

            throw new NotImplementedException();
        }

        public void SetDeselected()
        {
            if (delegateAllToParent)
            {
                parentCredentialNeverNull.SetDeselected();
                return;
            }

            StopStatusPolling();
            
        }

        public void SetSelected(out int pbAutoLogon)
        {
            if (delegateAllToParent)
            {
                parentCredentialNeverNull.SetSelected(out pbAutoLogon);
                return;
            }

            pbAutoLogon = 0;
        }

        public void SetStringValue(uint dwFieldID, string psz)
        {
            if (delegateAllToParent)
            {
                parentCredentialNeverNull.SetStringValue(dwFieldID, psz);
                return;
            }

            if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.USERNAME_INPUT)
            {
                Username = psz;
                parentCredentialNeverNull.SetStringValue(1, psz);   // HARDCODED for PasswordProvider
            } else if (dwFieldID == (uint)NotakeyNETProvider.FIELDS.PASS_INPUT)
            {
                // Having a string reference defeats the purpose of having a SecureString I guess. Maybe
                // in the future this can be refactored with unsafe code, to not 
                // instantiate C# strings (and instead pass in a char*)
                //
                // Consider the SecureString implementation a first-step towards a better solution,
                // not a complete solution.

                SafeResetPasswordMemory(psz);
                parentCredentialNeverNull.SetStringValue(2, psz);   // HARDCODED for PasswordProvider
            }
            else
            {
                throw new ArgumentException();
            }
        }

        public void GetUserSid(out string ppsz)
        {
            parentCredentialNeverNull.GetUserSid(out ppsz);
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
                        Events.SetFieldString(this, (uint)NotakeyNETProvider.FIELDS.STATUS_LABEL, statusLabel);
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

        /// <summary>
        /// If set - dispose the Password value, and set the reference to a new instance of SecurePassword.
        /// 
        /// Avoid setting the variable to null, or we will have to check for null everywhere a string value is needed.
        /// </summary>
        private void SafeResetPasswordMemory(string newValue = "")
        {
            Password?.Dispose();

            unsafe
            {
                fixed (char* c_str = newValue)
                {
                    Password = new SecureString(c_str, newValue.Length);
                }
            }
        }

        private void ConfigureUIForWaiting()
        {
            Events.SetFieldString(this, (uint)NotakeyNETProvider.FIELDS.INSTRUCTION_LABEL, "Please wait ...");
            Events.SetFieldState(this, (uint)NotakeyNETProvider.FIELDS.PASS_INPUT, _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_HIDDEN);
            Events.SetFieldState(this, (uint)NotakeyNETProvider.FIELDS.USERNAME_INPUT, _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_HIDDEN);
        }

        private void ConfigureUIForEditing()
        {
            Events.SetFieldString(this, (uint)NotakeyNETProvider.FIELDS.INSTRUCTION_LABEL, InstructionsText);
            Events.SetFieldState(this, (uint)NotakeyNETProvider.FIELDS.PASS_INPUT, _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_SELECTED_TILE);
            Events.SetFieldState(this, (uint)NotakeyNETProvider.FIELDS.USERNAME_INPUT, _CREDENTIAL_PROVIDER_FIELD_STATE.CPFS_DISPLAY_IN_SELECTED_TILE);
        }
    }
}
