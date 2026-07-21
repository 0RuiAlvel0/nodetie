using System;
using System.Runtime.InteropServices;

namespace NodeTie.Infrastructure.Context.Office;

public sealed class ComActiveObjectService : IComActiveObjectService
{
    public bool TryGetActiveObject(string progId, out object? activeObject)
    {
        activeObject = null;
        try
        {
            Guid clsid;
            int clsidResult = CLSIDFromProgIDEx(progId, out clsid);
            if (clsidResult < 0)
            {
                clsidResult = CLSIDFromProgID(progId, out clsid);
            }

            if (clsidResult < 0)
            {
                return false;
            }

            int getActiveObjectResult = GetActiveObject(ref clsid, IntPtr.Zero, out activeObject);
            if (getActiveObjectResult < 0)
            {
                return false;
            }

            return activeObject is not null;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    private static extern int CLSIDFromProgIDEx(string progId, out Guid clsid);

    [DllImport("ole32.dll", CharSet = CharSet.Unicode)]
    private static extern int CLSIDFromProgID(string progId, out Guid clsid);

    [DllImport("oleaut32.dll")]
    private static extern int GetActiveObject(ref Guid clsid, IntPtr reserved, [MarshalAs(UnmanagedType.IUnknown)] out object? activeObject);
}
