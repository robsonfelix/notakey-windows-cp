midl /env win64 "C:\Program Files (x86)\Windows Kits\8.1\Include\um\credentialprovider.idl"
tlbimp /namespace:CredentialProviders /machine:x64 /out:credentialprovider_x64.dll credentialprovider.tlb
del *.tlb
del *.c
del *.h

midl /env win32 "C:\Program Files (x86)\Windows Kits\8.1\Include\um\credentialprovider.idl"
tlbimp /namespace:CredentialProviders /machine:x86 /out:credentialprovider_x86.dll credentialprovider.tlb
del *.tlb
del *.c
del *.h