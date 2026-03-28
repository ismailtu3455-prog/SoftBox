param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

dotnet publish "./SoftBoxLauncher/SoftBoxLauncher.csproj" `
    -c $Configuration `
    -r $Runtime `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false
