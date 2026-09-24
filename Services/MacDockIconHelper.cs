using System;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Platform;

namespace AzureDeadLetterMonitor.Services;

/// <summary>
/// Helper to set the macOS Dock icon dynamically at runtime (especially useful during development
/// when running directly via 'dotnet run' without an .app bundle).
/// </summary>
public static class MacDockIconHelper
{
    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib")]
    private static extern IntPtr sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend(IntPtr receiver, IntPtr selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr(IntPtr receiver, IntPtr selector, IntPtr arg);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_IntPtr_ulong(IntPtr receiver, IntPtr selector, IntPtr arg1, ulong arg2);

    [DllImport("/usr/lib/libSystem.B.dylib")]
    private static extern IntPtr dlopen(string path, int mode);

    private static bool _iconSet;

    public static void SetDockIcon()
    {
        if (!OperatingSystem.IsMacOS() || _iconSet)
        {
            return;
        }

        try
        {
            byte[]? iconBytes = LoadIconBytes();
            if (iconBytes == null || iconBytes.Length == 0)
            {
                return;
            }

            // Ensure AppKit framework is loaded
            dlopen("/System/Library/Frameworks/AppKit.framework/AppKit", 1);

            var nsAppClass = objc_getClass("NSApplication");
            if (nsAppClass == IntPtr.Zero) return;

            var sharedAppSel = sel_registerName("sharedApplication");
            var nsApp = objc_msgSend(nsAppClass, sharedAppSel);
            if (nsApp == IntPtr.Zero) return;

            var nsDataClass = objc_getClass("NSData");
            var dataWithBytesSel = sel_registerName("dataWithBytes:length:");

            GCHandle handle = GCHandle.Alloc(iconBytes, GCHandleType.Pinned);
            try
            {
                IntPtr ptr = handle.AddrOfPinnedObject();
                IntPtr nsData = objc_msgSend_IntPtr_ulong(nsDataClass, dataWithBytesSel, ptr, (ulong)iconBytes.Length);
                if (nsData == IntPtr.Zero) return;

                var nsImageClass = objc_getClass("NSImage");
                var allocSel = sel_registerName("alloc");
                var initWithDataSel = sel_registerName("initWithData:");

                IntPtr imgAlloc = objc_msgSend(nsImageClass, allocSel);
                IntPtr image = objc_msgSend_IntPtr(imgAlloc, initWithDataSel, nsData);
                if (image == IntPtr.Zero) return;

                var setIconSel = sel_registerName("setApplicationIconImage:");
                objc_msgSend_IntPtr(nsApp, setIconSel, image);

                var dockTileSel = sel_registerName("dockTile");
                IntPtr dockTile = objc_msgSend(nsApp, dockTileSel);
                if (dockTile != IntPtr.Zero)
                {
                    var displaySel = sel_registerName("display");
                    objc_msgSend(dockTile, displaySel);
                }

                _iconSet = true;
            }
            finally
            {
                handle.Free();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MacDockIconHelper] Failed to set macOS dock icon: {ex.Message}");
        }
    }

    private static byte[]? LoadIconBytes()
    {
        // 1. Primary: Load from Avalonia embedded resource assets (compiled into assembly)
        try
        {
            var uri = new Uri("avares://AzureDeadLetterMonitor/Assets/app-icon.png");
            if (AssetLoader.Exists(uri))
            {
                using var stream = AssetLoader.Open(uri);
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MacDockIconHelper] Failed loading icon from AssetLoader: {ex.Message}");
        }

        // 2. Secondary fallback: Check known filesystem paths
        string[] candidatePaths =
        [
            Path.Combine(AppContext.BaseDirectory, "Assets", "app-icon.png"),
            Path.Combine(Directory.GetCurrentDirectory(), "Assets", "app-icon.png")
        ];

        foreach (var path in candidatePaths)
        {
            // File.Exists handles invalid paths, access restrictions, etc. and safely returns false without throwing
            if (!File.Exists(path))
            {
                continue;
            }

            // File.ReadAllBytes may fail due to concurrent lock, permissions, or transient I/O issues
            try
            {
                return File.ReadAllBytes(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Debug.WriteLine($"[MacDockIconHelper] Could not read icon at '{path}': {ex.Message}");
            }
        }

        return null;
    }
}
