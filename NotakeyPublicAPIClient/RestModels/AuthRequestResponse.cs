using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notakey.PublicAPI.RestModels
{
    internal class AuthRequestResponse : BaseModel
    {
        public bool Approved { get; set; }
        public bool Denied { get; set; }
        public string Description { get; set; }
        public string Uuid { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime ExpiresAt { get; set; }

        public bool IsExpired
        {
            get
            {
                return ExpiresAt < DateTime.Now;
            }
        }

        public AuthRequestStatus Status
        {
            get
            {
                if (IsExpired) return AuthRequestStatus.Expired;
                if (Approved) return AuthRequestStatus.Approved;
                if (Denied) return AuthRequestStatus.Denied;
                return AuthRequestStatus.Pending;
            }
        }
    }
}
