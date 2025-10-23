using System.Diagnostics;
using System.IO.Compression;

if (Process.GetProcessesByName("Docker Desktop").Length == 0)
{
    ExecuteCommand("cmd.exe", "/c start \"\" \"C:\\Program Files\\Docker\\Docker\\Docker Desktop.exe\"");
    Console.WriteLine("You forgot to start Docker. Now it is opening. Retry when ready.");
    return;
}

// execute the multiple steps to publish on the 3 platforms in parallel
// build windows and mac directly
// build linux in docker
string basePath = "H:\\KrissJourney";

// this takes longer, so we wait at the end
Task linuxBuild = BuildLinuxInDocker();

// clean the output folder
string outputDir = Path.Combine(basePath, "Scripts", ".output");
if (Directory.Exists(outputDir))
    Directory.Delete(outputDir, true);
Directory.CreateDirectory(outputDir);

// build windows and mac directly in parallel
Task windowsBuild = BuildPlatform("win-x64");
Task macBuild = BuildPlatform("osx-x64");
await Task.WhenAll(windowsBuild, macBuild);

// compress the builds for windows and mac and move them to the output folder
Task compressWindows = CompressBuild("win-x64");
Task compressMac = CompressBuild("osx-x64", "krissLauncher.sh");

await Task.WhenAll(compressWindows, compressMac, linuxBuild);

Console.WriteLine("All builds completed successfully.");

void ExecuteCommand(string fileName, string arguments)
{
    ProcessStartInfo psi = new()
    {
        FileName = fileName,
        Arguments = fileName == "pwsh.exe" ? $"-Command \"& {{ {arguments} }}\"" : arguments,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        CreateNoWindow = true
    };

    using Process process = Process.Start(psi) ?? throw new Exception($"Failed to start process.");
    process.OutputDataReceived += (s, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
    process.ErrorDataReceived += (s, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };
    process.BeginOutputReadLine();
    process.BeginErrorReadLine();
    process.WaitForExit();

    if (process.ExitCode != 0)
        throw new Exception($"Command {fileName} {arguments} failed with exit code {process.ExitCode}");
}

Task BuildLinuxInDocker()
{
    return Task.Run(() =>
    {
        try
        {
            ExecuteCommand("pwsh.exe", Path.Combine(basePath, "Scripts", "Linux-SteamOS", "build-docker.ps1"));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error executing Docker build script: {ex.Message}");
            throw;
        }
    });
}

Task BuildPlatform(string platform)
{
    return Task.Run(() =>
    {
        try
        {
            string buildDir = Path.Combine(basePath, "Kriss", "bin", "Release", "net8.0", platform, "build");
            string publishDir = Path.Combine(basePath, "Kriss", "bin", "Release", "net8.0", platform, "publish");
            if (Directory.Exists(buildDir))
                Directory.Delete(buildDir, true);
            if (Directory.Exists(publishDir))
                Directory.Delete(publishDir, true);

            string projectDir = Path.Combine(basePath, "Kriss", "KrissJourney.Kriss.csproj");
            ExecuteCommand(
                "dotnet",
                $"publish {projectDir} -c Release -r {platform} -p:PublishSingleFile=true --self-contained true /p:DebugType=None /p:DebugSymbols=false /p:AssemblyName=Kriss");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error publishing for {platform}: {ex.Message}");
            throw;
        }
    });
}

Task CompressBuild(string platform, string? launcher = null)
{
    return Task.Run(() =>
    {
        try
        {
            string sourceDir = Path.Combine(basePath, "Kriss", "bin", "Release", "net8.0", platform, "publish");

            // If there is a launcher, it means it's a mac build
            if (launcher != null)
                CopyScriptWithUnixLineEndings(
                    Path.Combine(basePath, "Scripts", platform, launcher),
                    Path.Combine(sourceDir, "krissLauncher.sh"));

            string outputZip = Path.Combine(basePath, "Scripts", ".output", $"Kriss-{platform}.zip");
            if (File.Exists(outputZip))
                File.Delete(outputZip);

            ZipFile.CreateFromDirectory(sourceDir, outputZip, CompressionLevel.Optimal, false);
            Console.WriteLine($"{platform} build compressed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error compressing {platform} build: {ex.Message}");
            throw;
        }
    });
}

static void CopyScriptWithUnixLineEndings(string sourcePath, string destPath)
{
    string content = File.ReadAllText(sourcePath);
    // Normalize line endings to LF only
    content = content.Replace("\r\n", "\n").Replace("\r", "\n");
    File.WriteAllText(destPath, content, new System.Text.UTF8Encoding(false));
}
