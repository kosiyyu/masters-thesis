namespace BinaryUtils;

using System;
using C = Command;
using D = Direction;
using Data;
using System.IO;

public static class BinaryUtils
{
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

    private static C.Command _getCommand(in byte[] byteArray)
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

    #region PositionData

    public static PositionData DeserializePositionData(in byte[] byteArray)
    {
        if (byteArray.Length < 18) // (1 + 1 + 4 + 4 + 4 + 4) bytes
        {
            throw new ArgumentException("Byte array is too short to deserialize PositionData.");
        }

        return new PositionData()
        {
            CommandID = _getCommand(in byteArray), // index 0
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

    #region MoveData

    public static MoveData DeserializeMoveData(in byte[] byteArray)
    {
        if (byteArray.Length < 7) // (1 + 1 + 1 + 4) bytes
        {
            throw new ArgumentException("Byte array is too short to deserialize MoveData.");
        }

        return new MoveData()
        {
            CommandID = _getCommand(in byteArray), // index 0
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
}
