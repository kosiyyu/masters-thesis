namespace Command;

public enum Command : byte
{
    POSITION = 0,
    MOVE = 1,
    POSITION_RTT = 2,
    MOVE_RTT = 3,
    DEFAULT_RTT = 4
}
