<p class="hero">
<img src="images/hero.png" alt="Logon using RDP" title="Logon using RDP" />
</p>

# Introduction

The Notakey Credential Provider (NtkCp) is a Windows plugin, which extends
the logon UI with a new mechanism, which injects Notakey 2FA in the normal
logon scenario.

# Technical Summary

NtkCp consists of 2 components:

- a Windows COM component, which implements the [ICredentialProvider](https://msdn.microsoft.com/en-us/library/windows/desktop/bb776042\(v=vs.85\).aspx) and
  [ICredentialProviderCredential2](https://msdn.microsoft.com/en-us/library/windows/desktop/hh706912\(v=vs.85\).aspx) interfaces.
- a Windows service, which communicates with the credential provider via named pipes,
  and with a Notakey API endpoint.

The credential provider provides a username and password input fields. The provided
username is sent to the Notakey API, to request approval on the user's smartphone.

<aside class="notice">
The password is <em>never</em> sent remotely by the Notakey credential provider. It is
forwarded to the underlying system.
</aside>

If the username is not found (i.e. the user does not exist or has not been
onboarded on the Notakey server), the login attempt will fail with a message.

If the username is found, and the attempt is denied, the logon attempt will fail with an error.

If the user approves the logon attempt, then the provided username and password are processed as
they would normally. If the entered password is incorrect, then the logon attempt will
fail with a message.

The CLSID for the credential provider is `77E5F42E-B280-4219-B130-D48BB3932A04`.

# System Requirements

The credential provider requires .NET v4.5, and
a supported version of the Windows operating system.

| OS Type    | Minimum Version     |
|------------|---------------------|
| Client     | Windows 8           |
| Server     | Windows Server 2012 |

# Installation Instructions (*.zip)

```shell
# The expected ZIP package contents
package.zip
├── NotakeyBGService
│   ├── winsw.exe
│   ├── winsw.xml
│   └── bin
│       └── Release
│           ├── *.dll
│           ├── *.xml
│           └── NotakeyBGService.exe
├── NotakeyNETProvider
│   └── bin
│       └── x64
│           └── Release
│               ├── *.dll
│               └── *.xml
├── register.bat
├── register.reg
└── unregister.bat
```

Place the contents of the package in the desired location (e.g. `C:\ntkcp`)
and then run `register.bat` as administrator.

This script will create a new system service (`Notakey BG Service`), and
register the credential provider in the registry.

To remove the provider from the system, run `unregister.bat` as the administrator.

<aside class="notice">
After running <code>register.bat</code>, be sure to make the service restart
on failure. This will prevent the credential provider from becoming unusable, in
case of an unexpected issue.
</aside>

## 32-bit systems

On 32-bit systems, the package should contain a `register32.bat` and `unregister32.bat`
file. These should be used to register the service and credential provider.

There are no other user-facing changes between 32-bit and 64-bit packages.

# Configuring the API Endpoint

After installation, the background service must be configured to connect to the desired Notakey API endpoint.

This is done by provisioning defined registry keys. 

Below is an example of configuration for 64-bit Windows machines. You can save the text to file with *.reg extension and
provision the registry by importing the text file with regedit. 

<aside class="notice">
Please remember to adjust ServiceURL and ServiceID values 
before import. 
</aside>

## 64-bit systems

```shell
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Notakey]

[HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Notakey\WindowsCP]
"ServiceURL"="https://demo.notakey.com/api/"
"ServiceID"="65af8d56-b7d9-49b9-86c6-595dc440d933"
"MessageTtlSeconds"=dword:0000001e
"MessageActionTitle"="Winlogin"
"MessageDescription"="Proceed as {0} on server {1}?"
"AuthCreateTimeoutSecs"=dword:00000014
"AuthWaitTimeoutSecs"=dword:0000003c
```

## 32-bit systems

```shell
Windows Registry Editor Version 5.00

[HKEY_LOCAL_MACHINE\SOFTWARE\Notakey]

[HKEY_LOCAL_MACHINE\SOFTWARE\Notakey\WindowsCP]
"ServiceURL"="https://demo.notakey.com/api/"
"ServiceID"="65af8d56-b7d9-49b9-86c6-595dc440d933"
"MessageTtlSeconds"=dword:0000001e
"MessageActionTitle"="Winlogin"
"MessageDescription"="Proceed as {0} on server {1}?"
"AuthCreateTimeoutSecs"=dword:00000014
"AuthWaitTimeoutSecs"=dword:0000003c
```

## Description of configuration options

| Name   |  Default |  Description  |
|--------|----------|---------------|
|ServiceURL| \<none\> | API endpoint URL. Has to end with /api/ |
|ServiceID| \<none\> | Service ID as displayed in NAS dashboard |
|MessageTtlSeconds|30| The validity duration of auth request |
|MessageActionTitle|Windows login| Title for auth request |
|MessageDescription|Do you wish to authenticate user {0} on computer {1}?| The message body of auth request |
|AuthCreateTimeoutSecs|10| The duration which WCP waits for response from NAS API endpoint for new auth request generation. This value cannot exceed 100 seconds | 
|AuthWaitTimeoutSecs|30| The time during which auth request has to be processed. This value cannot exceed 100 seconds and has to be aligned with MessageTtlSeconds | 


# Legacy configuration

If no changes are imported to registry the service supports alternate configuration using
xml file. 

This is done by modifying the `NotakeyBGService\winsw.xml` file. This file should contain
three argument tags:

```xml
<argument>https://demo.notakey.com/api/</argument>
<argument>65af8d56-b7d9-49b9-86c6-595dc440d933</argument>
<argument>/unattended</argument>
```

- The first argument should be a Notakey API endpoint (without the version number)
  with a trailing slash.
- The second argument should be a Notakey application access ID value. This value
  can be found in the Notakey dashboard, when viewing a specific application.
- The third argument should be left `/unattended`.

# Disabling Other Ways to Authenticate

Each credential provider on a system can be used on its own. Installing and activating
NtkCp does not disable the default ways to authenticate (e.g. via a simple username/password).

To prevent users circumventing the more secure Notakey credential provider, other
providers need to be disabled.

<aside class="notice">
Note that the system will allow the default credential providers when booted in
safe mode. This is by design.
</aside>

To do this:

- open local Group Policy editor
- navigate to `Computer Configuration -> Administrative Templates -> System -> Logon`
- find the policy `Exclude credential providers` on the right side
- right click `Exclude credential providers`, click `Edit`, click `Enabled` and enter all the credential provider identifiers (CLSID) (comma-separated), which are to be excluded during authentication
- click OK to save the changes.

<aside class="notice">
NOTE: all credential providers and their CLSID values can be seen in the Registry under the key: HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers.
</aside>

## Non-domain Workstations

If changing group policy is impossible (e.g. if the workstation runs Windows Home edition, or is not a part of a domain),
then you can still disable credential providers by editing the registry manually.

The registry key `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\Credential Providers`
lists all the available credential providers on the local system.

Right click on a credential provider's CLSID (which should be disabled), and add a new `DWORD (32-bit) Value` with the name `Disabled` and value `1`.

This provider will be excluded from future login screens.

# Determining Provider CLSID

One way to determine the CLSID of a credential provider, is to authenticate
yourself using it.

Then open the Registry Editor, and navigate to `HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Authentication\LogonUI`.

There will be a key named `LastLoggedOnProvider`, which will contain the CLSID
value of the provider in question.

You may then use this CLSID to disable this specific provider.

## Known Provider CLSIDs (Windows 10)

This is a non-exhaustive reference of credential providers and their
respective CLSID values on Windows 10.

Some of these values may be the same on other Windows versions.

<aside class="notice">
The CLSID for NtkCp is {77E5F42E-B280-4219-B130-D48BB3932A04}.
</aside>

| Name   |  CLSID |
|--------|--------|
|NtkCp   | {77E5F42E-B280-4219-B130-D48BB3932A04} | 
|Smartcard Reader Selection Provider|  {1b283861-754f-4022-ad47-a5eaaa618894}|
|Smartcard WinRT Provider | {1ee7337f-85ac-45e2-a23c-37c753209769}|
|PicturePasswordLogonProvider | {2135f72a-90b5-4ed3-a7f1-8bb705ac276a}|
|GenericProvider |{25CBB996-92ED-457e-B28C-4774084BD562}|
|PasswordProvider | {60b78e88-ead8-445c-9cfd-0b87f74ea6cd}|
|PasswordProvider\LogonPasswordReset |{8841d728-1a76-4682-bb6f-a9ea53b4b3ba}|
|FaceCredentialProvider | {8AF662BF-65A0-4D0A-A540-A338A999D36F}|
|Smartcard Credential Provider |{8FD7E19C-3BF7-489B-A72C-846AB3678C96}|
|Smartcard Pin Provider | {94596c7e-3744-41ce-893e-bbf09122f76a}|
|WinBio Credential Provider | {BEC09223-B018-416D-A0AC-523971B639F5}|
|IrisCredentialProvider | {C885AA15-1764-4293-B82A-0586ADD46B35}|
|PINLogonProvider | {cb82ea12-9f71-446d-89e1-8d0924e1256e}|
|NGC Credential Provider |{D6886603-9D2F-4EB2-B667-1971041FA96B}|
|WLIDCredentialProvider | {F8A0B131-5F68-486c-8040-7E8FC3C85BB6}|

# Network Connectivity

The background service will attempt to connect to its specified Notakey endpoint.

If the endpoint URL uses `https://`, then port `443` will be used. Otherwise,
the port `80` will be used.

There are no expected inbound connections.

# Log Files

The credential provider does not perform any logging.

However, the background service will create log files in the package's
`NotakeyBGService` folder.

## winsw.err.log

This file will contain information about errors.

## winsw.out.log

This file will contain informational output without errors.

# Status Messages

The logon UI will provide a status message, which reflects the status of the
background service.

The status will be re-checked every 10 seconds. Upon failure, the status check interval
will become progressively larger (exponential backoff).

## Service Status: OK

This message means that the background service is operational, and accessible,
and that the specified API endpoint is valid and reachable.

## Service Status: health-check request timed out. Is the background service running?

This message means that the background service is not running, or there is a permission
problem, which blocks the logon UI from communicating with it, using named pipes.

Double-check if the service is started, and if its identity is not restricted
from using named pipes.

## Service Status: service can not connect to API. Check network connectivity and API parameters.

The background service is operational, but the API endpoint is not reachable.

Double-check network connectivity, firewall rules and the API endpoint URL.

## Service Status: API call timed out.

The background service is operational, and the API endpoint was reachable at some point,
but not anymore.

Double-check network connectivity, and if the Notakey server can be reached.

## Service Status: error (&lt;error message&gt;)

This is a generic error message for unexpected issues.

# FAQ

## NtkCp can not be registered anymore (but it used to work)

NtkCp sometimes can not be registered, but Windows does not report any errors. One example situation is when a working NtkCp instance
is unregistered, and re-registered (possibly after moving files to a different location).

If registering gives no indication of errors, but the credential provider does not become available, try the following steps:

- remove the registry entry `HKEY_CLASSES_ROOT\CLSID\{77E5F42E-B280-4219-B130-D48BB3932A04}`
- register NtkCp
- reboot the system

## Does NtkCp send the locally entered password to a remote server?

No, only the username is sent to the Notakey API.

## Does NtkCp perform any username transormations, before sending it to the Notakey API?

No, the username is sent as-is.

## Does NtkCp work with local users or domain users?

NtkCp works with both local and domain users. The entered username
must match the username that has been onboarded in the Notakey Dashboard.

## Can NtkCp be used in an environment that requires smartcard logon?

No, NtkCp is a proxy for the normal username/password logon method.

## Can't the users choose a different credential method, and sidestep Notakey authentication?

If other credential providers are enabled, the users will be able to use them
and sidestep Notakey authentication.

To mitigate this, you can disable other credential providers.

## Can users use safe mode to sidestep the Notakey credential provider?

Yes, in safe mode, the Notakey credential provider can be avoided. The system
will fallback to the default system credential providers.

## Can this be used to protect remote servers?

NtkCp can be used together with Remote Desktop Protocol (RDP).

## I have an issue, when Network Level Access (NLA) is enabled

See:

- [When accessing a remote server, the Notakey login option fails](#when-accessing-a-remote-server-the-notakey-login-option-fails)
- [Why is the Notakey logon option missing, when accessing a remote server via RDP?](#why-is-the-notakey-logon-option-missing-when-accessing-a-remote-server-via-rdp)
- [Why am I prompted to authenticate twice (over Remote Desktop Protocol)](#why-am-i-prompted-to-authenticate-twice-over-remote-desktop-protocol)
- [How can I turn off Network Level Access for Remote Desktop connections?](#how-can-i-turn-off-network-level-access-for-remote-desktop-connections)

## Why am I prompted to authenticate twice (over Remote Desktop Protocol)

This can happen, if Network Level Authentication (NLA) is enabled.

This behavior is by Microsoft's design, and custom credential providers can
not circumvent it. For the rationale, see [RDC and Custom Credential Providers](https://blogs.msdn.microsoft.com/winsdk/2009/07/14/rdc-and-custom-credential-providers) on the Windows SDK Team blog.

A workaround is to disable NLA on the client connection, and allow clients without NLA on the server.

For instructions on how to disable NLA, see: [How can I turn off Network Level Access for Remote Desktop connections?](#how-can-i-turn-off-network-level-access-for-remote-desktop-connections).

## How can I turn off Network Level Access for Remote Desktop connections?

### For Hosts

For RD Session Hosts, see this Microsoft Technet article: [Configure Network Level Authentication for Remote Desktop Services Connections](https://technet.microsoft.com/en-us/library/cc732713%28v=ws.11%29.aspx?f=255&MSPPError=-2147217396).

The entry **Allow connections only from computers running Remote Desktop with Network Level Authentication** must **NOT** be checked.

![Example of the required RDP server settings](images/allow-without-nla.png)

### For Clients

On Remote Desktop Clients, you need to modify the Remote Desktop connection
file (with extension `.rdp`).

Open this file, and add the following setting:

`enablecredsspsupport:i:0`

<aside class="notice">
If you want to apply this setting to all connections, without creating multiple .rdp files, you can apply this setting to the master file in your user's documents folder.

Add this setting to Default.rdp in e.g. <code>C:\Users\&lt;User&gt;\Documents\Default.rdp</code>
</aside>
  
## When accessing a remote server, the Notakey login option fails

If Network Level Authentication (NLA) is enabled, then the Remote Desktop Client
will present a client-side pre-authentication dialog.

This dialog might display the Notakey option, if:

- the Notakey Credential Provider is used to log in to the client computer.

If Notakey is not used to access the client computer, the Notakey credential provider **should not** be present in the NLA pre-authentication dialog box.

NLA prevents client-side credential providers (including Notakey) to comply with NLA - only the built-in password and smartcard credential providers will work.

To enforce Notakey multi-factor authentication in these situations, you have 2 options:

- disable NLA
  - see: [How can I turn off Network Level Access for Remote Desktop connections?](#how-can-i-turn-off-network-level-access-for-remote-desktop-connections)
- use standard username and password authentication to satisfy the NLA requirements, but disable the built-in credential providers on the RD Session Host. This will force NtkCp to be used. **However, this will require your users to perform authentication twice.**

## Why is the Notakey logon option missing, when accessing a remote server via RDP?

If Network Level Authentication (NLA) is enabled, users will be prompted locally
for either their smartcard, or their username/password credentials.

This is by design and can not be changed.

However, if other credential providers are disabled on the remote server, then
after the initial authentication, a second logon UI will be presented, where
the user will be able to use the Notakey credential provider.




