using pGina.Shared.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotakeyPGINA
{
    public class NotakeyPGINA : pGina.Shared.Interfaces.IPluginAuthentication
    {
        public string Name
        {
            get { return "notaKey Plugin"; }
        }

        public string Description
        {
            get { return "2-factor authorization, using a password and a smartphone"; }
        }

        private static readonly Guid m_uuid = new Guid("9A9FAD6C-4579-4985-9796-727AB3D8F45F");
        public Guid Uuid
        {
            get { return m_uuid; }
        }

        public string Version
        {
            get
            {
                return System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

        public void Starting() { }

        public void Stopping() { }

        public BooleanResult AuthenticateUser(SessionProperties properties)
        {
            UserInformation userInfo = properties.GetTrackedSingle<UserInformation>();

            if (userInfo.Username.Equals("20208714") && userInfo.Password.Contains("notakey"))
            {
                // Successful authentication
                return new BooleanResult() { Success = true };
            }
            // Authentication failure
            return new BooleanResult() { Success = false, Message = "Incorrect phone number or password." };
        }

    }
}
