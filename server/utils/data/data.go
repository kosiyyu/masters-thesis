package data

import (
	"server/utils/command"
	"server/utils/direction"
)

type PositionData struct {
	CommandID command.Command
	UserID    uint8
	X         float32
	Y         float32
	Z         float32
	RotY      float32
}

type PositionDataRTT struct {
	CommandID    command.Command
	UserID       uint8
	X            float32
	Y            float32
	Z            float32
	RotY         float32
	TimestampRTT uint32
}

type MoveData struct {
	CommandID    command.Command
	UserID       uint8
	DirectionID  direction.Direction
	Speed        float32
	TimestampRTT uint32
}

type MoveDataRTT struct {
	CommandID    command.Command
	UserID       uint8
	DirectionID  direction.Direction
	Speed        float32
	TimestampRTT uint32
}

type DefaultRTT struct {
	CommandID    command.Command
	TimestampRTT uint32
}
