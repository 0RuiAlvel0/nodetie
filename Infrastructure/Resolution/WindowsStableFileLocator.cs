using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace NodeTie.Infrastructure.Resolution;

public sealed class WindowsStableFileLocator : IStableFileLocator
{
    private const uint FileReadAttributes = 0x80;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint FileShareDelete = 0x00000004;
    private const uint OpenExisting = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const int FileIdType = 0;

    public WindowsStableFileLocator(IFileIdentityService fileIdentityService)
    {
        _ = fileIdentityService;
    }

    public bool TryLocate(string stableId, out string locatedPath)
    {
        locatedPath = string.Empty;

        if (!TryParseStableId(stableId, out uint expectedVolumeSerial, out ulong expectedFileIndex))
        {
            return false;
        }

        foreach (DriveInfo drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady)
            {
                continue;
            }

            string rootPath = drive.RootDirectory.FullName;
            if (!TryGetVolumeSerial(rootPath, out uint volumeSerial))
            {
                continue;
            }

            if (volumeSerial != expectedVolumeSerial)
            {
                continue;
            }

            if (TryResolveByFileId(drive.Name, expectedFileIndex, out locatedPath))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryParseStableId(string stableId, out uint volumeSerial, out ulong fileIndex)
    {
        volumeSerial = 0;
        fileIndex = 0;

        if (string.IsNullOrWhiteSpace(stableId))
        {
            return false;
        }

        string[] parts = stableId.Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
        {
            return false;
        }

        if (!uint.TryParse(parts[0], System.Globalization.NumberStyles.HexNumber, null, out volumeSerial))
        {
            return false;
        }

        return ulong.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out fileIndex);
    }

    private static bool TryGetVolumeSerial(string rootPath, out uint volumeSerial)
    {
        volumeSerial = 0;
        return GetVolumeInformation(
            rootPath,
            null,
            0,
            out volumeSerial,
            out _,
            out _,
            null,
            0);
    }

    private static bool TryResolveByFileId(string driveName, ulong fileIndex, out string locatedPath)
    {
        locatedPath = string.Empty;

        string volumePath = BuildVolumePath(driveName);
        using SafeFileHandle volumeHandle = CreateFile(
            volumePath,
            0,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics,
            IntPtr.Zero);

        if (volumeHandle.IsInvalid)
        {
            return false;
        }

        FILE_ID_DESCRIPTOR descriptor = new()
        {
            dwSize = (uint)Marshal.SizeOf<FILE_ID_DESCRIPTOR>(),
            Type = FileIdType,
            Id = new FILE_ID_DESCRIPTOR_UNION
            {
                FileId = unchecked((long)fileIndex)
            }
        };

        using SafeFileHandle fileHandle = OpenFileById(
            volumeHandle,
            ref descriptor,
            FileReadAttributes,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            FileFlagBackupSemantics);

        if (fileHandle.IsInvalid)
        {
            return false;
        }

        StringBuilder pathBuilder = new(1024);
        uint resultLength = GetFinalPathNameByHandle(fileHandle, pathBuilder, (uint)pathBuilder.Capacity, 0);
        if (resultLength == 0 || resultLength >= pathBuilder.Capacity)
        {
            return false;
        }

        locatedPath = NormalizePath(pathBuilder.ToString());
        return !string.IsNullOrWhiteSpace(locatedPath);
    }

    private static string BuildVolumePath(string driveName)
    {
        string trimmed = driveName.TrimEnd('\\');
        return $@"\\.\{trimmed}";
    }

    private static string NormalizePath(string rawPath)
    {
        const string longPathPrefix = @"\\?\";
        const string uncPrefix = @"\\?\UNC\";

        if (rawPath.StartsWith(uncPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return "\\\\" + rawPath.Substring(uncPrefix.Length);
        }

        if (rawPath.StartsWith(longPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return rawPath.Substring(longPathPrefix.Length);
        }

        return rawPath;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetVolumeInformation(
        string lpRootPathName,
        StringBuilder? lpVolumeNameBuffer,
        uint nVolumeNameSize,
        out uint lpVolumeSerialNumber,
        out uint lpMaximumComponentLength,
        out uint lpFileSystemFlags,
        StringBuilder? lpFileSystemNameBuffer,
        uint nFileSystemNameSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern SafeFileHandle OpenFileById(
        SafeFileHandle hVolumeHint,
        ref FILE_ID_DESCRIPTOR lpFileId,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwFlagsAndAttributes);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetFinalPathNameByHandle(
        SafeFileHandle hFile,
        StringBuilder lpszFilePath,
        uint cchFilePath,
        uint dwFlags);

    [StructLayout(LayoutKind.Sequential)]
    private struct FILE_ID_DESCRIPTOR
    {
        public uint dwSize;
        public int Type;
        public FILE_ID_DESCRIPTOR_UNION Id;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct FILE_ID_DESCRIPTOR_UNION
    {
        [FieldOffset(0)]
        public long FileId;

        [FieldOffset(0)]
        public Guid ObjectId;

        [FieldOffset(0)]
        public Guid ExtendedFileId;
    }
}
