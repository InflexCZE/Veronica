# WebQwen tests live in Veronica.Tests

`Veronica.Tests` references `WebQwen.csproj` for the protocol tests (`ProtocolTests.cs`), and `WebQwen.csproj` references `WebQwen.Native.vcxproj`. So the plugin's tests cannot build on a machine without the native toolchain (LLVM, C++ build tools), although the plugin itself needs none of it: the dotnet CLI fails on the vcxproj with MSB4278 even with `-p:BuildProjectReferences=false`.

Missing: a separate test project for WebQwen, so `Veronica.Tests` builds with the managed SDK alone. The protocol tests run WebQwen's `Server` against the plugin's `WebQwenConnection`, so they need both sides; where they belong (the WebQwen test project referencing the plugin's agent, or kept here against a managed-only part of WebQwen) is open.
