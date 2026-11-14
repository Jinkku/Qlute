using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Godot;
using Steamworks;

public partial class SteamEngine : Node
{
    private static bool _isInitialized = false;

    public override void _Ready()
    {
        if (_isInitialized) return;

        // 1. Hook the DllImport resolver specifically for Linux
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            RegisterLinuxSteamResolver();
        }

        // 2. Safely initialize Steam
        try
        {
            _isInitialized = SteamAPI.Init();
            if (!_isInitialized)
            {
                GD.PrintErr("[Steam] SteamAPI.Init() returned false. Is Steam running?");
                SetProcess(false); // Stop _Process from running if init failed
                return;
            }

            GD.Print("[Steam] Success! Steam is running.");
            UpdateRichPresence("Exploring Main Menu", "Menu");

        }
        catch (Exception ex)
        {
            _isInitialized = false;
            SetProcess(false);
            GD.PrintErr($"[Steam] Exception during init: {ex.Message}");
        }
    }


    public void UpdateRichPresence(string statusText, string statusGroup = "")
    {
        if (!_isInitialized) return;

        // "steam_display" is the special core key Steam uses to determine what to show.
        // It can point to a localization token you set up in Steamworks, or a raw string for testing.
        bool successDisplay = SteamFriends.SetRichPresence("steam_display", statusText);

        if (!string.IsNullOrEmpty(statusGroup))
        {
            // You can also pass custom keys that your localization tokens reference
            SteamFriends.SetRichPresence("status_group", statusGroup);
        }

        if (successDisplay)
        {
            GD.Print($"Rich Presence updated: {statusText}");
        }
        else
        {
            GD.PrintErr("Failed to set Steam Rich Presence.");
        }
    }

    private void RegisterLinuxSteamResolver()
    {
        // Intercept native library requests coming from Steamworks.NET
        NativeLibrary.SetDllImportResolver(typeof(SteamAPI).Assembly, (libraryName, assembly, searchPath) =>
        {
            if (libraryName == "steam_api" || libraryName == "steam_api64" || libraryName == "libsteam_api")
            {
                // Converts Godot's res://libsteam_api.so to an absolute path like /home/user/Project/libsteam_api.so
                string absolutePath = ProjectSettings.GlobalizePath("res://libsteam_api.so");

                if (File.Exists(absolutePath))
                {
                    return NativeLibrary.Load(absolutePath);
                }

                // Fallback: Check executable base directory if res:// lookup fails
                string execPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "libsteam_api.so");
                if (File.Exists(execPath))
                {
                    return NativeLibrary.Load(execPath);
                }
            }

            // Return IntPtr.Zero to let .NET handle default loading for other libraries
            return IntPtr.Zero;
        });
    }
    public override void _Process(double delta)
    {
        if (_isInitialized)
        {
            SteamAPI.RunCallbacks();
        }
    }

    public override void _ExitTree()
    {
        if (_isInitialized)
        {
            SteamAPI.Shutdown();
            _isInitialized = false;
        }
    }
}