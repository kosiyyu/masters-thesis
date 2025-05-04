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

public partial class UdpListener : CharacterBody3D
{
    private const int _serverPort = 9000;
    private const int _clientPortReceive = 22222;
    private const int _clientPortSend = 33333;
    private const string _serverAddress = "127.0.0.1";
    private const int _targetFPS = 100; // updates per second

    private IPEndPoint _serverIP;
    private UdpClient _udpClientReceive;
    private UdpClient _udpClientSend;
    private bool _isRunning = false;

    private byte _dummyUserID = 13;
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

    private void SendPositionUpdate()
    {
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
            UserID = _dummyUserID,
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
            _udpClientSend.BeginSend(byteArray, byteArray.Length, _serverIP, null, null);
        }
        catch
        {
            // Todo handle send failures
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

    public override void _Ready()
    {
        _player = this;
        InitializeNetwork();
        _isRunning = true;

        // Spawn independent threads
        Task.Run(NetworkSendLoop);
        Task.Run(NetworkReceiveLoop);
    }

    private void InitializeNetwork()
    {
        try
        {
            _serverIP = new IPEndPoint(IPAddress.Parse(_serverAddress), _serverPort);

            _udpClientReceive = new UdpClient(new IPEndPoint(IPAddress.Any, _clientPortReceive))
            {
                Client = { ReceiveTimeout = 0 }
            };

            _udpClientSend = new UdpClient(new IPEndPoint(IPAddress.Any, _clientPortSend))
            {
                Client = { SendTimeout = 0 }
            };

            GD.Print($"Network initialized (Send: {_clientPortSend}, Receive: {_clientPortReceive})");
        }
        catch (Exception e)
        {
            GD.PrintErr($"Initialization failed: {e}");
        }
    }

    private async Task NetworkSendLoop()
    {
        var timer = new PeriodicTimer(TimeSpan.FromSeconds(_targetFrameTime));

        while (_isRunning)
        {
            try
            {
                SendPositionUpdate();
                await timer.WaitForNextTickAsync();
            }
            catch (Exception e)
            {
                GD.PrintErr($"Send error: {e}");
            }
        }
    }

    private async Task NetworkReceiveLoop()
    {
        while (_isRunning)
        {
            try
            {
                var result = await _udpClientReceive.ReceiveAsync().ConfigureAwait(false);
                ProcessPacket(result.Buffer);
            }
            catch (SocketException)
            {
                //
            }
            catch (Exception e)
            {
                GD.PrintErr($"Receive error: {e}");
            }
        }
    }

    private void ProcessPacket(byte[] data)
    {
        try
        {
            var command = BU.BinaryUtils.GetCommand(data);
            if (command == C.Command.DEFAULT_RTT)
            {
                HandleRttResponse(data);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Packet processing error: {e}");
        }
    }

    private void HandleRttResponse(byte[] data)
    {
        var response = BU.BinaryUtils.DeserializeDefaultRTT(data);
        if (!_sentPackets.TryRemove(response.TimestampRTT, out long sendTimestamp)) return;

        var receiveTimestamp = Stopwatch.GetTimestamp();
        var rttSeconds = (receiveTimestamp - sendTimestamp) / (double)Stopwatch.Frequency;

        UpdateRttStatistics(rttSeconds * 1000);
    }

    private void UpdateRttStatistics(double newRttMs)
    {
        _rttSamples++;
        _averageRtt = (_averageRtt * (_rttSamples - 1) + newRttMs) / _rttSamples;

        GD.Print($"RTT: {newRttMs:F2}ms | Avg: {_averageRtt:F2}ms");
    }

    public override void _ExitTree()
    {
        _isRunning = false;
        _udpClientReceive?.Close();
        _udpClientSend?.Close();
        GD.Print("Network shutdown complete");
    }
}