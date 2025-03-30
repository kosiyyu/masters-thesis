package udp

import (
	"bytes"
	"encoding/binary"
	"errors"
	"fmt"
	"net"
	"os"
	"server/utils/command"
	"server/utils/data"
	"server/utils/direction"
	"strconv"
)

type Udp struct {
	conn *net.UDPConn
	addr string
}

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

		_, err = extractData(buffer[:n])
		if err != nil {
			fmt.Println("Error during extracting data from the message:", err)
		}

		fmt.Printf("Received %d bytes from %s\n", n, clientAddr)

		content, err := extractData(buffer[:n])
		if err != nil {
			fmt.Println("Error during extracting data from the message:", err)
		} else {
			// Print the extracted data
			fmt.Printf("Received data: %+v\n", content)
		}
	}
}

func convUint8(reader *bytes.Reader) (uint8, error) {
	var num uint8
	err := binary.Read(reader, binary.BigEndian, &num)
	if err != nil {
		return 0, errors.New("Error during converting uint8.")
	}
	return num, nil
}

func convFloat32(reader *bytes.Reader) (float32, error) {
	var num float32
	err := binary.Read(reader, binary.BigEndian, &num)
	if err != nil {
		return 0, errors.New("Error during converting float32.")
	}
	return num, nil
}

const FixedFieldCount = 2

// This function extaract raw data into well defined structs, CommandID and UserID are always present inside a struct
func extractData(byteArray []byte) (interface{}, error) {
	reader := bytes.NewReader(byteArray)
	if len(byteArray) < FixedFieldCount {
		return nil, errors.New("Byte array need to consit of command ID and user ID.")
	}

	// CommandID is of type Command but it's baslicy uint8, later on it's being converted into a propper type
	commandID, err := convUint8(reader)
	if err != nil {
		return nil, err
	}

	userID, err := convUint8(reader)
	if err != nil {
		return nil, err
	}

	switch command.Command(commandID) {
	case command.POSITION:
		// 4 * float32(4)
		expectedRemainingSize := 16
		n := len(byteArray)
		if n-FixedFieldCount < expectedRemainingSize {
			return nil, errors.New("Byte array is too short, expected " + strconv.Itoa(expectedRemainingSize) + " bytes, received " + strconv.Itoa(n) + " bytes")
		}

		x, err := convFloat32(reader)
		if err != nil {
			return nil, err
		}

		y, err := convFloat32(reader)
		if err != nil {
			return nil, err
		}

		z, err := convFloat32(reader)
		if err != nil {
			return nil, err
		}

		rotY, err := convFloat32(reader)
		if err != nil {
			return nil, err
		}

		return data.PositionData{
			CommandID: command.Command(commandID),
			UserID:    userID,
			X:         x,
			Y:         y,
			Z:         z,
			RotY:      rotY,
		}, nil
	case command.MOVE:
		// uint8(1) + float32(4)
		expectedRemainingSize := 5
		n := len(byteArray)
		if n-FixedFieldCount < expectedRemainingSize {
			return nil, errors.New("Byte array is too short, expected " + strconv.Itoa(expectedRemainingSize) + " bytes, received " + strconv.Itoa(n) + " bytes")
		}

		directionID, err := convUint8(reader)
		if err != nil {
			return nil, err
		}

		speed, err := convFloat32(reader)
		if err != nil {
			return nil, err
		}

		return data.MoveData{
			CommandID:   command.Command(commandID),
			UserID:      userID,
			DirectionID: direction.Direction(directionID),
			Speed:       speed,
		}, nil
	default:
		return nil, errors.New("Unknown data")
	}
}
