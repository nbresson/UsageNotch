using System.Runtime.InteropServices;

namespace UsageNotch.Hook;

/// <summary>PID du processus parent via NtQueryInformationProcess, sans dépendance. 0 en cas d'échec.</summary>
internal static partial class ParentProcess
{
    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessBasicInformation
    {
        public nint ExitStatus;
        public nint PebBaseAddress;
        public nint AffinityMask;
        public nint BasePriority;
        public nint UniqueProcessId;
        public nint InheritedFromUniqueProcessId;
    }

    [LibraryImport("ntdll.dll")]
    private static partial int NtQueryInformationProcess(nint processHandle, int informationClass, ref ProcessBasicInformation information, int length, out int returnLength);

    public static int Id()
    {
        if (!OperatingSystem.IsWindows()) return 0;
        var info = default(ProcessBasicInformation);
        var status = NtQueryInformationProcess(-1, 0, ref info, Marshal.SizeOf<ProcessBasicInformation>(), out _);
        return status == 0 ? (int)info.InheritedFromUniqueProcessId : 0;
    }
}
