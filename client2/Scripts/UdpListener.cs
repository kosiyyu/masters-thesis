using C = Command;
using Godot;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

public partial class UdpListener<T> : Node
{
    private const int _receivePort = 55555;
    private const int _tickMs = 10;
    private bool _isListening = false;
    private UdpClient _udpClient;

    public override void _Ready()
    {
        _udpClient = new UdpClient(_receivePort);
        _isListening = true;
        _startListening();
    }

    private void _sendData(T data, string ipAddress, int port)
    {
        try
        {
            using (var sendClient = new UdpClient())
            {
                byte[] byteData = _convertToByteArray(data);

                IPEndPoint endPoint = new IPEndPoint(IPAddress.Parse(ipAddress), port);
                sendClient.Send(byteData, byteData.Length, endPoint);
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"Error sending data: {e.Message}");
        }
    }

    private byte[] _convertToByteArray(T data)
    {
        throw new NotImplementedException("You must override ConvertToByteArray in a derived class");
    }

    private T _convertFromByteArray(byte[] byteArray)
    {
        throw new NotImplementedException("You must override ConvertFromByteArray in a derived class");
    }

    private void _receiveData(byte[] byteArray)
    {
        if (byteArray.Length < 1)
        {
            throw new ArgumentException("ByteArray length is lesser that 1");
        }

        if (byteArray[0] == (byte)C.Command.MOVE)
        {
            // MOVE LOGIC
        }

        if (byteArray[0] == (byte)C.Command.POSITION)
        {
            // MOVE POSITION
        }

    }


    private async void _startListening()
    {
        while (_isListening)
        {
            try
            {
                if (_udpClient.Available > 0)
                {
                    UdpReceiveResult result = await _udpClient.ReceiveAsync();
                    _receiveData(result.Buffer);
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
}
