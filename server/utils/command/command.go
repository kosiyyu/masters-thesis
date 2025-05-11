package command

type Command uint8

const (
	POSITION        Command = iota // 0
	MOVE                           // 1
	POSITION_RTT                   // 2
	MOVE_RTT                       // 3
	DEFAULT_RTT                    // 4
	USER_ASSIGNMENT                // 5
	PORT_REQUEST                   // 6
	PORT_ASSIGNMENT                // 7
)
