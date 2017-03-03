# Notakey Windows Credential Provider

[![Branch status](https://ci.appveyor.com/api/projects/status/0o4qpa3idgl6y7ya?svg=true)](https://ci.appveyor.com/project/kirsis/notakey-windows-cp)

[![Master status](https://ci.appveyor.com/api/projects/status/0o4qpa3idgl6y7ya/branch/master?svg=true)](https://ci.appveyor.com/project/kirsis/notakey-windows-cp/branch/master)


## CredUIInvokerNET

Invokes the Windows privilege escalation UI (for testing)

## NotakeyBGService

Windows Service for communicating with the login screen plugin. This service
performs the actual communication with the Notakey API (and this is
the service that needs to be configured to bind to the correct endpoints etc.)

Communicates via named pipes.

## Notakey.SDK

Used by NotakeyBGService to perform Notakey verification

## NotakeyBGServiceTestClient

Communicates with the NotakeyBGService to verify it reacts as expected.

Communicates via named pipes.

## NotakeyIPCLibrary

Facilitates communication between BG service and login plugin.

## NotakeyNETProvider

Actual credential provider (Login screen plugin)

To build this, you need:

- Windows SDK (which should install credentialprovider.idl)
- generate a DLL (for the appropriate architecture) from the idl file (see
  gen.bat under generate\_CP\_dll\_from\_idl\\)
- after you build the NotakeyNETProvider.dll, it needs to be registered as a COM
  provider assembly (see register.bat step 1).

  NOTE: the /codebase param is required, if the assembly is not in GAC
- when the assembly is registered as a COM provider, see register.reg to
  register it as a CredentialProvider

At this point, the CredUIInvokerNET application should display Notakey as an option


