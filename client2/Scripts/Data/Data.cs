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
}

public struct MoveData
{
    public C.Command CommandID;
    public byte UserID;
    public D.Direction DirectionID;
    public float Speed;
}


