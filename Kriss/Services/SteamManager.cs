using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Steamworks;

namespace KrissJourney.Kriss.Services;

public static class SteamManager
{
    private static bool s_Initialized = false;
    private static bool s_EverInitialized = false;
    private static Action s_OnAppQuitCallback;
    private static readonly string LogFilePath;
    private static readonly object LogLock = new();

    public static bool Initialized => s_Initialized;

    static SteamManager() // Static constructor to initialize LogFilePath
    {
        try
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            LogFilePath = Path.Combine(baseDirectory, "SteamManager.log");
            // Clear old log file on startup or create a header
            File.WriteAllText(LogFilePath, $"--- SteamManager Log Initialized: {DateTime.Now} ---\n");
        }
        catch (Exception ex)
        {
            // If we can't write to the log file, output to Debug console at least
            Debug.WriteLine($"[SteamManager] CRITICAL ERROR: Failed to initialize log file path: {ex.Message}");
            LogFilePath = null; // Indicate logging is not available
        }
    }

    public static bool Initialize(Action onAppQuitCallback = null)
    {
        if (CommandLineOptions.SkipSteam)
        {
            Log("Local run mode detected - skipping Steam initialization.");
            s_Initialized = false;
            return false;
        }

        Log("Initialize called.");
        if (s_Initialized)
        {
            Log("Already initialized. Returning true.");
            return true;
        }

        s_OnAppQuitCallback = onAppQuitCallback;

        if (s_EverInitialized)
        {
            Log("Re-initializing SteamAPI.");
            // If we've initialized once before, we only need to call Init again
            s_Initialized = SteamAPI.Init();
            Log($"SteamAPI.Init() re-initialization result: {s_Initialized}");
            return s_Initialized;
        }

        try
        {
            Log("Attempting first-time initialization.");
            // Check if Steam API DLL files exist
            CheckSteamDllFiles();

            // If Steam is not running or the game wasn't started through Steam, SteamAPI_RestartAppIfNecessary starts the 
            // Steam client and also launches this game again if the User owns it
            Log("Checking SteamAPI.RestartAppIfNecessary...");
            bool restartNeeded = SteamAPI.RestartAppIfNecessary(AppId_t.Invalid); // Using AppId_t.Invalid as per your existing code
            Log($"SteamAPI.RestartAppIfNecessary result: {restartNeeded}");
            if (restartNeeded)
            {
                // If RestartAppIfNecessary returns true, we need to quit this instance of the game
                Log("SteamAPI_RestartAppIfNecessary returned true - quitting application.");
                Environment.Exit(0); // Use Environment.Exit for a clean shutdown
                return false; // Should not be reached if Exit(0) works
            }

            // Initialize Steamworks
            Log("Calling SteamAPI.Init()...");
            s_Initialized = SteamAPI.Init();
            if (!s_Initialized)
            {
                Log("SteamAPI.Init() failed. Make sure Steam is running and you are running the game through Steam, or that steam_appid.txt is present and correct.");
                return false;
            }

            s_EverInitialized = true;
            Log("Steam API initialized successfully.");
            Log($"Steam User: {SteamFriends.GetPersonaName()} (ID: {SteamUser.GetSteamID()})"); // Added more info
            Log($"Steam Cloud Quota: {SteamRemoteStorage.GetQuota(out ulong totalBytes, out ulong availableBytes)} Total: {totalBytes} bytes, Available: {availableBytes} bytes"); // Added
            return true;
        }
        catch (DllNotFoundException e)
        {
            Log($"Steam API DLL not found: {e.Message}");
            Log("Please make sure the Steam API library files are in your application directory:");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Log($"- {GetExpectedDllName()} (should be in {AppDomain.CurrentDomain.BaseDirectory})");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                Log($"- libsteam_api.so (should be in {AppDomain.CurrentDomain.BaseDirectory})");
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                Log($"- libsteam_api.dylib (should be in {AppDomain.CurrentDomain.BaseDirectory})");

            s_Initialized = false;
            return false;
        }
        catch (Exception e)
        {
            Log($"Steam initialization error: {e}");
            s_Initialized = false;
            return false;
        }
    }

    private static void CheckSteamDllFiles()
    {
        Log("CheckSteamDllFiles called.");
        string expectedDll = GetExpectedDllName();
        string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, expectedDll);

        Log($"Expected Dll: {expectedDll}, Path: {dllPath}");

        if (!string.IsNullOrEmpty(expectedDll) && !File.Exists(dllPath))
        {
            Log($"Warning: Could not find {expectedDll} in the application directory.");
            Log($"Current directory: {AppDomain.CurrentDomain.BaseDirectory}");
            Log("Available files in BaseDirectory:");

            try
            {
                foreach (string file in Directory.GetFiles(AppDomain.CurrentDomain.BaseDirectory))
                    Log($" - {Path.GetFileName(file)}");
            }
            catch (Exception ex)
            {
                Log($"Error listing directory files: {ex.Message}");
            }
        }
        else if (!string.IsNullOrEmpty(expectedDll))
            Log($"{expectedDll} found at {dllPath}.");
        else
            Log("No specific DLL name expected for this platform (or GetExpectedDllName returned empty).");
    }

    private static string GetExpectedDllName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Environment.Is64BitProcess ? "steam_api64.dll" : "steam_api.dll";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return "libsteam_api.so";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "libsteam_api.dylib";

        Log("GetExpectedDllName: Unknown OSPlatform or not applicable.");
        return string.Empty;
    }

    public static void Shutdown()
    {
        Log("Shutdown called.");
        if (!s_Initialized)
        {
            Log("Not initialized, Shutdown doing nothing.");
            return;
        }

        s_OnAppQuitCallback?.Invoke();
        SteamAPI.Shutdown();
        s_Initialized = false;
        Log("Steam API Shutdown complete.");
    }

    public static void RunCallbacks()
    {
        // This can be very noisy, so only log if needed for specific debugging
        // Log("RunCallbacks called."); 
        if (s_Initialized)
            SteamAPI.RunCallbacks();
    }

    public static bool WriteCloudFile(string fileName, byte[] data)
    {
        Log($"WriteCloudFile called for '{fileName}', Data length: {(data?.Length ?? -1)} bytes.");
        if (!s_Initialized)
        {
            Log("Steam not initialized. Cannot write to cloud.");
            return false;
        }
        if (data == null)
        {
            Log("WriteCloudFile: Data is null. Cannot write.");
            return false;
        }

        bool success = SteamRemoteStorage.FileWrite(fileName, data, data.Length);

        Log($"SteamRemoteStorage.FileWrite {(success ? "succeeded" : "failed")} for {fileName}.");

        return success;
    }

    public static byte[] ReadCloudFile(string fileName)
    {
        Log($"ReadCloudFile called for '{fileName}'.");
        if (!s_Initialized)
        {
            Log("Steam not initialized. Cannot read from cloud.");
            return null;
        }

        if (!SteamRemoteStorage.FileExists(fileName))
        {
            Log($"File '{fileName}' does not exist in Steam Cloud.");
            return null;
        }

        int fileSize = SteamRemoteStorage.GetFileSize(fileName);
        Log($"File '{fileName}' exists in Steam Cloud. Size: {fileSize} bytes.");
        if (fileSize == 0)
        {
            Log($"File '{fileName}' in Steam Cloud is empty (0 bytes). Returning empty array.");
            return []; // Return empty array for an empty file.
        }

        byte[] buffer = new byte[fileSize];
        Log($"Attempting to read {fileSize} bytes for '{fileName}'.");
        int bytesRead = SteamRemoteStorage.FileRead(fileName, buffer, buffer.Length);

        if (bytesRead == fileSize)
        {
            Log($"SteamRemoteStorage.FileRead succeeded for '{fileName}'. Read {bytesRead} bytes.");
            return buffer;
        }
        else
        {
            Log($"SteamRemoteStorage.FileRead for '{fileName}': expected {fileSize} bytes, got {bytesRead}. Read failed or file inconsistent.");
            return null; // Indicate failure to read the expected content.
        }
    }

    public static bool CloudFileExists(string fileName)
    {
        Log($"CloudFileExists called for '{fileName}'.");
        if (!s_Initialized)
        {
            Log("Steam not initialized. Cannot check cloud file existence.");
            return false;
        }
        bool exists = SteamRemoteStorage.FileExists(fileName);
        Log($"SteamRemoteStorage.FileExists for '{fileName}' returned: {exists}.");
        return exists;
    }

    public static ulong GetSteamId()
    {
        if (!s_Initialized)
        {
            Log("Steam not initialized. Cannot get Steam ID.");
            return 0;
        }

        try
        {
            CSteamID steamId = SteamUser.GetSteamID();
            ulong id = steamId.m_SteamID;
            Log($"Retrieved Steam ID: {id}");
            return id;
        }
        catch (Exception ex)
        {
            Log($"Error getting Steam ID: {ex.Message}");
            return 0;
        }
    }

    private static void Log(string message)
    {
        string timedMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}";
        Debug.WriteLine(timedMessage); // Keep console logging

        if (LogFilePath != null)
        {
            lock (LogLock) // Ensure thread safety when writing to the file
            {
                try
                {
                    File.AppendAllText(LogFilePath, timedMessage + Environment.NewLine);
                }
                catch (Exception ex)
                {
                    // Fallback if file logging fails after initialization
                    Debug.WriteLine($"[SteamManager] ERROR: Failed to write to log file '{LogFilePath}': {ex.Message}");
                }
            }
        }
    }
}