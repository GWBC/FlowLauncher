using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Flow.Launcher.Plugin.Program
{
    public static class EverythingApi
    {
        private const string DLL = "Everything.dll";

        public static void Load(string directory)
        {
            var path = Path.Combine(directory, DLL);
            int code = LoadLibrary(path);
            if (code == 0)
            {
                int err = Marshal.GetLastPInvokeError();
                Marshal.ThrowExceptionForHR(err);
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int LoadLibrary(string name);

        [DllImport(DLL)]
        internal static extern int Everything_GetMajorVersion();

        [DllImport(DLL)]
        internal static extern int Everything_GetLastError();

        [DllImport(DLL)]
        internal static extern uint Everything_GetMinorVersion();

        [DllImport(DLL, CharSet = CharSet.Unicode)]
        internal static extern void Everything_SetSearchW(string search);

        [DllImport(DLL)]
        internal static extern void Everything_QueryW(bool wait);

        [DllImport(DLL)]
        internal static extern int Everything_GetNumResults();

        [DllImport(DLL, CharSet = CharSet.Unicode)]
        internal static extern void Everything_GetResultFullPathNameW(int index, System.Text.StringBuilder buf, int bufsize);
    }
}
