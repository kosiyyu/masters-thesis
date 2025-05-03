namespace Data;

using System.Runtime.InteropServices;
using C = Command;
using D = Direction;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PositionData
{
    public C.Command CommandID;
    public byte UserID;
    public float X;
    public float Y;
    public float Z;
    public float RotY;

    public override string ToString()
    {
        return $"CommandID: {CommandID}, UserID: {UserID}, X: {X}, Y: {Y}, Z: {Z}, RotY: {RotY}";
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PositionDataRTT
{
    public C.Command CommandID;
    public byte UserID;
    public float X;
    public float Y;
    public float Z;
    public float RotY;
    public uint TimestampRTT;

    public override string ToString()
    {
        return $"CommandID: {CommandID}, UserID: {UserID}, X: {X}, Y: {Y}, Z: {Z}, RotY: {RotY} | TimestampRTT: {TimestampRTT}";
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MoveData
{
    public C.Command CommandID;
    public byte UserID;
    public D.Direction DirectionID;
    public float Speed;

    public override string ToString()
    {
        return $"CommandID: {CommandID}, UserID: {UserID}, DirectionID: {DirectionID}, Speed: {Speed}";
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct MoveDataRTT
{
    public C.Command CommandID;
    public byte UserID;
    public D.Direction DirectionID;
    public float Speed;
    public uint TimestampRTT;

    public override string ToString()
    {
        return $"CommandID: {CommandID}, UserID: {UserID}, DirectionID: {DirectionID}, Speed: {Speed} | TimestampRTT: {TimestampRTT}";
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct DefaultRTT
{
    public C.Command CommandID;
    public uint TimestampRTT;

    public override string ToString()
    {
        return $"CommandID: {CommandID} | TimestampRTT: {TimestampRTT}";
    }
}


