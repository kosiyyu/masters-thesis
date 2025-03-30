extends Node

var client: UdpClient = null
var networking_thread: Thread = null
var running: bool = true
var mutex: Mutex = Mutex.new()
var received_packets = []
var player_data: String = ""
var are_logs: bool = true
var delay: int = 50
		
func _ready():
	client = UdpClient.new()
	
	# Run main loop in a thread
	networking_thread = Thread.new()
	networking_thread.start(_loop)
	
	Global.log("Threaded UDP Client initialized")
	
# Client main loop for thread to use
func _loop():
	while running:
		client.listen_for_response()
		OS.delay_msec(delay) # 50ms = 20 updates per second

#######################################################################
#                             IMPORTANT                               #
#######################################################################
# THIS FUNCTION IS USED BY OTHER SCRIPTS
# DO NOT CHANGE THE NAME OR THE FUNCTION SIGNATURE
func send_player_data(byte_array: PackedByteArray):
	client.send_packet(byte_array)

func send_player_data_to_server():
	if player_data.is_empty():
		return

# Implement _process for handling any received data in the main thread
func _process(_delta):
	mutex.lock()
	var packets = received_packets.duplicate()
	received_packets.clear()
	mutex.unlock()
	
	# Process any received packets
	for packet_data in packets:
		var text = packet_data.get_string_from_utf8()
		Global.log("Received: ", text)
		# Handle packet...
		
func process_player_data(data_str: String):
	# Parse the string format: "id;x;y;z;rot;"
	var parts = data_str.split(";")
	if parts.size() >= 5:
		var player_id = parts[0]
		var pos_x = float(parts[1])
		var pos_y = float(parts[2])
		var pos_z = float(parts[3])
		var rot_y = float(parts[4])
		
		# Get the PlayerManager to update the appropriate remote player
		var player_manager = get_node("../../PlayerManager")
		if player_manager:
			player_manager.update_remote_player(player_id, pos_x, pos_y, pos_z, rot_y)

# Called when the UDP client receives data
func _on_packet_received(data: PackedByteArray, ip: String, port: int):
	var text = data.get_string_from_utf8()
	Global.log("Received: {0}, from: {1}, on port: {2}", [text, ip, str(port)])
	
	# Parse player data and update game state
	# This would typically update other players' positions
	process_player_data(text)

func _exit_tree():
	# Stop the thread
	running = false
	if networking_thread and networking_thread.is_alive():
		networking_thread.wait_to_finish()
	
	# Clean up
	if client:
		client.queue_free()
