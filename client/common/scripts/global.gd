extends Node3D

var logging_enabled: bool = true

func log(msg: String, args: Array = []) -> void:
	if logging_enabled:
		print(msg.format(args))

enum Command {
  POSITION, # 0
  MOVE # 1
}

class PositionData:
	var command_id: int
	var user_id: int
	var x: float # Internally 64-bit
	var y: float # Internally 64-bit
	var z: float # Internally 64-bit
	var rotation: float # Internally 64-bit

	func _init(_user_id: int, _x: float, _y: float, _z: float, _rotation: float) -> void:
		if _user_id < 0 or _user_id > 255:
			push_error("_user_id must be a uint8")

		self.command_id = Command.POSITION
		if command_id < 0 or command_id > 255:
			push_error("command_id must be a uint8")

		self.user_id = _user_id
		self.x = _x
		self.y = _y
		self.z = _z
		self.rotation = _rotation

	func _to_string() -> String:
		return "%d;%d;%f;%f;%f;%f;" % [command_id, user_id, x, y, z, rotation]

	func pack_to_bytearray() -> PackedByteArray:
		var byte_array = PackedByteArray()
		byte_array.resize(2 + 4 * 4) # 2 bytes for commands + 4 floats at 4 bytes each
		byte_array.encode_u8(0, command_id)
		byte_array.encode_u8(1, user_id)
		byte_array.encode_float(2, x) # Convert 64-bit to 32-bit
		byte_array.encode_float(6, y) # Convert 64-bit to 32-bit
		byte_array.encode_float(10, z) # Convert 64-bit to 32-bit
		byte_array.encode_float(14, rotation) # Convert 64-bit to 32-bit
		return byte_array

	static func unpack_from_bytearray(byte_array: PackedByteArray) -> PositionData:
		var _command_id = byte_array.decode_u8(0)
		var _user_id = byte_array.decode_u8(1)
		var _x = byte_array.decode_float(2) # Return as 64-bit float
		var _y = byte_array.decode_float(6) # Return as 64-bit float
		var _z = byte_array.decode_float(10) # Return as 64-bit float
		var _rotation = byte_array.decode_float(14) # Return as 64-bit float

		var position_data = PositionData.new(_user_id, _x, _y, _z, _rotation)
		position_data.command_id = _command_id
		return position_data
