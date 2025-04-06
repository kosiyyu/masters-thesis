extends Node

class DataInterface:
	func pack_to_bytearray() -> PackedByteArray:
		push_error("DataInterface.pack_to_bytearray() must be implemented in derived class.")
		var x: PackedByteArray = PackedByteArray()
		return x
