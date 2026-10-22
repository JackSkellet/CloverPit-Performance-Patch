# Third-party notices

The installer uses Mono.Cecil 0.11.4 under the MIT license. Its notice is in
[third-party/Mono.Cecil-LICENSE.txt](third-party/Mono.Cecil-LICENSE.txt).

Self-contained releases include the .NET runtime. The .NET license and third-party
notices are retained in `third-party/dotnet-LICENSE.txt` and
`third-party/dotnet-ThirdPartyNotices.txt`. Some components listed in those upstream
notices may not be present in this particular distribution.

`src/Installer/Resources/CloverPitPatch.dll` is this project's own compiled runtime
helper, built from `src/Runtime`. It is not a CloverPit game assembly. The helper
references types in the user's game and Unity installation; those game and Unity
assemblies are not redistributed here.
