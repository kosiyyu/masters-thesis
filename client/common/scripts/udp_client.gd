extends Node

class_name UdpClient

signal packet_received(data: PackedByteArray, ip: String, port: int)

var socket = PacketPeerUDP.new()
var server_ip: String
var server_port: int
var local_port: int = 0 # Use 0 to let OS choose a free port

func _init(ip: String = "127.0.0.1", port: int = 9000, bind_port: int = 0): # Changed port to match server
	server_ip = ip
	server_port = port
	local_port = bind_port
	
	Global.log("CLIENT: Initializing with server at {0}:{1}", [server_ip, server_port])
	Global.log("CLIENT: Attempting to bind to local port: {0}", [local_port])
	
	# Try to bind multiple times with different ports if needed
	var max_attempts = 3
	var attempt = 0
	var err = ERR_CANT_CREATE
	
	while err != OK and attempt < max_attempts:
		attempt += 1
		err = socket.bind(local_port)
		if err != OK:
			Global.log("CLIENT: Bind attempt {0} failed with error: {1}", [attempt, err])
			local_port = 10000 + randi() % 50000 # Try a random high port
	
	if err == OK:
		Global.log("CLIENT: Bound to local port: {0}", [socket.get_local_port()])
	else:
		Global.log("CLIENT: All bind attempts FAILED! Last error code: ", err)
		Global.log("CLIENT: Will still try to send, but may not receive responses")
	
	Global.log("CLIENT: Setting destination address to {0}:{1}", [server_ip, server_port])
	socket.set_dest_address(server_ip, server_port)
	Global.log("CLIENT: Client setup complete")

func send_packet(data: PackedByteArray):
	# Global.log("CLIENT: Preparing to send packet of size: {0}", [data.size()])
	if data.size() > 0:
		var text_content = data
		Global.log("CLIENT: Packet content: {0}", [text_content])
		print(text_content)
	
	var resp = socket.put_packet(data)
	if resp == OK:
		# Global.log("CLIENT: Packet sent successfully to {0}:{1}", [server_ip, server_port])
		pass
	else:
		Global.log("CLIENT: Failed to send packet! Error code: {0}", [resp])
		Global.log("CLIENT: Is server running? Current dest address: {0}:{1}", [server_ip, server_port])

func listen_for_response():
	# Print occasional checks
	if randi() % 100 == 0: # Print only occasionally to avoid spam
		Global.log("CLIENT: Checking for incoming packets...")
	
	var available = socket.get_available_packet_count()
	if available > 0:
		Global.log("CLIENT: {0} packets available!", [available])
		var packet = socket.get_packet()
		var ip = socket.get_packet_ip()
		var port = socket.get_packet_port()
		
		Global.log("CLIENT: Packet from {0}:{1} - Size: {2}", [ip, port, packet.size()])
		if packet.size() > 0:
			var text_content = packet.get_string_from_utf8()
			Global.log("CLIENT: Packet content: {0}", [text_content])
		
		packet_received.emit(packet, ip, port)
	elif randi() % 400 == 0: # Less frequent message
		Global.log("CLIENT: No packets available yet")
