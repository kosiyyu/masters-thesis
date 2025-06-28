namespace BinaryUtils;

using System;
using System.Buffers;
using C = Command;
using D = Direction;
using Data;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

public static class BinaryUtils
{
	// Static buffer pools for serialization to avoid GC allocations
	private static readonly ArrayPool<byte> _bytePool = ArrayPool<byte>.Shared;

	#region _get complex types

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static C.Command GetCommand(in byte[] byteArray)
	{
		if (byteArray.Length < 1)
		{
			throw new ArgumentException("Byte array should have at least one byte.");
		}

		byte value = byteArray[0];
		return (C.Command)value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static D.Direction _getDirection(in byte[] byteArray, int startIndex)
	{
		if (startIndex < 0 || startIndex >= byteArray.Length)
		{
			throw new ArgumentException("Invalid start index for direction.");
		}

		return (D.Direction)byteArray[startIndex];
	}

	#endregion

	#region PositionData

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static PositionData DeserializePositionData(in byte[] byteArray)
	{
		if (byteArray.Length < 18) // (1 + 1 + 4 + 4 + 4 + 4) bytes
		{
			throw new ArgumentException("Byte array is too short to deserialize PositionData.");
		}

		// Span<T> for more efficient memory access
		ReadOnlySpan<byte> dataSpan = byteArray;

		return new PositionData()
		{
			CommandID = (C.Command)dataSpan[0],
			UserID = dataSpan[1],
			X = BitConverter.ToSingle(dataSpan.Slice(2, 4)),
			Y = BitConverter.ToSingle(dataSpan.Slice(6, 4)),
			Z = BitConverter.ToSingle(dataSpan.Slice(10, 4)),
			RotY = BitConverter.ToSingle(dataSpan.Slice(14, 4))
		};
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static byte[] SerializePositionData(PositionData positionData)
	{
		// Get buffer from pool
		byte[] buffer = _bytePool.Rent(18);

		try
		{
			Span<byte> span = buffer.AsSpan(0, 18);

			// Write data directly to span
			span[0] = (byte)positionData.CommandID;
			span[1] = positionData.UserID;

			// Write floats
			BitConverter.TryWriteBytes(span.Slice(2, 4), positionData.X);
			BitConverter.TryWriteBytes(span.Slice(6, 4), positionData.Y);
			BitConverter.TryWriteBytes(span.Slice(10, 4), positionData.Z);
			BitConverter.TryWriteBytes(span.Slice(14, 4), positionData.RotY);

			// Create a copy to return (the caller owns this memory)
			byte[] result = new byte[18];
			span.Slice(0, 18).CopyTo(result);
			return result;
		}
		finally
		{
			// Return buffer to pool
			_bytePool.Return(buffer);
		}
	}

	#endregion

	#region PositionDataRTT

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static PositionDataRTT DeserializePositionDataRTT(in byte[] byteArray)
	{
		if (byteArray.Length < 22) // (1 + 1 + 4 + 4 + 4 + 4 + 4) bytes
		{
			throw new ArgumentException("Byte array is too short to deserialize PositionDataRTT.");
		}

		// Span<T> for more efficient memory access
		ReadOnlySpan<byte> dataSpan = byteArray;

		return new PositionDataRTT()
		{
			CommandID = (C.Command)dataSpan[0],
			UserID = dataSpan[1],
			X = BitConverter.ToSingle(dataSpan.Slice(2, 4)),
			Y = BitConverter.ToSingle(dataSpan.Slice(6, 4)),
			Z = BitConverter.ToSingle(dataSpan.Slice(10, 4)),
			RotY = BitConverter.ToSingle(dataSpan.Slice(14, 4)),
			TimestampRTT = BitConverter.ToUInt32(dataSpan.Slice(18, 4))
		};
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static byte[] SerializePositionDataRTT(PositionDataRTT positionData)
	{
		// Get buffer from pool
		byte[] buffer = _bytePool.Rent(22);

		try
		{
			Span<byte> span = buffer.AsSpan(0, 22);

			// Write data directly to span
			span[0] = (byte)positionData.CommandID;
			span[1] = positionData.UserID;

			// Write floats and uint
			BitConverter.TryWriteBytes(span.Slice(2, 4), positionData.X);
			BitConverter.TryWriteBytes(span.Slice(6, 4), positionData.Y);
			BitConverter.TryWriteBytes(span.Slice(10, 4), positionData.Z);
			BitConverter.TryWriteBytes(span.Slice(14, 4), positionData.RotY);
			BitConverter.TryWriteBytes(span.Slice(18, 4), positionData.TimestampRTT);

			// Create a copy to return (the caller owns this memory)
			byte[] result = new byte[22];
			span.Slice(0, 22).CopyTo(result);
			return result;
		}
		finally
		{
			// Return buffer to pool
			_bytePool.Return(buffer);
		}
	}

	#endregion

	#region MoveData

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static MoveData DeserializeMoveData(in byte[] byteArray)
	{
		if (byteArray.Length < 7) // (1 + 1 + 1 + 4) bytes
		{
			throw new ArgumentException("Byte array is too short to deserialize MoveData.");
		}

		ReadOnlySpan<byte> dataSpan = byteArray;

		return new MoveData()
		{
			CommandID = (C.Command)dataSpan[0],
			UserID = dataSpan[1],
			DirectionID = (D.Direction)dataSpan[2],
			Speed = BitConverter.ToSingle(dataSpan.Slice(3, 4))
		};
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static byte[] SerializeMoveData(MoveData moveData)
	{
		byte[] buffer = _bytePool.Rent(7);

		try
		{
			Span<byte> span = buffer.AsSpan(0, 7);

			span[0] = (byte)moveData.CommandID;
			span[1] = moveData.UserID;
			span[2] = (byte)moveData.DirectionID;

			BitConverter.TryWriteBytes(span.Slice(3, 4), moveData.Speed);

			byte[] result = new byte[7];
			span.Slice(0, 7).CopyTo(result);
			return result;
		}
		finally
		{
			_bytePool.Return(buffer);
		}
	}

	#endregion

	#region MoveDataRTT

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static MoveDataRTT DeserializeMoveDataRTT(in byte[] byteArray)
	{
		if (byteArray.Length < 11) // (1 + 1 + 1 + 4 + 4) bytes
		{
			throw new ArgumentException("Byte array is too short to deserialize MoveDataRTT.");
		}

		ReadOnlySpan<byte> dataSpan = byteArray;

		return new MoveDataRTT()
		{
			CommandID = (C.Command)dataSpan[0],
			UserID = dataSpan[1],
			DirectionID = (D.Direction)dataSpan[2],
			Speed = BitConverter.ToSingle(dataSpan.Slice(3, 4)),
			TimestampRTT = BitConverter.ToUInt32(dataSpan.Slice(7, 4))
		};
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static byte[] SerializeMoveDataRTT(MoveDataRTT moveData)
	{
		byte[] buffer = _bytePool.Rent(11);

		try
		{
			Span<byte> span = buffer.AsSpan(0, 11);

			span[0] = (byte)moveData.CommandID;
			span[1] = moveData.UserID;
			span[2] = (byte)moveData.DirectionID;

			BitConverter.TryWriteBytes(span.Slice(3, 4), moveData.Speed);
			BitConverter.TryWriteBytes(span.Slice(7, 4), moveData.TimestampRTT);

			byte[] result = new byte[11];
			span.Slice(0, 11).CopyTo(result);
			return result;
		}
		finally
		{
			_bytePool.Return(buffer);
		}
	}

	#endregion

	#region DefaultRTT

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static DefaultRTT DeserializeDefaultRTT(in byte[] byteArray)
	{
		if (byteArray.Length < 5) // (1 + 4) bytes
		{
			throw new ArgumentException("Byte array is too short to deserialize DefaultRTT.");
		}

		ReadOnlySpan<byte> dataSpan = byteArray;

		return new DefaultRTT()
		{
			CommandID = (C.Command)dataSpan[0],
			TimestampRTT = BitConverter.ToUInt32(dataSpan.Slice(1, 4))
		};
	}

	#endregion

	#region UserAssignment

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static UserAssignment DeserializeUserAssignment(in byte[] byteArray)
	{
		if (byteArray.Length < 2) // (1 + 1) bytes
		{
			throw new ArgumentException("Byte array is too short to deserialize UserAssignment.");
		}

		ReadOnlySpan<byte> dataSpan = byteArray;

		return new UserAssignment()
		{
			CommandID = (C.Command)dataSpan[0],
			UserID = dataSpan[1],
		};
	}

	#endregion

	#region PortAssignment

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static PortAssignment DeserializePortAssignment(in byte[] byteArray)
	{
		if (byteArray.Length < 4) // (1 + 1 + 2) bytes for Command, UserID, and Port
		{
			throw new ArgumentException("Byte array is too short to deserialize PortAssignment.");
		}

		ReadOnlySpan<byte> dataSpan = byteArray;

		return new PortAssignment()
		{
			CommandID = (C.Command)dataSpan[0],
			UserID = dataSpan[1],
			Port = BitConverter.ToUInt16(dataSpan.Slice(2, 2)) // Read 2 bytes for Port
		};
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static byte[] SerializePortAssignment(in Data.PortAssignment assignment)
	{
		byte[] result = new byte[4]; // Total size: 1 + 1 + 2 = 4 bytes

		result[0] = (byte)assignment.CommandID;
		result[1] = assignment.UserID;

		// Write Port (2 bytes)
		BitConverter.TryWriteBytes(new Span<byte>(result, 2, 2), assignment.Port);

		return result;
	}

	#endregion

}
