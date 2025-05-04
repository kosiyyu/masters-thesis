package udp

import (
	"bytes"
	"errors"
	"fmt"
	"net"
	"os"
	b "server/utils/binary"
	"server/utils/command"
	"server/utils/data"
	"time"
)

type Udp struct {
	conn *net.UDPConn
	addr string
	tick time.Duration
}

const TICK_CONSTANT_MS = 1

func NewUdp(addr string) *Udp {
	if len(os.Args) > 1 {
		addr = os.Args[1]
	}

	udpAddr, err := net.ResolveUDPAddr("udp", addr)
	if err != nil {
		fmt.Println("Error resolving address:", err)
		os.Exit(1)
	}

	conn, err := net.ListenUDP("udp", udpAddr)
	if err != nil {
		fmt.Println("Error listening:", err)
		defer conn.Close()
		os.Exit(1)
	}

	return &Udp{
		conn: conn,
		addr: addr,
		tick: TICK_CONSTANT_MS * time.Millisecond,
	}
}

func (u *Udp) Run() {
	buffer := make([]byte, 1024)

	fmt.Printf("UDP server listening on %s\n", u.addr)
	for {
		n, clientAddr, err := u.conn.ReadFromUDP(buffer)
		if err != nil {
			fmt.Println("Error reading from UDP:", err)
			continue
		}

		handleData(u.conn, clientAddr, n, buffer[:n])

		time.Sleep(u.tick)
	}
}

func handleData(conn *net.UDPConn, clientAddr *net.UDPAddr, n int, byteArray []byte) {
	value, commandID, err := deserializeData(byteArray)
	if err != nil {

	}

	switch commandID {
	case command.POSITION:
		positionData, ok := value.(data.PositionData)
		if !ok {
			fmt.Printf("Error during receiving %v data. Received %d bytes from %s\n", commandID, n, clientAddr)
		}
		data.PositionData.Print(positionData)
	case command.POSITION_RTT:
		positionDataRTT, ok := value.(data.PositionDataRTT)
		if !ok {
			fmt.Printf("Error during receiving %v data.", commandID)
		}
		data.PositionDataRTT.Print(positionDataRTT)
		sendRTTResponse(conn, clientAddr, positionDataRTT.TimestampRTT)
	case command.MOVE:
		moveData, ok := value.(data.MoveData)
		if !ok {
			fmt.Printf("Error during receiving %v data.", commandID)
		}
		data.MoveData.Print(moveData)
	case command.MOVE_RTT:
		moveDataRTT, ok := value.(data.MoveDataRTT)
		if !ok {
			fmt.Printf("Error during receiving %v data.", commandID)
		}
		data.MoveDataRTT.Print(moveDataRTT)
		sendRTTResponse(conn, clientAddr, moveDataRTT.TimestampRTT)
	}
}

func sendRTTResponse(conn *net.UDPConn, clientAddr *net.UDPAddr, timestampRTT uint32) {
	// Create response with client's RECEIVE port (22222)
	responseAddr := &net.UDPAddr{
		IP:   clientAddr.IP,
		Port: 22222, // Explicitly send to client's receive port
	}

	defaultRTT := data.DefaultRTT{
		CommandID:    command.DEFAULT_RTT,
		TimestampRTT: timestampRTT,
	}

	byteArray, err := b.SerializeDefaultRTT(defaultRTT)
	if err != nil {
		fmt.Println("Serialization error:", err)
		return
	}

	_, err = conn.WriteToUDP(byteArray, responseAddr)
	if err != nil {
		fmt.Println("Send error:", err)
		return
	}

	// fmt.Printf("Sent RTT response to %s:%d (Timestamp: %d)\n", responseAddr.IP, responseAddr.Port, timestampRTT)
}

// This function deserialize raw data into well defined structs, CommandID and UserID are always present inside a struct
func deserializeData(byteArray []byte) (interface{}, command.Command, error) {
	reader := bytes.NewReader(byteArray)

	var commandID, err = b.GetCommand(byteArray)
	if err != nil {
		return nil, 0, err // NIL or NULL must be added to enum
	}

	switch command.Command(commandID) {
	case command.POSITION:
		value, err := b.DeserializePostitionData(reader)
		return value, commandID, err
	case command.MOVE:
		value, err := b.DeserializeMoveData(reader)
		return value, commandID, err
	case command.POSITION_RTT:
		value, err := b.DeserializePostitionDataRTT(reader)
		return value, commandID, err
	case command.MOVE_RTT:
		value, err := b.DeserializeMoveDataRTT(reader)
		return value, commandID, err
	default:
		return nil, 0, errors.New("unknown data") // NIL or NULL must be added to enum
	}
}
