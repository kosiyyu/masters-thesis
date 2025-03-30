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

type MoveData struct {
	CommandID   command.Command
	UserID      uint8
	DirectionID direction.Direction
	Speed       float32
}
