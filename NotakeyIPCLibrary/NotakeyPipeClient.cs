using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NotakeyIPCLibrary
{
	public class NotakeyPipeClient
	{
		private const int TimeOutInMilliseconds = 10000;

		public NotakeyPipeClient()
		{

		}

		public void Execute(Action<StreamReader> action, string cmd)
		{
			this.Execute(action, new string[] { cmd });
		}

		/// <summary>
		/// 
		/// </summary>
		/// <exception cref="TimeoutException">If connect to server fails</exception>
		/// <returns></returns>
		public void Execute(Action<StreamReader> action, params string[] cmds)
		{

			string pipeName;
			try
			{
				try
				{
					pipeName = ReadPipeName();
				}
				catch (Exception e)
				{
					Debug.WriteLine($"EXCEPTION: failed to read pipe name: {e.ToString()}");
					throw;
				}
			}
			catch (IOException e)
			{
				if (System.Environment.OSVersion.Platform == PlatformID.Unix ||
					System.Environment.OSVersion.Platform == PlatformID.MacOSX)
				{
					throw new Exception("Failed to connect to server. HINT: on Unix you need to 'mkfifo <pipe_name>'", e);
				}

				throw;
			}

			if (pipeName == null)
			{
				Debug.WriteLine("Pipe client has pipeName null. Invalid situation - throwing.");
				throw new ServerErrorException("Server returned null pipe name");
			}

			Debug.WriteLine($"Connecting to pipe {pipeName}");
			using (var pipeClient = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut))
			{
				pipeClient.Connect(TimeOutInMilliseconds);

				using (var sw = new StreamWriter(pipeClient, Encoding.UTF8, 4096, true))
				{
					using (var sr = new StreamReader(pipeClient, Encoding.UTF8, true, 4096, true))
					{
						foreach (string cmd in cmds)
						{
							sw.WriteLine(cmd);
						}
						sw.Flush();

						action(sr);
					}
				}
			}
		}

		private string ReadPipeName()
		{
            Debug.WriteLine("Reading pipe name from master pipe " + NotakeyPipeServer.MasterPipeName);

			using (NamedPipeClientStream tmpPipeClient =
				   new NamedPipeClientStream(".", NotakeyPipeServer.MasterPipeName, PipeDirection.In))
			{
				Debug.WriteLine("... connecting");
                // Connect to the pipe or wait until the pipe is available.
				tmpPipeClient.Connect(TimeOutInMilliseconds);

				Debug.WriteLine("... reading");
				using (StreamReader sr = new StreamReader(tmpPipeClient))
				{
					var name = sr.ReadLine();
					Debug.WriteLine($"... got pipe name {name}. Returning.");
					return name;
				}
			}
		}

		public string StatusCheckMessage(int timeoutSeconds = 7)
		{
			var msg = "";
			Execute((StreamReader sr) =>
			{
				Debug.WriteLine($"Status check - waiting for readLine...");
				msg = sr.ReadLine();
				Debug.WriteLine($"Status check - got {msg}");
            }, "API_HEALTH_CHECK", timeoutSeconds.ToString());
			return msg;
		}
	}
}
