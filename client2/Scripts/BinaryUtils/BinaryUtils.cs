namespace BinaryUtils;

using System;
using C = Command;
using D = Direction;
using Data;
using System.IO;

public static class BinaryUtils
{
    #region _get used types

    private static byte _getByte(in byte[] byteArray, int startIndex)
    {
        if (startIndex + 1 > byteArray.Length)
        {
            throw new ArgumentException("Byte array should have at least 1 byte.");
        }
        if (startIndex < 0 || startIndex > byteArray.Length - 1)
        {
            throw new ArgumentException("Start index value is incorrect.");
        }

        return byteArray[startIndex];

    }

    private static float _getFloat(in byte[] byteArray, int startIndex)
    {
        if (startIndex + 4 > byteArray.Length)
        {
            throw new ArgumentException("Byte array should have at least 4 bytes.");
        }
        if (startIndex < 0 || startIndex > byteArray.Length - 1)
        {
            throw new ArgumentException("Start index value is incorrect.");
        }

        return BitConverter.ToSingle(byteArray, startIndex);
    }

    private static uint _getUInt(in byte[] byteArray, int startIndex)
    {
        if (startIndex + 4 > byteArray.Length)
        {
            throw new ArgumentException("Byte array should have at least 4 bytes.");
        }
        if (startIndex < 0 || startIndex > byteArray.Length - 1)
        {
            throw new ArgumentException("Start index value is incorrect.");
        }

        return BitConverter.ToUInt32(byteArray, startIndex);
    }

    #endregion

    #region _get complex types

    public static C.Command GetCommand(in byte[] byteArray)
    {
        if (byteArray.Length < 1)
        {
            throw new ArgumentException("Byte array should have at least one byte.");
        }

        var value = byteArray[0];
        return value switch
        {
            (byte)C.Command.MOVE => C.Command.MOVE,
            (byte)C.Command.POSITION => C.Command.POSITION,
            (byte)C.Command.MOVE_RTT => C.Command.MOVE_RTT,
            (byte)C.Command.POSITION_RTT => C.Command.POSITION_RTT,
            (byte)C.Command.DEFAULT_RTT => C.Command.DEFAULT_RTT,
            _ => throw new ArgumentException($"Unknown command value: {value}")
        };
    }

    private static D.Direction _getDirection(in byte[] byteArray, int startIndex)
    {
        if (byteArray.Length < 1)
        {
            throw new ArgumentException("Byte array should have at least one byte.");
        }
        if (startIndex < 0 || startIndex > byteArray.Length - 1)
        {
            throw new ArgumentException("Start index value is incorrect.");
        }

        return (D.Direction)byteArray[startIndex];
    }

    #endregion

    #region PositionData

    public static PositionData DeserializePositionData(in byte[] byteArray)
    {
        if (byteArray.Length < 18) // (1 + 1 + 4 + 4 + 4 + 4) bytes
        {
            throw new ArgumentException("Byte array is too short to deserialize PositionData.");
        }

        return new PositionData()
        {
            CommandID = GetCommand(in byteArray), // index 0
            UserID = _getByte(in byteArray, 1), // index 1
            X = _getFloat(in byteArray, 2), // index 2 - 5
            Y = _getFloat(in byteArray, 6), // index 6 - 9 
            Z = _getFloat(in byteArray, 10), // index 10 - 13
            RotY = _getFloat(in byteArray, 14) // index 14 - 17
        };
    }

    public static byte[] SerializePositionData(PositionData positionData)
    {
        using (var memoryStream = new MemoryStream())
        using (var writer = new BinaryWriter(memoryStream))
        {
            writer.Write((byte)positionData.CommandID);
            writer.Write(positionData.UserID);
            writer.Write(positionData.X);
            writer.Write(positionData.Y);
            writer.Write(positionData.Z);
            writer.Write(positionData.RotY);

            return memoryStream.ToArray();
        }
    }

    #endregion

    #region PositionDataRTT

    public static PositionDataRTT DeserializePositionDataRTT(in byte[] byteArray)
    {
        if (byteArray.Length < 21) // (1 + 1 + 4 + 4 + 4 + 4 + 4) bytes
        {
            throw new ArgumentException("Byte array is too short to deserialize PositionData.");
        }

        return new PositionDataRTT()
        {
            CommandID = GetCommand(in byteArray), // index 0
            UserID = _getByte(in byteArray, 1), // index 1
            X = _getFloat(in byteArray, 2), // index 2 - 5
            Y = _getFloat(in byteArray, 6), // index 6 - 9 
            Z = _getFloat(in byteArray, 10), // index 10 - 13
            RotY = _getFloat(in byteArray, 14), // index 14 - 17
            TimestampRTT = _getUInt(in byteArray, 18), // index 18 - 21
        };
    }

    public static byte[] SerializePositionDataRTT(PositionDataRTT positionData)
    {

        using (var memoryStream = new MemoryStream())
        using (var writer = new BinaryWriter(memoryStream))
        {
            writer.Write((byte)positionData.CommandID);
            writer.Write(positionData.UserID);
            writer.Write(positionData.X);
            writer.Write(positionData.Y);
            writer.Write(positionData.Z);
            writer.Write(positionData.RotY);
            writer.Write(positionData.TimestampRTT);

            return memoryStream.ToArray();
        }
    }

    #endregion

    #region MoveData

    public static MoveData DeserializeMoveData(in byte[] byteArray)
    {
        if (byteArray.Length < 7) // (1 + 1 + 1 + 4) bytes
        {
            throw new ArgumentException("Byte array is too short to deserialize MoveData.");
        }

        return new MoveData()
        {
            CommandID = GetCommand(in byteArray), // index 0
            UserID = _getByte(in byteArray, 1), // index 1
            DirectionID = _getDirection(in byteArray, 2), // index 2
            Speed = _getFloat(in byteArray, 3) // index 3 - 6
        };
    }

    public static byte[] SerializeMoveData(MoveData moveData)
    {
        using (var memoryStream = new MemoryStream())
        using (var writer = new BinaryWriter(memoryStream))
        {
            writer.Write((byte)moveData.CommandID);
            writer.Write(moveData.UserID);
            writer.Write((byte)moveData.DirectionID);
            writer.Write(moveData.Speed);

            return memoryStream.ToArray();
        }
    }

    #endregion

    #region MoveDataRTT

    public static MoveDataRTT DeserializeMoveDataRTT(in byte[] byteArray)
    {
        if (byteArray.Length < 10) // (1 + 1 + 1 + 4) bytes
        {
            throw new ArgumentException("Byte array is too short to deserialize MoveData.");
        }

        return new MoveDataRTT()
        {
            CommandID = GetCommand(in byteArray), // index 0
            UserID = _getByte(in byteArray, 1), // index 1
            DirectionID = _getDirection(in byteArray, 2), // index 2
            Speed = _getFloat(in byteArray, 3), // index 3 - 6
            TimestampRTT = _getUInt(in byteArray, 7), // index 7 - 10
        };
    }

    public static byte[] SerializeMoveDataRTT(MoveDataRTT moveData)
    {
        using (var memoryStream = new MemoryStream())
        using (var writer = new BinaryWriter(memoryStream))
        {
            writer.Write((byte)moveData.CommandID);
            writer.Write(moveData.UserID);
            writer.Write((byte)moveData.DirectionID);
            writer.Write(moveData.Speed);
            writer.Write(moveData.TimestampRTT);

            return memoryStream.ToArray();
        }
    }

    #endregion

    #region DefaultRTT

    public static DefaultRTT DeserializeDefaultRTT(in byte[] byteArray)
    {
        if (byteArray.Length < 5) // (1 + 4) bytes
        {
            throw new ArgumentException("Byte array is too short to deserialize MoveData.");
        }

        return new DefaultRTT()
        {
            CommandID = (C.Command)byteArray[0], // index 0
            TimestampRTT = _getUInt(in byteArray, 1), // index 1 - 4
        };
    }

    #endregion
}
