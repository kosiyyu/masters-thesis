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

public partial class PlayersManager : Node3D
{
	// Networking constants
	private const int _serverPort = 8080;
	private const string _serverAddress = "127.0.0.1";
	private const int _targetFPS = 100; // updates per second

	// Reference to the player container node
	private Node3D _playerContainer;

	// Player management
	[Export]
	private PackedScene _playerScene;
	private Dictionary<byte, CharacterBody3D> _otherPlayers = new Dictionary<byte, CharacterBody3D>();
	private CharacterBody3D _localPlayer;

	// Network components
	private IPEndPoint _serverIP;
	private UdpClient _udpClient;
	private UdpClient _receiveClient;
	private bool _isRunning = false;
	private int _assignedPort = -1; // Dynamically assigned by server
	private byte _userID = 0; // Will be assigned by server
	private double _targetFrameTime = 1.0 / _targetFPS;

	// RTT tracking
	private uint _sequenceNumber = 0;
	private ConcurrentDictionary<uint, long> _sentPackets = new ConcurrentDictionary<uint, long>();
	private double _averageRtt = 0;
	private int _rttSamples = 0;

	// Log level for debugging
	private enum LogLevel { Debug, Info, Warning, Error }
	private LogLevel _logLevel = LogLevel.Info;
	private HashSet<byte> _userIDsToLog = new HashSet<byte>();
	private string _clientInstanceId;

	// UI Elements
	private Label _rttLabel;

	public override void _Ready()
	{
		// Find or create the player container
		_playerContainer = GetNodeOrNull<Node3D>("../PlayerContainer");
		if (_playerContainer == null)
		{
			GD.PrintErr("PlayerContainer node not found! Creating one.");
			_playerContainer = new Node3D();
			_playerContainer.Name = "PlayerContainer";
			GetParent().AddChild(_playerContainer);
		}

		// Load the player scene
		if (_playerScene == null)
		{
			_playerScene = GD.Load<PackedScene>("res://Scenes/player.tscn");
			if (_playerScene == null)
			{
				GD.PrintErr("Failed to load player scene. Make sure the path is correct.");
				_playerScene = new PackedScene();
			}
		}

		// Create a unique client instance ID (helpful with multiple clients)
		_clientInstanceId = $"Client-{Guid.NewGuid().ToString().Substring(0, 6)}";

		// Initialize network
		InitializeNetwork();
		_isRunning = true;

		// Start network thread
		Task.Run(NetworkLoop);

		// Setup UI for RTT display
		SetupRttDisplay();
	}

	private void SetupRttDisplay()
	{
		_rttLabel = new Label();
		_rttLabel.Name = "RttLabel";
		_rttLabel.Position = new Vector2(10, 10);
		_rttLabel.Text = "RTT: Waiting...";
		_rttLabel.AddThemeColorOverride("font_color", Colors.Green);
		AddChild(_rttLabel);
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

	private async Task NetworkLoop()
	{
		var timer = new PeriodicTimer(TimeSpan.FromSeconds(_targetFrameTime));

		while (_isRunning)
		{
			try
			{
				// Check for incoming packets (non-blocking)
				if (_udpClient.Available > 0)
				{
					IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
					byte[] data = _udpClient.Receive(ref remoteEP);
					ProcessPacket(data);
				}

				// Retry port request if we don't have one yet
				if (_assignedPort <= 0 && DateTime.Now.Second % 5 == 0)
				{
					SendPortRequest();
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

	/// Called when local player is first created after user ID is assigned
	private void SpawnLocalPlayer()
	{
		if (_localPlayer != null) return;

		_localPlayer = _playerScene.Instantiate<CharacterBody3D>();
		_localPlayer.Position = new Vector3(_localPlayer.Position.X, 1, _localPlayer.Position.Z);
		_playerContainer.AddChild(_localPlayer);

		// Explicitly set the parent of the controller to be the local player
		var controller = new PlayerController();
		_localPlayer.AddChild(controller);

		// Print debug information
		GD.Print($"Local player spawned with ID: {_userID}, adding controller");
		GD.Print($"Controller parent: {controller.GetParent()?.Name}");

		// Connect to position updates from controller
		controller.Connect(PlayerController.SignalName.PlayerPositionUpdated,
			new Callable(this, MethodName.OnLocalPlayerPositionUpdated));

		// Print debug information about signals
		GD.Print($"Connected PlayerPositionUpdated signal");
	}

	/// Called when the local player's position is updated
	private void OnLocalPlayerPositionUpdated(Vector3 position, float rotationY)
	{
		SendPositionUpdate(_userID, position, rotationY);
	}

	/// Sends a position update to the server
	private void SendPositionUpdate(byte userId, Vector3 position, float rotationY)
	{
		if (_userID == 0 || _assignedPort <= 0) return; // Don't send until we have an ID and port

		var sequence = _sequenceNumber++;
		var positionData = new PositionDataRTT
		{
			CommandID = C.Command.POSITION_RTT,
			UserID = userId,
			X = position.X,
			Y = position.Y,
			Z = position.Z,
			RotY = rotationY,
			TimestampRTT = sequence
		};

		_sentPackets[sequence] = Stopwatch.GetTimestamp();

		try
		{
			var byteArray = BU.BinaryUtils.SerializePositionDataRTT(positionData);
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
			var command = BU.BinaryUtils.GetCommand(data);

			switch (command)
			{
				case C.Command.PORT_ASSIGNMENT:
					HandlePortAssignment(data);
					break;

				case C.Command.USER_ASSIGNMENT:
					HandleUserAssignment(data);
					break;

				case C.Command.DEFAULT_RTT:
					HandleRttResponse(data);
					break;

				case C.Command.POSITION_RTT:
				case C.Command.POSITION:
					HandlePositionUpdate(data);
					break;

				default:
					Log($"Unknown command: {command}", LogLevel.Debug);
					break;
			}
		}
		catch (Exception e)
		{
			Log($"Failed to process packet: {e}", LogLevel.Error);
		}
	}

	private void HandleRttResponse(byte[] data)
	{
		try
		{
			var response = BU.BinaryUtils.DeserializeDefaultRTT(data);

			if (_sentPackets.TryRemove(response.TimestampRTT, out long sendTimestamp))
			{
				var receiveTimestamp = Stopwatch.GetTimestamp();
				var rttSeconds = (receiveTimestamp - sendTimestamp) / (double)Stopwatch.Frequency;
				var rttMs = rttSeconds * 1000;

				UpdateRttStatistics(rttMs);
			}
		}
		catch (Exception e)
		{
			Log($"RTT response error: {e}", LogLevel.Error);
		}
	}

	private void HandlePortAssignment(byte[] data)
	{
		try
		{
			var assignment = BU.BinaryUtils.DeserializePortAssignment(data);
			_assignedPort = assignment.Port;

			//  Use UserID from PortAssignment
			if (assignment.UserID > 0)
			{
				_userID = assignment.UserID;
				EnableLoggingForUser(_userID);

				// Spawn local player now that we have a user ID
				CallDeferred(nameof(SpawnLocalPlayer));
			}

			Log($"Assigned port: {_assignedPort}, UserID: {_userID}", LogLevel.Info);

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

			// Enable logging for our user ID
			EnableLoggingForUser(_userID);

			// Spawn local player now that we have a user ID
			CallDeferred(nameof(SpawnLocalPlayer));
		}
		catch (Exception e)
		{
			Log($"Failed to process user assignment: {e}", LogLevel.Error);
		}
	}

	private void HandlePositionUpdate(byte[] data)
	{
		try
		{
			var positionData = BU.BinaryUtils.DeserializePositionData(data);

			// Skip our own position updates
			if (positionData.UserID == _userID) return;

			// Queue the update to happen on the main thread
			CallDeferred(nameof(UpdateRemotePlayerPosition),
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

	/// <summary>
	/// Updates or spawns a remote player at the given position
	/// </summary>
	private void UpdateRemotePlayerPosition(byte userId, float x, float y, float z, float rotY)
	{
		// Don't update the local player through this method
		if (userId == _userID) return;

		if (!_otherPlayers.TryGetValue(userId, out var playerNode))
		{
			// Create new player instance
			playerNode = _playerScene.Instantiate<CharacterBody3D>();
			// Set initial position with y = 1 only for newly created players
			playerNode.Position = new Vector3(x, 1, z);
			_playerContainer.AddChild(playerNode);
			_otherPlayers[userId] = playerNode;

			// Set a different color to distinguish players
			MeshInstance3D mesh = playerNode.GetNodeOrNull<MeshInstance3D>("Mesh");
			if (mesh != null)
			{
				StandardMaterial3D material = new StandardMaterial3D();
				// Generate a unique color based on user ID
				material.AlbedoColor = new Color(
					(userId * 50) % 255 / 255f,
					(userId * 120) % 255 / 255f,
					(userId * 200) % 255 / 255f);
				mesh.MaterialOverride = material;
			}

			Log($"Remote player joined with ID: {userId}", LogLevel.Info);
		}
		else
		{
			// For existing players, use the Y value from the network update
			playerNode.Position = new Vector3(x, y, z);
			playerNode.Rotation = new Vector3(0, rotY, 0);
		}
	}

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

					ProcessPacket(data);
				}
				await Task.Delay(1); // Prevent CPU overuse
			}
			catch (Exception e)
			{
				Log($"Receive loop error: {e}", LogLevel.Error);
				await Task.Delay(1000); // Longer delay after error
			}
		}

		Log("Receive loop ended", LogLevel.Info);
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

	private void UpdateRttStatistics(double newRttMs)
	{
		_rttSamples++;
		_averageRtt = (_averageRtt * (_rttSamples - 1) + newRttMs) / _rttSamples;

		CallDeferred(nameof(UpdateRttDisplay), newRttMs, _averageRtt);

		if (_userIDsToLog.Contains(_userID))
		{
			Log($"RTT [{_userID}]: {newRttMs:F2}ms | Avg: {_averageRtt:F2}ms", LogLevel.Info);
		}
	}

	private void UpdateRttDisplay(double rttMs, double avgRtt)
	{
		if (_rttLabel != null)
		{
			_rttLabel.Text = $"RTT: {rttMs:F2}ms | Avg: {avgRtt:F2}ms | UserID: {_userID}";
		}
	}

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

		// Clean up network resources
		_udpClient?.Close();
		_receiveClient?.Close();

		// Clean up players
		foreach (var player in _otherPlayers.Values)
		{
			player.QueueFree();
		}
		_otherPlayers.Clear();

		Log("Network and player cleanup complete", LogLevel.Info);
	}
}
