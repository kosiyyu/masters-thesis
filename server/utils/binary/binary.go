package binary

import (
	"bytes"
	"encoding/binary"
	"errors"
	"fmt"
	"server/utils/command"
	"server/utils/data"
	"server/utils/direction"
	"strconv"
)

//region get type

func ConvUint8(reader *bytes.Reader) (uint8, error) {
	var num uint8
	err := binary.Read(reader, binary.LittleEndian, &num)
	if err != nil {
		return 0, errors.New("error during converting uint8,")
	}
	return num, nil
}

func convUint32(reader *bytes.Reader) (uint32, error) {
	var num uint32
	err := binary.Read(reader, binary.LittleEndian, &num)
	if err != nil {
		return 0, errors.New("error during converting uint32,")
	}
	return num, nil
}

func ConvFloat32(reader *bytes.Reader) (float32, error) {
	var num float32
	err := binary.Read(reader, binary.LittleEndian, &num)
	if err != nil {
		return 0, errors.New("error during converting float32")
	}
	return num, nil
}

func convUint8(reader *bytes.Reader) (uint8, error) {
	var num uint8
	err := binary.Read(reader, binary.LittleEndian, &num)
	if err != nil {
		return 0, errors.New("error during converting uint8,")
	}
	return num, nil
}

func convFloat32(reader *bytes.Reader) (float32, error) {
	var num float32
	err := binary.Read(reader, binary.LittleEndian, &num)
	if err != nil {
		return 0, errors.New("error during converting float32")
	}
	return num, nil
}

//endregion

//region get complex type

func GetCommand(byteArray []byte) (command.Command, error) {
	var reader = bytes.NewReader(byteArray)
	var value command.Command
	err := binary.Read(reader, binary.LittleEndian, &value)
	if err != nil {
		return 0, errors.New("error during converting Command,")
	}
	return value, nil
}

func GetDirection(byteArray []byte) (direction.Direction, error) {
	var reader = bytes.NewReader(byteArray)
	var value direction.Direction
	err := binary.Read(reader, binary.LittleEndian, &value)
	if err != nil {
		return 0, errors.New("error during converting Direction,")
	}
	return value, nil
}

//endregion

// region PostitionData

func DeserializePostitionData(reader *bytes.Reader) (data.PositionData, error) {
	const size int = 18 // (1 + 1 + 4 + 4 + 4 + 4) bytes

	if reader.Len() < size {
		return data.PositionData{}, errors.New("byte array is too short, expected " + strconv.Itoa(size) + " bytes, received " + strconv.Itoa(reader.Len()) + " bytes")
	}

	commandID, err := convUint8(reader)
	if err != nil {
		return data.PositionData{}, err
	}

	userID, err := convUint8(reader)
	if err != nil {
		return data.PositionData{}, err
	}

	x, err := convFloat32(reader)
	if err != nil {
		return data.PositionData{}, err
	}

	y, err := convFloat32(reader)
	if err != nil {
		return data.PositionData{}, err
	}

	z, err := convFloat32(reader)
	if err != nil {
		return data.PositionData{}, err
	}

	rotY, err := convFloat32(reader)
	if err != nil {
		return data.PositionData{}, err
	}

	return data.PositionData{
		CommandID: command.Command(commandID),
		UserID:    userID,
		X:         x,
		Y:         y,
		Z:         z,
		RotY:      rotY,
	}, nil
}

func SerializePositionData(positionData data.PositionData) ([]byte, error) {
	buf := new(bytes.Buffer)

	if err := binary.Write(buf, binary.LittleEndian, positionData.CommandID); err != nil {
		return nil, err
	}

	if err := binary.Write(buf, binary.LittleEndian, positionData.UserID); err != nil {
		return nil, err
	}

	if err := binary.Write(buf, binary.LittleEndian, positionData.X); err != nil {
		return nil, err
	}

	if err := binary.Write(buf, binary.LittleEndian, positionData.Y); err != nil {
		return nil, err
	}

	if err := binary.Write(buf, binary.LittleEndian, positionData.Z); err != nil {
		return nil, err
	}

	if err := binary.Write(buf, binary.LittleEndian, positionData.RotY); err != nil {
		return nil, err
	}

	return buf.Bytes(), nil
}

//endregion

// region PostitionDataRTT

func DeserializePostitionDataRTT(reader *bytes.Reader) (data.PositionDataRTT, error) {
	const size int = 22 // (1 + 1 + 4 + 4 + 4 + 4 + 4) bytes

	if reader.Len() < size {
		return data.PositionDataRTT{}, errors.New("byte array is too short, expected " + strconv.Itoa(size) + " bytes, received " + strconv.Itoa(reader.Len()) + " bytes")
	}

	commandID, err := convUint8(reader)
	if err != nil {
		return data.PositionDataRTT{}, err
	}

	userID, err := convUint8(reader)
	if err != nil {
		return data.PositionDataRTT{}, err
	}

	x, err := convFloat32(reader)
	if err != nil {
		return data.PositionDataRTT{}, err
	}

	y, err := convFloat32(reader)
	if err != nil {
		return data.PositionDataRTT{}, err
	}

	z, err := convFloat32(reader)
	if err != nil {
		return data.PositionDataRTT{}, err
	}

	rotY, err := convFloat32(reader)
	if err != nil {
		return data.PositionDataRTT{}, err
	}

	timestampRTT, err := convUint32(reader)
	if err != nil {
		return data.PositionDataRTT{}, err
	}

	return data.PositionDataRTT{
		CommandID:    command.Command(commandID),
		UserID:       userID,
		X:            x,
		Y:            y,
		Z:            z,
		RotY:         rotY,
		TimestampRTT: timestampRTT,
	}, nil
}

func SerializePositionDataRTT(positionData data.PositionDataRTT) ([]byte, error) {
	buf := new(bytes.Buffer)

	if err := binary.Write(buf, binary.LittleEndian, positionData.CommandID); err != nil {
		return nil, err
	}

	if err := binary.Write(buf, binary.LittleEndian, positionData.UserID); err != nil {
		return nil, err
	}

	if err := binary.Write(buf, binary.LittleEndian, positionData.X); err != nil {
		return nil, err
	}

	if err := binary.Write(buf, binary.LittleEndian, positionData.Y); err != nil {
		return nil, err
	}

	if err := binary.Write(buf, binary.LittleEndian, positionData.Z); err != nil {
		return nil, err
	}

	if err := binary.Write(buf, binary.LittleEndian, positionData.RotY); err != nil {
		return nil, err
	}

	if err := binary.Write(buf, binary.LittleEndian, positionData.TimestampRTT); err != nil {
		return nil, err
	}

	return buf.Bytes(), nil
}

//endregion

// region MoveData

func DeserializeMoveData(reader *bytes.Reader) (data.MoveData, error) {
	const size int = 7 // (1 + 1 + 1 + 4) bytes

	if reader.Len() < size {
		return data.MoveData{}, errors.New("byte array is too short, expected " + strconv.Itoa(size) + " bytes, received " + strconv.Itoa(reader.Len()) + " bytes")
	}

	commandID, err := convUint8(reader)
	if err != nil {
		return data.MoveData{}, err
	}

	userID, err := convUint8(reader)
	if err != nil {
		return data.MoveData{}, err
	}

	directionID, err := convUint8(reader)
	if err != nil {
		return data.MoveData{}, err
	}

	speed, err := convFloat32(reader)
	if err != nil {
		return data.MoveData{}, err
	}

	return data.MoveData{
		CommandID:   command.Command(commandID),
		UserID:      userID,
		DirectionID: direction.Direction((directionID)),
		Speed:       speed,
	}, nil
}

//endregion

// region MoveDataRTT

func DeserializeMoveDataRTT(reader *bytes.Reader) (data.MoveDataRTT, error) {
	const size int = 7 // (1 + 1 + 1 + 4) bytes
	if reader.Len() < size {
		return data.MoveDataRTT{}, errors.New("byte array is too short, expected " + strconv.Itoa(size) + " bytes, received " + strconv.Itoa(reader.Len()) + " bytes")
	}

	commandID, err := convUint8(reader)
	if err != nil {
		return data.MoveDataRTT{}, err
	}

	userID, err := convUint8(reader)
	if err != nil {
		return data.MoveDataRTT{}, err
	}

	directionID, err := convUint8(reader)
	if err != nil {
		return data.MoveDataRTT{}, err
	}

	speed, err := convFloat32(reader)
	if err != nil {
		return data.MoveDataRTT{}, err
	}

	timestampRTT, err := convUint32(reader)
	if err != nil {
		return data.MoveDataRTT{}, err
	}

	return data.MoveDataRTT{
		CommandID:    command.Command(commandID),
		UserID:       userID,
		DirectionID:  direction.Direction((directionID)),
		Speed:        speed,
		TimestampRTT: timestampRTT,
	}, nil
}

//endregion

//region DefaultRTT

func SerializeDefaultRTT(defaultRTT data.DefaultRTT) ([]byte, error) {
	buf := new(bytes.Buffer)

	if err := binary.Write(buf, binary.LittleEndian, defaultRTT.CommandID); err != nil {
		return nil, fmt.Errorf("failed to write CommandID: %v", err)
	}

	timestampBytes := make([]byte, 4)
	binary.LittleEndian.PutUint32(timestampBytes, defaultRTT.TimestampRTT)
	if _, err := buf.Write(timestampBytes); err != nil {
		return nil, fmt.Errorf("failed to write TimestampRTT: %v", err)
	}

	return buf.Bytes(), nil
}

//endregion

//region UserAssignment

func DeserializeUserAssignment(reader *bytes.Reader) (data.UserAssignment, error) {
	const size = 2 // 1 + 1 bytes
	if reader.Len() < size {
		return data.UserAssignment{}, fmt.Errorf("expected %d bytes, got %d", size, reader.Len())
	}

	commandID, err := convUint8(reader)
	if err != nil {
		return data.UserAssignment{}, err
	}

	userID, err := convUint8(reader)
	if err != nil {
		return data.UserAssignment{}, err
	}

	return data.UserAssignment{
		CommandID: command.Command(commandID),
		UserID:    userID,
	}, nil
}

func SerializeUserAssignment(ua data.UserAssignment) ([]byte, error) {
	buf := new(bytes.Buffer)
	if err := binary.Write(buf, binary.LittleEndian, ua.CommandID); err != nil {
		return nil, err
	}
	if err := binary.Write(buf, binary.LittleEndian, ua.UserID); err != nil {
		return nil, err
	}
	return buf.Bytes(), nil
}

//endregion

//region PortAssignment

func DeserializePortAssignment(reader *bytes.Reader) (data.PortAssignment, error) {
	const size = 4 // 1 + 1 + 2 bytes
	if reader.Len() < size {
		return data.PortAssignment{}, fmt.Errorf("expected %d bytes, got %d", size, reader.Len())
	}

	commandID, err := convUint8(reader)
	if err != nil {
		return data.PortAssignment{}, err
	}

	userID, err := convUint8(reader)
	if err != nil {
		return data.PortAssignment{}, err
	}

	var portBytes [2]byte
	_, err = reader.Read(portBytes[:])
	if err != nil {
		return data.PortAssignment{}, err
	}

	// Convert to uint16 using LittleEndian
	port := binary.LittleEndian.Uint16(portBytes[:])

	return data.PortAssignment{
		CommandID: command.Command(commandID),
		UserID:    userID,
		Port:      port,
	}, nil
}

func SerializePortAssignment(pa data.PortAssignment) ([]byte, error) {
	buf := new(bytes.Buffer)
	if err := binary.Write(buf, binary.LittleEndian, pa.CommandID); err != nil {
		return nil, err
	}
	if err := binary.Write(buf, binary.LittleEndian, pa.UserID); err != nil {
		return nil, err
	}
	if err := binary.Write(buf, binary.LittleEndian, pa.Port); err != nil {
		return nil, err
	}
	return buf.Bytes(), nil
}

//endregion
