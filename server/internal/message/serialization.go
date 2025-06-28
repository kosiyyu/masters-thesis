package message

import (
	"bytes"
	"encoding/binary"
	"errors"
	"fmt"
	"server/internal/command"
	"server/pkg/direction"
)

// Serializer handles all message serialization/deserialization
type Serializer struct{}

func NewSerializer() *Serializer {
	return &Serializer{}
}

// Deserialize parses incoming byte data into appropriate message types
func (s *Serializer) Deserialize(data []byte) (interface{}, command.Command, error) {
	if len(data) == 0 {
		return nil, 0, errors.New("empty data")
	}

	cmd := command.Command(data[0])
	reader := bytes.NewReader(data)

	switch cmd {
	case command.POSITION:
		return s.deserializePositionData(reader)
	case command.POSITION_RTT:
		return s.deserializePositionDataRTT(reader)
	case command.MOVE:
		return s.deserializeMoveData(reader)
	case command.MOVE_RTT:
		return s.deserializeMoveDataRTT(reader)
	case command.USER_ASSIGNMENT:
		return s.deserializeUserAssignment(reader)
	case command.PORT_ASSIGNMENT:
		return s.deserializePortAssignment(reader)
	default:
		return nil, cmd, fmt.Errorf("unknown command: %d", cmd)
	}
}

// PositionData serialization
func (s *Serializer) SerializePositionData(pos PositionData) ([]byte, error) {
	buf := new(bytes.Buffer)
	fields := []interface{}{pos.CommandID, pos.UserID, pos.X, pos.Y, pos.Z, pos.RotY}

	for _, field := range fields {
		if err := binary.Write(buf, binary.LittleEndian, field); err != nil {
			return nil, err
		}
	}
	return buf.Bytes(), nil
}

func (s *Serializer) deserializePositionData(reader *bytes.Reader) (PositionData, command.Command, error) {
	if reader.Len() < 18 { // 1+1+4+4+4+4
		return PositionData{}, 0, errors.New("insufficient data for PositionData")
	}

	var pos PositionData
	fields := []interface{}{&pos.CommandID, &pos.UserID, &pos.X, &pos.Y, &pos.Z, &pos.RotY}

	for _, field := range fields {
		if err := binary.Read(reader, binary.LittleEndian, field); err != nil {
			return PositionData{}, 0, err
		}
	}
	return pos, pos.CommandID, nil
}

// PositionDataRTT serialization
func (s *Serializer) SerializePositionDataRTT(pos PositionDataRTT) ([]byte, error) {
	buf := new(bytes.Buffer)
	fields := []interface{}{pos.CommandID, pos.UserID, pos.X, pos.Y, pos.Z, pos.RotY, pos.TimestampRTT}

	for _, field := range fields {
		if err := binary.Write(buf, binary.LittleEndian, field); err != nil {
			return nil, err
		}
	}
	return buf.Bytes(), nil
}

func (s *Serializer) deserializePositionDataRTT(reader *bytes.Reader) (PositionDataRTT, command.Command, error) {
	if reader.Len() < 22 { // 1+1+4+4+4+4+4
		return PositionDataRTT{}, 0, errors.New("insufficient data for PositionDataRTT")
	}

	var pos PositionDataRTT
	fields := []interface{}{&pos.CommandID, &pos.UserID, &pos.X, &pos.Y, &pos.Z, &pos.RotY, &pos.TimestampRTT}

	for _, field := range fields {
		if err := binary.Read(reader, binary.LittleEndian, field); err != nil {
			return PositionDataRTT{}, 0, err
		}
	}
	return pos, pos.CommandID, nil
}

// MoveData serialization
func (s *Serializer) deserializeMoveData(reader *bytes.Reader) (MoveData, command.Command, error) {
	if reader.Len() < 7 { // 1+1+1+4
		return MoveData{}, 0, errors.New("insufficient data for MoveData")
	}

	var mov MoveData
	var dirByte uint8
	fields := []interface{}{&mov.CommandID, &mov.UserID, &dirByte, &mov.Speed}

	for _, field := range fields {
		if err := binary.Read(reader, binary.LittleEndian, field); err != nil {
			return MoveData{}, 0, err
		}
	}
	mov.DirectionID = direction.Direction(dirByte)
	return mov, mov.CommandID, nil
}

func (s *Serializer) deserializeMoveDataRTT(reader *bytes.Reader) (MoveDataRTT, command.Command, error) {
	if reader.Len() < 11 { // 1+1+1+4+4
		return MoveDataRTT{}, 0, errors.New("insufficient data for MoveDataRTT")
	}

	var mov MoveDataRTT
	var dirByte uint8
	fields := []interface{}{&mov.CommandID, &mov.UserID, &dirByte, &mov.Speed, &mov.TimestampRTT}

	for _, field := range fields {
		if err := binary.Read(reader, binary.LittleEndian, field); err != nil {
			return MoveDataRTT{}, 0, err
		}
	}
	mov.DirectionID = direction.Direction(dirByte)
	return mov, mov.CommandID, nil
}

// DefaultRTT serialization
func (s *Serializer) SerializeDefaultRTT(rtt DefaultRTT) ([]byte, error) {
	buf := new(bytes.Buffer)
	fields := []interface{}{rtt.CommandID, rtt.TimestampRTT}

	for _, field := range fields {
		if err := binary.Write(buf, binary.LittleEndian, field); err != nil {
			return nil, err
		}
	}
	return buf.Bytes(), nil
}

// UserAssignment serialization
func (s *Serializer) SerializeUserAssignment(ua UserAssignment) ([]byte, error) {
	buf := new(bytes.Buffer)
	fields := []interface{}{ua.CommandID, ua.UserID}

	for _, field := range fields {
		if err := binary.Write(buf, binary.LittleEndian, field); err != nil {
			return nil, err
		}
	}
	return buf.Bytes(), nil
}

func (s *Serializer) deserializeUserAssignment(reader *bytes.Reader) (UserAssignment, command.Command, error) {
	if reader.Len() < 2 {
		return UserAssignment{}, 0, errors.New("insufficient data for UserAssignment")
	}

	var ua UserAssignment
	fields := []interface{}{&ua.CommandID, &ua.UserID}

	for _, field := range fields {
		if err := binary.Read(reader, binary.LittleEndian, field); err != nil {
			return UserAssignment{}, 0, err
		}
	}
	return ua, ua.CommandID, nil
}

// PortAssignment serialization
func (s *Serializer) SerializePortAssignment(pa PortAssignment) ([]byte, error) {
	buf := new(bytes.Buffer)
	fields := []interface{}{pa.CommandID, pa.UserID, pa.Port}

	for _, field := range fields {
		if err := binary.Write(buf, binary.LittleEndian, field); err != nil {
			return nil, err
		}
	}
	return buf.Bytes(), nil
}

func (s *Serializer) deserializePortAssignment(reader *bytes.Reader) (PortAssignment, command.Command, error) {
	if reader.Len() < 4 {
		return PortAssignment{}, 0, errors.New("insufficient data for PortAssignment")
	}

	var pa PortAssignment
	fields := []interface{}{&pa.CommandID, &pa.UserID, &pa.Port}

	for _, field := range fields {
		if err := binary.Read(reader, binary.LittleEndian, field); err != nil {
			return PortAssignment{}, 0, err
		}
	}
	return pa, pa.CommandID, nil
}
