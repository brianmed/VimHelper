# VimHelper

### Beta

This is a command line app that can create a ctag file from C# code.

## Running

``` bash
$ dotnet run -- dll_directory deps.json file1.cs file2.cs
$ dotnet run -- $(pwd)/bin/Debug/netcoreapp2.0/osx.10.12-x64 bin/Debug/netcoreapp2.0/osx.10.12-x64/VimHelper.deps.json $(pwd)/Program.cs $(pwd)/Project.cs | sort >| tags
$ cat tags | head -3
AddDocument     Project.cs      :normal 17451go ;"
AddDocument     Project.cs      :normal 17451go ;"
AddDocument     Project.cs      :normal 17580go ;"
```
