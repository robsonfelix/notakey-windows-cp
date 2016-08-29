# Notakey Windows Credential Provider

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



