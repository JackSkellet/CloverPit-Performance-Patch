# Runtime helper provenance

The release embeds the already-tested `CloverPitPatch.dll` from these three source
files. It was compiled with Roslyn 4.8.0 against the supported game's managed
references, with optimization enabled. Its SHA-256 is:

`4e10da52074c8f5e0e7293e297365816b1cc1dbd1baab3659fcafab45302e283`

The public algorithm tests compile these source files against authored fixtures.
They do not load or redistribute game assemblies. Actual Unity validation was
performed separately, as described in `../../VALIDATION.md`.

To rebuild the helper, use the C# compiler from Microsoft.Net.Compilers.Toolset
4.8.0 with `-target:library -nostdlib+ -optimize+`, output name
`CloverPitPatch.dll`, and these references from your own game's Managed directory:

- mscorlib.dll, netstandard.dll, System.dll, System.Core.dll, System.Numerics.dll
- UnityEngine.CoreModule.dll, UnityEngine.UI.dll, UnityEngine.UIModule.dll
- Unity.TextMeshPro.dll, Assembly-CSharp.dll

Compile `PatchAlgorithms.cs`, `TransitionSpeed.cs`, and `CrtOpacity.cs` together.
Compiler metadata can change the binary hash. Revalidate a rebuilt helper in the
game and update the embedded resource and pinned runtime hash together; the
installer deliberately refuses an unrecognized helper. Do not commit the game
references or patched game assembly.
