using Godot;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Data;
using BU = BinaryUtils;
using C = Command;
using System.Net;
using System.Collections.Generic;

public partial class UdpListener : CharacterBody3D
{
    private const int _serverPort = 9000;
    private const int _clientPortReceive = 22222;
    private const int _clientPortSend = 33333;
    private const string _serverAddress = "127.0.0.1";
    private const int _tickMs = 1;

    private IPEndPoint _serverIP;
    private UdpClient _udpClientReceive;
    private UdpClient _udpClientSend;
    private bool _isRunning = false;

    private byte _dummyUserID = 13;
    private CharacterBody3D _player;
    private uint _sequenceNumber = 0;
    private Dictionary<uint, ulong> _sentPackets = new Dictionary<uint, ulong>();
    private float _averageRtt = 0;
    private int _rttSamples = 0;

    public override void _PhysicsProcess(double delta)
    {
        // Movement handling remains the same
        Vector3 velocity = Velocity;
        Vector3 direction = Vector3.Zero;

        if (Input.IsActionPressed("ui_up")) direction += -Transform.Basis.Z;
        if (Input.IsActionPressed("ui_down")) direction += Transform.Basis.Z;
        if (Input.IsActionPressed("ui_left")) direction += -Transform.Basis.X;
        if (Input.IsActionPressed("ui_right")) direction += Transform.Basis.X;

        if (direction != Vector3.Zero)
        {
            direction = direction.Normalized();
            velocity.X = direction.X * 10;
            velocity.Z = direction.Z * 10;
        }
        else
        {
            velocity.X = Mathf.Lerp(velocity.X, 0f, 0.2f);
            velocity.Z = Mathf.Lerp(velocity.Z, 0f, 0.2f);
        }

        Velocity = velocity;
        MoveAndSlide();
    }

    public override void _Ready()
    {
        _player = GetNode<CharacterBody3D>(".");
        _serverIP = new IPEndPoint(IPAddress.Parse(_serverAddress), _serverPort);

        try
        {
            _udpClientReceive = new UdpClient(new IPEndPoint(IPAddress.Any, _clientPortReceive));
            _udpClientSend = new UdpClient(new IPEndPoint(IPAddress.Any, _clientPortSend));

            _udpClientReceive.Client.ReceiveTimeout = 100;
            _udpClientSend.Client.SendTimeout = 100;

            GD.Print($"Network initialized:");
            GD.Print($"- Receiving on port: {_clientPortReceive}");
            GD.Print($"- Sending from port: {_clientPortSend}");
        }
        catch (Exception e)
        {
            GD.PrintErr($"Initialization failed: {e.Message}");
            return;
        }

        _isRunning = true;
        _ = RunNetworkLoop();
    }

    private async Task RunNetworkLoop()
    {
        while (_isRunning)
        {
            try
            {
                var sendTask = SendPositionUpdate();
                var receiveTask = ReceiveData();
                await Task.WhenAll(sendTask, receiveTask);
            }
            catch (Exception e)
            {
                GD.PrintErr($"Network error: {e.Message}");
            }

            await Task.Delay(_tickMs);
        }
    }

    private async Task ReceiveData()
    {
        try
        {
            // var receiveTask = _udpClientReceive.ReceiveAsync();
            // var timeoutTask = Task.Delay(1);

            // if (await Task.WhenAny(receiveTask, timeoutTask) == receiveTask)
            // {
            //     var result = receiveTask.Result;
            //     var byteArray = result.Buffer;
            //     var command = BU.BinaryUtils.GetCommand(in byteArray);

            //     if (command == C.Command.DEFAULT_RTT)
            //     {
            //         HandleRttResponse(byteArray);
            //     }
            // }

            var result = await _udpClientReceive.ReceiveAsync();

            var byteArray = result.Buffer;
            var command = BU.BinaryUtils.GetCommand(in byteArray);

            if (command == C.Command.DEFAULT_RTT)
            {
                HandleRttResponse(byteArray);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Receive error: {e.Message}");
        }
    }

    private void HandleRttResponse(byte[] data)
    {
        try
        {
            var response = BU.BinaryUtils.DeserializeDefaultRTT(data);
            if (_sentPackets.TryGetValue(response.TimestampRTT, out ulong sendTime))
            {
                var receiveTime = Time.GetTicksUsec();
                var rttUs = receiveTime - sendTime;
                var rttMs = rttUs / 1000f;

                _rttSamples++;
                _averageRtt = (_averageRtt * (_rttSamples - 1) + rttMs) / _rttSamples;

                GD.Print($"RTT: {rttMs:F2} ms (Seq: {response.TimestampRTT}) | Avg: {_averageRtt:F2} ms | Q: {_udpClientReceive.Available}");

                _sentPackets.Remove(response.TimestampRTT);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"RTT processing error: {e.Message}");
        }
    }

    private async Task SendPositionUpdate()
    {
        try
        {
            var sequence = _sequenceNumber++;
            var positionData = new PositionDataRTT()
            {
                CommandID = C.Command.POSITION_RTT,
                UserID = _dummyUserID,
                X = _player.Position.X,
                Y = _player.Position.Y,
                Z = _player.Position.Z,
                RotY = _player.Rotation.Y,
                TimestampRTT = sequence
            };

            _sentPackets[sequence] = Time.GetTicksUsec();

            var byteArray = BU.BinaryUtils.SerializePositionDataRTT(positionData);
            await _udpClientSend.SendAsync(byteArray, byteArray.Length, _serverIP);
        }
        catch (Exception e)
        {
            GD.PrintErr($"Send error: {e.Message}");
        }
    }

    public override void _ExitTree()
    {
        _isRunning = false;
        try
        {
            _udpClientReceive?.Close();
            _udpClientSend?.Close();
            GD.Print("Network resources cleaned up");
        }
        catch (Exception e)
        {
            GD.PrintErr($"Cleanup error: {e.Message}");
        }
    }
}