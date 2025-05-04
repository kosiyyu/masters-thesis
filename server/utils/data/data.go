package data

import (
	"fmt"
	"server/utils/command"
	"server/utils/direction"
)

//region structs

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
	CommandID   command.Command
	UserID      uint8
	DirectionID direction.Direction
	Speed       float32
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

//endregion

//region print

func (p PositionData) Print() {
	fmt.Printf("PositionData{CommandID: %v, UserID: %v, X: %v, Y: %v, Z: %v, RotY: %v}\n",
		p.CommandID, p.UserID, p.X, p.Y, p.Z, p.RotY)
}

func (p PositionDataRTT) Print() {
	fmt.Printf("PositionDataRTT{CommandID: %v, UserID: %v, X: %v, Y: %v, Z: %v, RotY: %v, TimestampRTT: %v}\n",
		p.CommandID, p.UserID, p.X, p.Y, p.Z, p.RotY, p.TimestampRTT)
}

func (m MoveData) Print() {
	fmt.Printf("MoveData{CommandID: %v, UserID: %v, DirectionID: %v, Speed: %v}\n",
		m.CommandID, m.UserID, m.DirectionID, m.Speed)
}

func (m MoveDataRTT) Print() {
	fmt.Printf("MoveDataRTT{CommandID: %v, UserID: %v, DirectionID: %v, Speed: %v, TimestampRTT: %v}\n",
		m.CommandID, m.UserID, m.DirectionID, m.Speed, m.TimestampRTT)
}

func (d DefaultRTT) Print() {
	fmt.Printf("DefaultRTT{CommandID: %v, TimestampRTT: %v}\n", d.CommandID, d.TimestampRTT)
}

//endregion
