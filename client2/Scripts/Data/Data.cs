namespace Data;

using C = Command;
using D = Direction;

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


