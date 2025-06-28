package server

import (
	"fmt"
	"net"
	"server/internal/game"
	"server/internal/message"
	"sync"
	"time"
)

// ClientManager handles all connected clients and their state
type ClientManager struct {
	players     map[uint8]*game.Player
	clientAddrs map[string]uint8 // IP:Port -> UserID mapping
	portManager *PortManager
	serializer  *message.Serializer
	nextUserID  uint8
	mu          sync.RWMutex
}

// NewClientManager creates a new client manager
func NewClientManager(minPort, maxPort int) *ClientManager {
	return &ClientManager{
		players:     make(map[uint8]*game.Player),
		clientAddrs: make(map[string]uint8),
		portManager: NewPortManager(minPort, maxPort),
		serializer:  message.NewSerializer(),
		nextUserID:  1,
	}
}

// RegisterClient registers a new client and assigns them a user ID and port
func (cm *ClientManager) RegisterClient(addr *net.UDPAddr) (*game.Player, error) {
	cm.mu.Lock()
	defer cm.mu.Unlock()

	key := addr.String()
	if userID, exists := cm.clientAddrs[key]; exists {
		// Client already registered
		return cm.players[userID], nil
	}

	port, err := cm.portManager.AllocatePort()
	if err != nil {
		return nil, fmt.Errorf("failed to allocate port: %w", err)
	}

	userID := cm.nextUserID
	cm.nextUserID++

	player := game.NewPlayer(userID, addr, port)
	cm.players[userID] = player
	cm.clientAddrs[key] = userID

	return player, nil
}

// GetPlayerByAddress returns a player by their network address
func (cm *ClientManager) GetPlayerByAddress(addr *net.UDPAddr) (*game.Player, bool) {
	cm.mu.RLock()
	defer cm.mu.RUnlock()

	key := addr.String()
	if userID, exists := cm.clientAddrs[key]; exists {
		player, ok := cm.players[userID]
		return player, ok
	}
	return nil, false
}

// GetPlayer returns a player by their user ID
func (cm *ClientManager) GetPlayer(userID uint8) (*game.Player, bool) {
	cm.mu.RLock()
	defer cm.mu.RUnlock()

	player, exists := cm.players[userID]
	return player, exists
}

// GetAllPlayers returns all active players (except the excluded one)
func (cm *ClientManager) GetAllPlayers(excludeUserID uint8) []*game.Player {
	cm.mu.RLock()
	defer cm.mu.RUnlock()

	var players []*game.Player
	for _, player := range cm.players {
		if player.ID != excludeUserID {
			players = append(players, player)
		}
	}
	return players
}

// UpdatePlayerPosition updates a player's position
func (cm *ClientManager) UpdatePlayerPosition(userID uint8, pos message.PositionDataRTT) {
	cm.mu.Lock()
	defer cm.mu.Unlock()

	if player, exists := cm.players[userID]; exists {
		player.UpdatePosition(pos)
	}
}

// CleanupInactivePlayers removes players that haven't been seen recently
func (cm *ClientManager) CleanupInactivePlayers(timeout time.Duration) {
	cm.mu.Lock()
	defer cm.mu.Unlock()

	for userID, player := range cm.players {
		if !player.IsActive(timeout) {
			// Release the player's port
			cm.portManager.ReleasePort(player.ListenPort)

			// Remove from address mapping
			key := player.Address.String()
			delete(cm.clientAddrs, key)

			// Remove from players
			delete(cm.players, userID)

			fmt.Printf("Cleaned up inactive player %d\n", userID)
		}
	}
}

// GetStats returns current statistics about connected clients
func (cm *ClientManager) GetStats() (playerCount, availablePorts int) {
	cm.mu.RLock()
	defer cm.mu.RUnlock()

	return len(cm.players), cm.portManager.AvailablePorts()
}
