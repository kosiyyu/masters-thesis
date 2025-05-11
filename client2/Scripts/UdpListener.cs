using Godot;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Data;
using BU = BinaryUtils;
using C = Command;
using System.Net;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;

public partial class UdpListener : CharacterBody3D
{
	private const int _serverPort = 9000;
	private const string _serverAddress = "127.0.0.1";
	private const int _targetFPS = 100; // updates per second

	private IPEndPoint _serverIP;
	private UdpClient _udpClient;
	private bool _isRunning = false;
	private int _assignedPort = -1; // Dynamically assigned by server

	private byte _userID = 0; // Will be assigned by server
	private CharacterBody3D _player;
	private uint _sequenceNumber = 0;
	private ConcurrentDictionary<uint, long> _sentPackets = new ConcurrentDictionary<uint, long>();
	private double _averageRtt = 0;
	private int _rttSamples = 0;

	private double _timeSinceLastUpdate;
	private double _targetFrameTime = 1.0 / _targetFPS;

	private Vector3 _cachedPosition;
	private float _cachedRotationY;
	private readonly object _positionLock = new object();

	private Dictionary<byte, CharacterBody3D> _otherPlayers = new Dictionary<byte, CharacterBody3D>();
	private PackedScene _playerScene;

	// Log level for debugging
	private enum LogLevel { Debug, Info, Warning, Error }
	private LogLevel _logLevel = LogLevel.Info;
	private HashSet<byte> _userIDsToLog = new HashSet<byte>();
	private string _clientInstanceId;
	private Node3D _playerContainer;

	public void EnableLoggingForUser(byte userID)
	{
		_userIDsToLog.Add(userID);
		Log($"Enabled RTT logging for UserID {userID}", LogLevel.Info);
	}

	public void DisableLoggingForUser(byte userID)
	{
		_userIDsToLog.Remove(userID);
		Log($"Disabled RTT logging for UserID {userID}", LogLevel.Info);
	}

	public void ClearUserLogging()
	{
		_userIDsToLog.Clear();
		Log("Cleared all user-specific RTT logging", LogLevel.Info);
	}

	public override void _Ready()
	{
		_player = this;
		_playerContainer = GetNodeOrNull<Node3D>("PlayerContainer");

		// Create a unique client instance ID (helpful with multiple clients)
		_clientInstanceId = $"Client-{Guid.NewGuid().ToString().Substring(0, 6)}";

		// Load the player scene for other players
		_playerScene = GD.Load<PackedScene>("res://Scenes/player.tscn");
		if (_playerScene == null)
		{
			Log("Failed to load player scene. Make sure the path is correct.", LogLevel.Error);
			// Use a placeholder for development
			_playerScene = new PackedScene();
		}

		InitializeNetwork();
		_isRunning = true;

		// Start network threads
		Task.Run(NetworkLoop);

		// Add visual RTT display
		var rttLabel = new Label();
		rttLabel.Name = "RttLabel";
		rttLabel.Position = new Vector2(10, 10);
		rttLabel.Text = "RTT: Waiting...";
		rttLabel.AddThemeColorOverride("font_color", Colors.Green);
		AddChild(rttLabel);

	}

	public override void _PhysicsProcess(double delta)
	{
		// Update cached values in main thread
		lock (_positionLock)
		{
			_cachedPosition = Position;
			_cachedRotationY = Rotation.Y;
		}

		Velocity = CalculateMovementVelocity();
		MoveAndSlide();
	}

	private void InitializeNetwork()
	{
		try
		{
			_serverIP = new IPEndPoint(IPAddress.Parse(_serverAddress), _serverPort);

			// Create a UDP client that listens on ANY port initially
			_udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0))
			{
				Client = {
				ReceiveTimeout = 0,
				SendTimeout = 0
			}
			};

			// Print actual local endpoint
			IPEndPoint localEndpoint = (IPEndPoint)_udpClient.Client.LocalEndPoint;
			Log($"Client initially listening on {localEndpoint.Address}:{localEndpoint.Port}", LogLevel.Info);

			// Send port request to server immediately
			SendPortRequest();
		}
		catch (Exception e)
		{
			Log($"Network initialization failed: {e}", LogLevel.Error);
		}
	}

	// Add this method to recreate the UdpClient with the assigned port
	private void RebindToAssignedPort()
	{
		try
		{
			if (_assignedPort <= 0) return;

			Log($"Attempting to rebind to assigned port {_assignedPort}...", LogLevel.Info);

			// Close existing client
			_udpClient.Close();

			// Create new client bound to the assigned port
			_udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, _assignedPort))
			{
				Client = {
				ReceiveTimeout = 0,
				SendTimeout = 0
			}
			};

			IPEndPoint localEndpoint = (IPEndPoint)_udpClient.Client.LocalEndPoint;
			Log($"Successfully rebound to {localEndpoint.Address}:{localEndpoint.Port}", LogLevel.Info);
		}
		catch (Exception e)
		{
			Log($"Failed to rebind to assigned port: {e}", LogLevel.Error);

			// Recreate with dynamic port as fallback
			_udpClient = new UdpClient(new IPEndPoint(IPAddress.Any, 0))
			{
				Client = {
				ReceiveTimeout = 0,
				SendTimeout = 0
			}
			};
		}
	}

	private void SendPortRequest()
	{
		try
		{
			byte[] requestData = [(byte)C.Command.PORT_REQUEST];
			_udpClient.Send(requestData, requestData.Length, _serverIP);
			Log("Port request sent to server", LogLevel.Debug);
		}
		catch (Exception e)
		{
			Log($"Failed to send port request: {e}", LogLevel.Error);
		}
	}

	private Vector3 CalculateMovementVelocity()
	{
		// Get 2D input vector (-1 to 1 in both axes)
		Vector2 inputDirection = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");

		// Convert to 3D movement direction relative to player rotation
		Vector3 movementDirection = new Vector3(
			x: inputDirection.X,
			y: 0,
			z: inputDirection.Y
		).Normalized();

		// Rotate direction to match camera/player orientation
		movementDirection = movementDirection.Rotated(Vector3.Up, _player.Rotation.Y);

		return movementDirection * 10f;
	}

	private async Task NetworkLoop()
	{
		var timer = new PeriodicTimer(TimeSpan.FromSeconds(_targetFrameTime));

		while (_isRunning)
		{
			try
			{
				// Force UserID if we have a port but no UserID
				if (_assignedPort > 0 && _userID <= 0)
				{
					Log("Port assigned but UserID missing - forcing UserID = 1 for testing", LogLevel.Warning);
					_userID = 1;
				}

				// Check for incoming packets (non-blocking)
				if (_udpClient.Available > 0)
				{
					IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
					byte[] data = _udpClient.Receive(ref remoteEP);
					GD.Print($"Received packet from {remoteEP} ({data.Length} bytes)");
					ProcessPacket(data);
				}

				// Debug current state
				GD.Print($"Network state: UserID={_userID}, AssignedPort={_assignedPort}");

				// Use original if/else logic but with extra debugging
				if (_userID > 0 && _assignedPort > 0)
				{
					SendPositionUpdate();
				}
				else if (_assignedPort <= 0)
				{
					GD.Print("No port assigned yet");
					if (DateTime.Now.Second % 5 == 0)
					{
						SendPortRequest();
					}
				}
				else
				{
					GD.Print("Port assigned but waiting for UserID");
				}

				// Wait for next tick
				await timer.WaitForNextTickAsync();
			}
			catch (Exception e)
			{
				Log($"Network error: {e}", LogLevel.Error);
				await Task.Delay(1000); // Add delay on error to avoid tight loop
			}
		}
	}

	private void SendPositionUpdate()
	{
		if (_userID == 0) return; // Don't send until we have an ID

		Vector3 position;
		float rotationY;

		lock (_positionLock)
		{
			position = _cachedPosition;
			rotationY = _cachedRotationY;
		}

		var sequence = _sequenceNumber++;
		var positionData = new PositionDataRTT
		{
			CommandID = C.Command.POSITION_RTT,
			UserID = _userID,
			X = position.X,
			Y = position.Y,
			Z = position.Z,
			RotY = rotationY,
			TimestampRTT = sequence
		};

		_sentPackets[sequence] = Stopwatch.GetTimestamp();
		Log($"Sending position update with RTT timestamp: {sequence}", LogLevel.Debug);

		try
		{
			var byteArray = BU.BinaryUtils.SerializePositionDataRTT(positionData);
			PrintPacketBytes("Position update packet", byteArray);
			_udpClient.Send(byteArray, byteArray.Length, _serverIP);
		}
		catch (Exception e)
		{
			Log($"Failed to send position update: {e}", LogLevel.Error);
		}
	}

	private void ProcessPacket(byte[] data)
	{
		if (data == null || data.Length == 0) return;

		try
		{
			// Print raw bytes for debugging
			string byteString = BitConverter.ToString(data).Replace("-", " ");
			GD.Print($"Processing packet: [{byteString}]");

			var command = BU.BinaryUtils.GetCommand(data);
			GD.Print($"Packet command: {command} (0x{(byte)command:X2})");

			switch (command)
			{
				case C.Command.PORT_ASSIGNMENT:
					GD.Print("Processing as PORT_ASSIGNMENT");
					HandlePortAssignment(data);
					break;

				case C.Command.USER_ASSIGNMENT:
					GD.Print("Processing as USER_ASSIGNMENT");
					HandleUserAssignment(data);
					break;

				case C.Command.DEFAULT_RTT:
					GD.Print("Processing as DEFAULT_RTT");
					HandleRttResponse(data);
					break;

				case C.Command.POSITION_RTT:
					GD.Print("Processing as POSITION_RTT");
					HandlePositionUpdate(data);
					break;

				default:
					GD.Print($"Unknown command: {command}");
					break;
			}
		}
		catch (Exception e)
		{
			Log($"Failed to process packet: {e}", LogLevel.Error);
		}
	}

	// Enhanced HandleRttResponse to provide more details
	private void HandleRttResponse(byte[] data)
	{
		try
		{
			var response = BU.BinaryUtils.DeserializeDefaultRTT(data);

			if (!_sentPackets.TryRemove(response.TimestampRTT, out long sendTimestamp))
			{

			}

			var receiveTimestamp = Stopwatch.GetTimestamp();
			var rttSeconds = (receiveTimestamp - sendTimestamp) / (double)Stopwatch.Frequency;
			var rttMs = rttSeconds * 1000;

			UpdateRttStatistics(rttMs);
		}
		catch (Exception e)
		{
			GD.Print($"RTT response error: {e}");
		}
	}

	private void HandlePortAssignment(byte[] data)
	{
		try
		{
			var assignment = BU.BinaryUtils.DeserializePortAssignment(data);
			_assignedPort = assignment.Port;

			// Also use UserID from PortAssignment
			if (assignment.UserID > 0)
			{
				_userID = assignment.UserID;
				// Enable logging for this client's UserID
				EnableLoggingForUser(_userID);
			}

			Log($"Assigned port: {_assignedPort}, Current UserID: {_userID}", LogLevel.Info);

			// Setup a dedicated client for receiving on the assigned port
			SetupReceiveClient();
		}
		catch (Exception e)
		{
			Log($"Failed to process port assignment: {e}", LogLevel.Error);
		}
	}

	private void HandleUserAssignment(byte[] data)
	{
		try
		{
			var assignment = BU.BinaryUtils.DeserializeUserAssignment(data);
			_userID = assignment.UserID;
			Log($"Assigned UserID: {_userID}", LogLevel.Info);
		}
		catch (Exception e)
		{
			Log($"Failed to process user assignment: {e}", LogLevel.Error);
		}
	}

	private void PrintPacketBytes(string label, byte[] data)
	{
		if (data == null || data.Length == 0)
		{
			Log($"{label}: Empty packet", LogLevel.Debug);
			return;
		}

		var hexString = BitConverter.ToString(data);
		Log($"{label} ({data.Length} bytes): {hexString}", LogLevel.Debug);
	}

	private void HandlePositionUpdate(byte[] data)
	{
		try
		{
			var positionData = BU.BinaryUtils.DeserializePositionData(data);

			// Skip our own position updates
			if (positionData.UserID == _userID) return;

			// Queue the update to happen on the main thread
			CallDeferred(nameof(UpdatePlayerPosition),
				positionData.UserID,
				positionData.X,
				positionData.Y,
				positionData.Z,
				positionData.RotY);
		}
		catch (Exception e)
		{
			Log($"Failed to process position update: {e}", LogLevel.Error);
		}
	}

	private UdpClient _receiveClient = null;

	private async Task ReceiveLoop()
	{
		Log("Starting receive loop...", LogLevel.Info);

		while (_isRunning && _receiveClient != null)
		{
			try
			{
				if (_receiveClient.Available > 0)
				{
					IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
					byte[] data = _receiveClient.Receive(ref remoteEP);

					ProcessReceivedPacket(data);
				}
				// Prevent my pc from burning
				await Task.Delay(1);
			}
			catch (Exception e)
			{
				Log($"Receive loop error: {e}", LogLevel.Error);
				await Task.Delay(1000); // Longer delay after error
			}
		}

		Log("Receive loop ended", LogLevel.Info);
	}

	private void ProcessReceivedPacket(byte[] data)
	{
		if (data == null || data.Length == 0) return;

		try
		{
			var command = BU.BinaryUtils.GetCommand(data);

			if (command == C.Command.DEFAULT_RTT)
			{
				HandleRttResponse(data);
			}
			else if (command == C.Command.POSITION)
			{
				HandlePositionUpdate(data);
			}
			else
			{
				// Log($"RECEIVE CLIENT: Ignoring non-RTT command {command}", LogLevel.Info);
			}
		}
		catch (Exception e)
		{
			Log($"Error processing received packet: {e}", LogLevel.Error);
		}
	}

	private void SetupReceiveClient()
	{
		try
		{
			if (_assignedPort <= 0) return;

			// If we already have a receive client, close it
			if (_receiveClient != null)
			{
				try { _receiveClient.Close(); } catch { }
				_receiveClient = null;
			}

			Log($"Setting up receive client on port {_assignedPort}...", LogLevel.Info);

			// Create a separate client specifically for receiving on the assigned port
			_receiveClient = new UdpClient(_assignedPort)
			{
				Client = {
				ReceiveTimeout = 0
			}
			};

			Log($"Receive client listening on port {_assignedPort}", LogLevel.Info);

			// Start a separate thread to listen for responses on this client
			Task.Run(ReceiveLoop);
		}
		catch (Exception e)
		{
			Log($"Failed to setup receive client: {e}", LogLevel.Error);
		}
	}


	// Called on the main thread to update player positions
	private void UpdatePlayerPosition(byte userID, float x, float y, float z, float rotY)
	{
		if (!_otherPlayers.TryGetValue(userID, out var playerNode))
		{
			// Create new player instance
			playerNode = _playerScene.Instantiate<CharacterBody3D>();
			AddChild(playerNode);
			_otherPlayers[userID] = playerNode;

			// Set a different color or visual to distinguish players
			MeshInstance3D mesh = playerNode.GetNodeOrNull<MeshInstance3D>("Mesh");
			if (mesh != null)
			{
				StandardMaterial3D material = new StandardMaterial3D();
				// Generate a unique color based on user ID
				material.AlbedoColor = new Color(
					(userID * 50) % 255 / 255f,
					(userID * 120) % 255 / 255f,
					(userID * 200) % 255 / 255f);
				mesh.MaterialOverride = material;
			}

			Log($"New player joined with ID: {userID}", LogLevel.Info);
		}

		// Apply position and rotation
		playerNode.Position = new Vector3(x, y, z);
		playerNode.Rotation = new Vector3(0, rotY, 0);
	}

	private void UpdateRttStatistics(double newRttMs)
	{
		_rttSamples++;
		_averageRtt = (_averageRtt * (_rttSamples - 1) + newRttMs) / _rttSamples;

		// Always update the visual display regardless of logging settings
		CallDeferred(nameof(UpdateRttLabel), newRttMs, _averageRtt);

		// Only log if this UserID is in the logging set
		if (_userIDsToLog.Contains(_userID))
		{
			Log($"RTT [{_userID}]: {newRttMs:F2}ms | Avg: {_averageRtt:F2}ms", LogLevel.Info);
		}
	}

	private void UpdateRttLabel(double rttMs, double avgRtt)
	{
		var label = GetNodeOrNull<Label>("RttLabel");
		if (label != null)
		{
			label.Text = $"RTT: {rttMs:F2}ms | Avg: {avgRtt:F2}ms | UserID: {_userID}";
		}
	}

	private void Log(string message, LogLevel level)
	{
		if (level >= _logLevel)
		{
			string clientTag = !string.IsNullOrEmpty(_clientInstanceId) ? $"[{_clientInstanceId}]" : "";

			switch (level)
			{
				case LogLevel.Debug:
					GD.Print($"[DEBUG]{clientTag} {message}");
					break;
				case LogLevel.Info:
					GD.Print($"[INFO]{clientTag} {message}");
					break;
				case LogLevel.Warning:
					GD.PushWarning($"[WARNING]{clientTag} {message}");
					break;
				case LogLevel.Error:
					GD.PrintErr($"[ERROR]{clientTag} {message}");
					break;
			}
		}
	}

	public override void _ExitTree()
	{
		_isRunning = false;
		foreach (var player in _otherPlayers.Values)
		{
			player.QueueFree();
		}
		_otherPlayers.Clear();
		_udpClient?.Close();
		Log("Network shutdown complete", LogLevel.Info);
	}
}
