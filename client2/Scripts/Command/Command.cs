namespace Command;

public enum Command : byte
{
    POSITION = 0,
    MOVE = 1,
    POSITION_RTT = 2,
    MOVE_RTT = 3,
    DEFAULT_RTT = 4,
    USER_ASSIGNMENT = 5,
    PORT_REQUEST = 6,     // Changed to match server (was 7)
    PORT_ASSIGNMENT = 7,  // Changed to match server (was 6)
}
