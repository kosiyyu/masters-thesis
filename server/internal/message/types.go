package message

import (
	"fmt"
	"server/internal/command"
	"server/pkg/direction"
)

// PositionData represents a player's position in 3D space
type PositionData struct {
	CommandID command.Command
	UserID    uint8
	X, Y, Z   float32
	RotY      float32
}

// PositionDataRTT extends PositionData with round-trip time data
type PositionDataRTT struct {
	CommandID    command.Command
	UserID       uint8
	X, Y, Z      float32
	RotY         float32
	TimestampRTT uint32
}

// MoveData represents player movement data
type MoveData struct {
	CommandID   command.Command
	UserID      uint8
	DirectionID direction.Direction
	Speed       float32
}

// MoveDataRTT extends MoveData with round-trip time data
type MoveDataRTT struct {
	CommandID    command.Command
	UserID       uint8
	DirectionID  direction.Direction
	Speed        float32
	TimestampRTT uint32
}

// DefaultRTT is sent back to clients for latency calculation
type DefaultRTT struct {
	CommandID    command.Command
	TimestampRTT uint32
}

// UserAssignment tells a client their assigned user ID
type UserAssignment struct {
	CommandID command.Command
	UserID    uint8
}

// PortAssignment tells a client their assigned port for receiving updates
type PortAssignment struct {
	CommandID command.Command
	UserID    uint8
	Port      uint16
}

// Print methods for debugging
func (p PositionData) String() string {
	return fmt.Sprintf("PositionData{UserID: %d, X: %.2f, Y: %.2f, Z: %.2f, RotY: %.2f}",
		p.UserID, p.X, p.Y, p.Z, p.RotY)
}

func (p PositionDataRTT) String() string {
	return fmt.Sprintf("PositionDataRTT{UserID: %d, X: %.2f, Y: %.2f, Z: %.2f, RotY: %.2f, RTT: %d}",
		p.UserID, p.X, p.Y, p.Z, p.RotY, p.TimestampRTT)
}

func (m MoveData) String() string {
	return fmt.Sprintf("MoveData{UserID: %d, Direction: %s, Speed: %.2f}",
		m.UserID, m.DirectionID, m.Speed)
}

func (m MoveDataRTT) String() string {
	return fmt.Sprintf("MoveDataRTT{UserID: %d, Direction: %s, Speed: %.2f, RTT: %d}",
		m.UserID, m.DirectionID, m.Speed, m.TimestampRTT)
}
