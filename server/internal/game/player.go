package game

import (
	"net"
	"server/internal/message"
	"time"
)

// Player represents a connected game client
type Player struct {
	ID         uint8
	Address    *net.UDPAddr
	ListenPort int
	LastSeen   time.Time
	Position   message.PositionDataRTT
}

// NewPlayer creates a new player instance
func NewPlayer(id uint8, addr *net.UDPAddr, port int) *Player {
	return &Player{
		ID:         id,
		Address:    addr,
		ListenPort: port,
		LastSeen:   time.Now(),
		Position: message.PositionDataRTT{
			UserID: id,
		},
	}
}

// UpdatePosition updates the player's position and last seen time
func (p *Player) UpdatePosition(pos message.PositionDataRTT) {
	p.Position = pos
	p.LastSeen = time.Now()
}

// IsActive checks if the player has been active within the timeout period
func (p *Player) IsActive(timeout time.Duration) bool {
	return time.Since(p.LastSeen) <= timeout
}

// GetListenAddress returns the address where this player listens for updates
func (p *Player) GetListenAddress() *net.UDPAddr {
	return &net.UDPAddr{
		IP:   p.Address.IP,
		Port: p.ListenPort,
	}
}
