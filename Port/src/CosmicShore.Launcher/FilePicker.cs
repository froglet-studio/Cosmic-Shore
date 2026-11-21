using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace CosmicShore.Launcher
{
    /// <summary>
    /// The system's Open File dialog (Windows' common dialog, comdlg32). Elsewhere there is none:
    /// <see cref="Open"/> answers null and the path is typed in launcher.json instead. Blocks the
    /// calling thread, so call it off the UI thread; it runs on its own STA thread as the dialog wants.
    /// </summary>
    static class FilePicker
    {
        /// <param name="filter">"Label|pattern" pairs, e.g. "blender.exe|blender.exe|All files|*.*".</param>
        public static string? Open(string title, string filter)
        {
            if (!OperatingSystem.IsWindows()) return null;
            string? result = null;
            var t = new Thread(() => result = OpenWin32(title, filter));
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            t.Join();
            return result;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct OpenFileName
        {
            public int lStructSize;
            public IntPtr hwndOwner, hInstance;
            public string lpstrFilter;
            public IntPtr lpstrCustomFilter;
            public int nMaxCustFilter, nFilterIndex;
            public IntPtr lpstrFile;
            public int nMaxFile;
            public IntPtr lpstrFileTitle;
            public int nMaxFileTitle;
            public string? lpstrInitialDir, lpstrTitle;
            public int Flags;
            public short nFileOffset, nFileExtension;
            public string? lpstrDefExt;
            public IntPtr lCustData, lpfnHook;
            public string? lpTemplateName;
            public IntPtr pvReserved;
            public int dwReserved, FlagsEx;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool GetOpenFileNameW(ref OpenFileName ofn);

        const int OFN_FILEMUSTEXIST = 0x1000, OFN_PATHMUSTEXIST = 0x800, OFN_NOCHANGEDIR = 0x8, OFN_EXPLORER = 0x80000;

        static string? OpenWin32(string title, string filter)
        {
            const int max = 4096;
            var buffer = Marshal.AllocHGlobal(max * 2);
            try
            {
                Marshal.WriteInt16(buffer, 0);
                var ofn = new OpenFileName
                {
                    lStructSize = Marshal.SizeOf<OpenFileName>(),
                    // The dialog wants NUL-separated pairs ending in two NULs.
                    lpstrFilter = filter.Replace('|', '\0') + "\0All files\0*.*\0\0",
                    nFilterIndex = 1,
                    lpstrFile = buffer,
                    nMaxFile = max,
                    lpstrTitle = title,
                    Flags = OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR | OFN_EXPLORER,
                };
                return GetOpenFileNameW(ref ofn) ? Marshal.PtrToStringUni(buffer) : null;
            }
            catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { return null; }
            finally { Marshal.FreeHGlobal(buffer); }
        }
    }
}
