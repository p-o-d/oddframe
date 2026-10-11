namespace OddFrame.Core;

public sealed record DeviceOptions
{
    public Region Region { get; init; } = Region.Ntsc;
    public bool EnableDebug { get; init; }
}