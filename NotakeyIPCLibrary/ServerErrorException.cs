using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotakeyIPCLibrary
{
	class ServerErrorException : ApplicationException
	{
		public ServerErrorException(string p)
			: base(p)
		{

		}
	}
}
