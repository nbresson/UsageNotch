using System.Runtime.InteropServices;

namespace UsageNotch.Hook;

/// <summary>PID du processus parent et chaîne d'ancêtres, via ntdll et Toolhelp32, sans dépendance. 0 ou liste vide en cas d'échec.</summary>
internal static unsafe partial class ParentProcess
{
    private const uint Th32csSnapProcess = 0x00000002;
    private static readonly nint InvalidHandleValue = -1;

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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ProcessEntry32W
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public nuint th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        public fixed char szExeFile[260];
    }

    [LibraryImport("ntdll.dll")]
    private static partial int NtQueryInformationProcess(nint processHandle, int informationClass, ref ProcessBasicInformation information, int length, out int returnLength);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint CreateToolhelp32Snapshot(uint flags, uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Process32FirstW(nint snapshot, ProcessEntry32W* entry);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Process32NextW(nint snapshot, ProcessEntry32W* entry);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    public static int Id()
    {
        if (!OperatingSystem.IsWindows()) return 0;
        var info = default(ProcessBasicInformation);
        var status = NtQueryInformationProcess(-1, 0, ref info, Marshal.SizeOf<ProcessBasicInformation>(), out _);
        return status == 0 ? (int)info.InheritedFromUniqueProcessId : 0;
    }

    /// <summary>
    /// Parent, grand-parent, etc. (au plus <paramref name="maxDepth"/>), lus dans un seul instantané Toolhelp32.
    /// S'arrête au PID 0, à un cycle ou à une entrée absente ; toute erreur rend ce qui a déjà été rassemblé.
    /// </summary>
    public static IReadOnlyList<(int Pid, string ExeName)> Ancestors(int maxDepth = 8)
    {
        var chain = new List<(int Pid, string ExeName)>();
        if (!OperatingSystem.IsWindows()) return chain;
        try
        {
            var start = Id();
            if (start == 0) return chain;

            var table = new Dictionary<int, (int ParentPid, string ExeName)>();
            var snapshot = CreateToolhelp32Snapshot(Th32csSnapProcess, 0);
            if (snapshot == InvalidHandleValue || snapshot == 0) return chain;
            try
            {
                var entry = default(ProcessEntry32W);
                entry.dwSize = (uint)sizeof(ProcessEntry32W);
                var more = Process32FirstW(snapshot, &entry);
                while (more)
                {
                    var name = new string(entry.szExeFile);
                    table[(int)entry.th32ProcessID] = ((int)entry.th32ParentProcessID, name);
                    more = Process32NextW(snapshot, &entry);
                }
            }
            finally
            {
                CloseHandle(snapshot);
            }

            var seen = new HashSet<int>();
            var pid = start;
            while (pid != 0 && chain.Count < maxDepth && seen.Add(pid) && table.TryGetValue(pid, out var row))
            {
                chain.Add((pid, row.ExeName));
                pid = row.ParentPid;
            }
        }
        catch
        {
            // On rend ce qui a été rassemblé.
        }
        return chain;
    }
}
