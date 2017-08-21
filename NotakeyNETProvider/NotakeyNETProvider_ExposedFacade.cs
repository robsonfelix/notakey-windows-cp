using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CredentialProviders;
using System.Runtime.InteropServices;

namespace NotakeyNETProvider
{
    /// <summary>
    /// Notakey NET Provider facade.
    /// 
    /// This class is exposed as a COM object. Internally,
    /// it routes ICredentialProvider calls to either a parent provider 
    /// (e.g. so that we do not have to implement password change scenarios,
    /// or so we can be compatible with in-house providers etc.), or
    /// our own NotakeyNETProvider_Impl interface.
    /// 
    /// This should be the only provider, which is exposed as a COM object.
    /// 
    /// This class should only proxy calls around, and not contain any logic
    /// itself (except for instantiating target implementations).
    /// </summary>
    [Guid("77E5F42E-B280-4219-B130-D48BB3932A04")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    [ProgId("NotakeyNETProvider.NotakeyNETProvider")]
    public class NotakeyNETProvider_ExposedFacade : ICredentialProvider
    {
        private NotakeyNETProvider_Impl _notakeyProvider = null;

        /// <summary>
        /// The parent provider. This is loaded as a COM object in 
        /// SetUsageScenario, and used for handling password reset, and
        /// underlying authentication (so that we need to handle just
        /// the Notakey part).
        /// 
        /// Do not use this directly, instead pass an Action/Func to 
        /// HandleScenarios, which will do provider validation too.
        /// 
        /// This CAN be null (but shouldn't). It can only be instantiated
        /// in the first call to this interface, which is SetUsageScenario.
        /// </summary>
        private ICredentialProvider _parentProvider = null;

        /// <summary>
        /// We keep track of the usage scenario, because for password-reset
        /// we will forward every method to the parentProvider.
        /// </summary>
        private _CREDENTIAL_PROVIDER_USAGE_SCENARIO usageScenario;
        private uint usageScenarioFlags;

        public void Advise(ICredentialProviderEvents pcpe, ulong upAdviseContext)
        {
            DetermineProvider(provider => provider.Advise(pcpe, upAdviseContext));
        }

        public void UnAdvise()
        {
            DetermineProvider(provider => provider.UnAdvise());
        }

        public void GetCredentialAt(uint dwIndex, out ICredentialProviderCredential ppcpc)
        {
            ppcpc = (ICredentialProviderCredential)DetermineProvider(provider =>
            {
                ICredentialProviderCredential lambdaResult = null;
                provider.GetCredentialAt(dwIndex, out lambdaResult);
                return lambdaResult;
            });
        }

        public void GetCredentialCount(out uint pdwCount, out uint pdwDefault, out int pbAutoLogonWithDefault)
        {
            // These are needed because we can not use/set "out" params from within a lambda
            uint result_pdwCount = 0;
            uint result_pdwDefault = 0;
            int result_pbAutoLogonWithDefault = 0;
            bool didSet = false;

			DetermineProvider(provider => {
                provider.GetCredentialCount(out result_pdwCount, out result_pdwDefault, out result_pbAutoLogonWithDefault);
                didSet = true;
            });

            if (!didSet) {
                throw new InvalidOperationException("DetermineProvider did not invoke the callback (did it happen on a different thread?)");
            }

            pdwCount = result_pdwCount;
            pdwDefault = result_pdwDefault;
            pbAutoLogonWithDefault = result_pbAutoLogonWithDefault;
        }

        public void GetFieldDescriptorAt(uint dwIndex, IntPtr ppcpfd)
        {
            DetermineProvider(provider => provider.GetFieldDescriptorAt(dwIndex, ppcpfd));
        }

        public void GetFieldDescriptorCount(out uint pdwCount)
        {
            pdwCount = (uint)DetermineProvider(provider =>
            {
                uint result = 0;
                provider.GetFieldDescriptorCount(out result);
                return result;
            });
        }

        public void SetSerialization(ref _CREDENTIAL_PROVIDER_CREDENTIAL_SERIALIZATION pcpcs)
        {
            /* We don't use HandleScenarios here, because there is no reason why
             * SetSerialization should not be passed to the parent provider, 
             * regardless of the usage scenario. */
            if (_parentProvider == null)
            {
                throw new InvalidOperationException("_parentProvider not set for SetSerialization");
            }
            _parentProvider.SetSerialization(ref pcpcs);
        }

        public void SetUsageScenario(_CREDENTIAL_PROVIDER_USAGE_SCENARIO cpus, uint dwFlags)
        {
            string CLSID_AsRequired = "60b78e88-ead8-445c-9cfd-0b87f74ea6cd";

			Type comType = Type.GetTypeFromCLSID(new Guid(CLSID_AsRequired));
            var instance = Activator.CreateInstance(comType);

            // Using an exception-generating cast on purpose (instead of " as ")
            _parentProvider = (ICredentialProvider)instance;
            _notakeyProvider = new NotakeyNETProvider_Impl(_parentProvider);

			/**
             * NOTE: to get a non-default provider by CLSID, do the following:
             * 
             *   string CLSID_AsRequired = "F8A0B131-5F68-486c-8040-7E8FC3C85BB6";
             * 
             *   Type comType = Type.GetTypeFromCLSID(new Guid(CLSID_AsRequired));
             *   var instance = Activator.CreateInstance(comType);
             *   ICredentialProvider casti = (ICredentialProvider) instance;
             * 
             * DO NOTE that the casting will FAIL if performed in a regular application. It
             * must be done in the CP context.
             */

			_parentProvider.SetUsageScenario(cpus, dwFlags);

			usageScenario = cpus;
			usageScenarioFlags = dwFlags;
        }

        /// <summary>
        /// When we handle password change, we want to perform provider 
        /// verification every time. This function wraps that.
        /// 
        /// We return "object" because many COM methods work via "out" params,
        /// which are not lambda-compatible. So we return values in the lambdas,
        /// which this function passes on (and the values are cast at the
        /// call site).
        /// </summary>
        /// <param name="normalScenario">Normal scenario.</param>
        /// <param name="passwordChangeScenario">Password change scenario.</param>
		private object DetermineProvider(Func<ICredentialProvider, object> lambda)
		{
            ICredentialProvider cp = null;

			switch (usageScenario)
			{
				case _CREDENTIAL_PROVIDER_USAGE_SCENARIO.CPUS_CHANGE_PASSWORD:
                    cp = _parentProvider;
                    break;
				default:
                    cp = _notakeyProvider;
                    break;
			}

            if (cp == null) {
                throw new InvalidOperationException(string.Format("Provider 'null' for scenario {0}", usageScenario));
            }
            return lambda(cp);
		}

        private void DetermineProvider(Action<ICredentialProvider> lambda)
        {
            DetermineProvider(provider => { lambda(provider); return null; });
        }
    }
}
