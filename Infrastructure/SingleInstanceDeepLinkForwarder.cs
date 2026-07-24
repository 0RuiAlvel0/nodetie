using System;
using System.Runtime.InteropServices;
using System.Text;

namespace NodeTie.Infrastructure;

public static class SingleInstanceDeepLinkForwarder
{
    private const int WmCopyData = 0x004A;
    private const uint SmtoAbortIfHung = 0x0002;

    public static bool TryGetDeepLinkArgument(string[] args, out string deepLink)
    {
        deepLink = string.Empty;
        if (args.Length == 0)
        {
            return false;
        }

        foreach (string arg in args)
        {
            string candidate = arg.Trim().Trim('"');
            if (!candidate.StartsWith("winlink://", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            deepLink = candidate;
            return true;
        }

        return false;
    }

    public static bool TryForwardToRunningInstance(string deepLink)
    {
        IntPtr targetWindow = FindWindow(lpClassName: null, lpWindowName: NodeTieMessageWindow.WindowCaption);
        if (targetWindow == IntPtr.Zero)
        {
            return false;
        }

        string payload = deepLink.Trim();
        if (payload.Length == 0)
        {
            return false;
        }

        byte[] bytes = Encoding.Unicode.GetBytes(payload + "\0");
        IntPtr dataPtr = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, dataPtr, bytes.Length);
            var copyData = new CopyDataStruct
            {
                DwData = IntPtr.Zero,
                CbData = bytes.Length,
                LpData = dataPtr
            };

            IntPtr copyDataPtr = Marshal.AllocHGlobal(Marshal.SizeOf<CopyDataStruct>());
            try
            {
                Marshal.StructureToPtr(copyData, copyDataPtr, fDeleteOld: false);
                IntPtr sendResult;
                IntPtr response = SendMessageTimeout(
                    targetWindow,
                    WmCopyData,
                    IntPtr.Zero,
                    copyDataPtr,
                    SmtoAbortIfHung,
                    1500,
                    out sendResult);

                return response != IntPtr.Zero;
            }
            finally
            {
                Marshal.FreeHGlobal(copyDataPtr);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(dataPtr);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct CopyDataStruct
    {
        public IntPtr DwData { get; init; }
        public int CbData { get; init; }
        public IntPtr LpData { get; init; }
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        int msg,
        IntPtr wParam,
        IntPtr lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);
}