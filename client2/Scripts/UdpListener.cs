using Godot;
using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using Data;
using BU = BinaryUtils;
using C = Command;
using System.Net;

public partial class UdpListener : CharacterBody3D
{
    private const int _serverPort = 9000;
    private const int _clientPortReceive = 22222;
    private const int _clientPortSend = 33333;
    private const string _serverAddress = "127.0.0.1";
    private const int _tickMs = 10;
    private bool _isRunning = false;

    private IPEndPoint _serverIP;
    private UdpClient _udpClientReceive;
    private UdpClient _udpClientSend;

    private byte _dummyUserID = 13;
    private CharacterBody3D _player;

    public override void _PhysicsProcess(double delta)
    {
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
        _udpClientReceive = new UdpClient(_clientPortReceive);
        _udpClientSend = new UdpClient(_clientPortSend);
        _isRunning = true;
        _run();
    }

    private async Task _receiveData()
    {
        UdpReceiveResult result = await _udpClientReceive.ReceiveAsync();
        if (result.Buffer is null)
        {
            GD.PushError($"Received buffer is null.");
            return;
        }

        var byteArray = result.Buffer;
        var command = BU.BinaryUtils.GetCommand(in byteArray);
        switch (command)
        {
            case C.Command.DEFAULT_RTT:
                var defaultRTT = BU.BinaryUtils.DeserializeDefaultRTT(result.Buffer);
                GD.Print($"{command} | Received: {defaultRTT}");
                break;
            case C.Command.MOVE:
                var moveData = BU.BinaryUtils.DeserializeMoveData(result.Buffer);
                GD.Print($"{command} | Received: {moveData}");
                break;
            case C.Command.POSITION:
                var positionData = BU.BinaryUtils.DeserializePositionData(result.Buffer);
                GD.Print($"{command} | Received: {positionData}");
                break;
            case C.Command.MOVE_RTT:
                var moveDataRTT = BU.BinaryUtils.DeserializeMoveDataRTT(result.Buffer);
                GD.Print($"{command} | Received: {moveDataRTT}");
                break;
            case C.Command.POSITION_RTT:
                var positionDataRTT = BU.BinaryUtils.DeserializePositionDataRTT(result.Buffer);
                GD.Print($"{command} | Received: {positionDataRTT}");
                break;
            default:
                GD.PushError("Unknown command.");
                break;
        }
    }

    private async Task _sendData()
    {
        var positionDataRTT = new PositionDataRTT()
        {
            CommandID = C.Command.POSITION,
            UserID = _dummyUserID,
            X = _player.Position.X,
            Y = _player.Position.Y,
            Z = _player.Position.Z,
            RotY = _player.Rotation.Y,
            TimestampRTT = (uint)Time.GetTicksUsec()
        };
        var byteArray = BU.BinaryUtils.SerializePositionDataRTT(positionDataRTT);

        await _udpClientSend.SendAsync(byteArray, byteArray.Length, _serverIP);

        GD.Print($"Sent data: {positionDataRTT}");
    }

    private async void _run()
    {
        while (_isRunning)
        {
            try
            {
                await _sendData();

                if (_udpClientReceive.Available > 0)
                {
                    await _receiveData();
                }
            }
            catch (ObjectDisposedException e)
            {
                GD.PrintErr($"Error 0 receiving data: {e.Message}");
                break;
            }
            catch (Exception e)
            {
                GD.PrintErr($"Error 1 receiving data: {e.Message}");
            }

            await Task.Delay(_tickMs);
        }
    }

    public override void _ExitTree()
    {
        _isRunning = false;
        _udpClientReceive?.Close();
        _udpClientSend?.Close();
    }
}
