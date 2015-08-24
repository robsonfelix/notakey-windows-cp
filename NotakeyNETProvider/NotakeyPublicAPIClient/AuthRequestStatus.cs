using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Notakey.PublicAPI
{
    public enum AuthRequestStatus
    {
        Pending = 0,
        Approved,
        Denied,
        Expired
    }
}
