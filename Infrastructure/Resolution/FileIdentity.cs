namespace NodeTie.Infrastructure.Resolution;

public sealed record FileIdentity(uint VolumeSerialNumber, ulong FileIndex)
{
    public string ToStableId()
    {
        return $"{VolumeSerialNumber:X8}:{FileIndex:X16}";
    }
}
