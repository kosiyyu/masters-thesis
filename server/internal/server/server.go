package server

import (
	"fmt"
	"log"
	"net"
	"server/internal/command"
	"server/internal/message"
	"time"
)

// Server represents the main UDP game server
type Server struct {
	conn          *net.UDPConn
	address       string
	clientManager *ClientManager
	serializer    *message.Serializer
	tickRate      time.Duration
}

// NewServer creates a new UDP game server
func NewServer(address string, minPort, maxPort int) *Server {
	return &Server{
		address:       address,
		clientManager: NewClientManager(minPort, maxPort),
		serializer:    message.NewSerializer(),
		tickRate:      time.Millisecond, // 1ms tick rate
	}
}

// Start starts the UDP server
func (s *Server) Start() error {
	udpAddr, err := net.ResolveUDPAddr("udp", s.address)
	if err != nil {
		return fmt.Errorf("failed to resolve UDP address: %w", err)
	}

	s.conn, err = net.ListenUDP("udp", udpAddr)
	if err != nil {
		return fmt.Errorf("failed to listen on UDP: %w", err)
	}

	log.Printf("UDP server listening on %s", s.address)

	// Start cleanup routine
	go s.cleanupRoutine()

	// Start main server loop
	return s.run()
}

// Stop stops the server
func (s *Server) Stop() error {
	if s.conn != nil {
		return s.conn.Close()
	}
	return nil
}

// run is the main server loop
func (s *Server) run() error {
	buffer := make([]byte, 1024)

	for {
		n, clientAddr, err := s.conn.ReadFromUDP(buffer)
		if err != nil {
			log.Printf("Error reading UDP: %v", err)
			continue
		}

		// Handle the incoming data
		go s.handlePacket(clientAddr, buffer[:n])

		// Tick rate limiting
		time.Sleep(s.tickRate)
	}
}

// handlePacket processes incoming packets
func (s *Server) handlePacket(clientAddr *net.UDPAddr, data []byte) {
	if len(data) == 0 {
		return
	}

	// Check for port request (special case)
	if command.Command(data[0]) == command.PORT_REQUEST {
		s.handlePortRequest(clientAddr)
		return
	}

	// Deserialize the packet
	messageData, cmd, err := s.serializer.Deserialize(data)
	if err != nil {
		log.Printf("Deserialization error: %v", err)
		return
	}

	// Handle different message types
	switch cmd {
	case command.POSITION:
		s.handlePosition(messageData.(message.PositionData))
	case command.POSITION_RTT:
		s.handlePositionRTT(clientAddr, messageData.(message.PositionDataRTT))
	case command.MOVE:
		s.handleMovement(messageData.(message.MoveData))
	case command.MOVE_RTT:
		s.handleMovementRTT(clientAddr, messageData.(message.MoveDataRTT))
	default:
		log.Printf("Unhandled command: %v", cmd)
	}
}

// handlePortRequest handles new client registration
func (s *Server) handlePortRequest(clientAddr *net.UDPAddr) {
	player, err := s.clientManager.RegisterClient(clientAddr)
	if err != nil {
		log.Printf("Failed to register client: %v", err)
		return
	}

	// Send port assignment
	portAssignment := message.PortAssignment{
		CommandID: command.PORT_ASSIGNMENT,
		UserID:    player.ID,
		Port:      uint16(player.ListenPort),
	}

	data, err := s.serializer.SerializePortAssignment(portAssignment)
	if err != nil {
		log.Printf("Failed to serialize port assignment: %v", err)
		return
	}

	s.conn.WriteToUDP(data, clientAddr)

	// Send user assignment
	userAssignment := message.UserAssignment{
		CommandID: command.USER_ASSIGNMENT,
		UserID:    player.ID,
	}

	data, err = s.serializer.SerializeUserAssignment(userAssignment)
	if err != nil {
		log.Printf("Failed to serialize user assignment: %v", err)
		return
	}

	s.conn.WriteToUDP(data, player.GetListenAddress())

	log.Printf("Registered new client: UserID=%d, Port=%d", player.ID, player.ListenPort)
}

// handlePosition handles position updates
func (s *Server) handlePosition(pos message.PositionData) {
	fmt.Printf("Position update: UserID=%d, X=%.2f, Y=%.2f, Z=%.2f, RotY=%.2f\n",
		pos.UserID, pos.X, pos.Y, pos.Z, pos.RotY)
}

// handlePositionRTT handles position updates with RTT
func (s *Server) handlePositionRTT(clientAddr *net.UDPAddr, pos message.PositionDataRTT) {
	// Auto-register if client not found
	player, exists := s.clientManager.GetPlayerByAddress(clientAddr)
	if !exists {
		s.handlePortRequest(clientAddr)
		return
	}

	// Update player position
	s.clientManager.UpdatePlayerPosition(pos.UserID, pos)

	// Broadcast to other players
	s.broadcastPosition(message.PositionData{
		CommandID: command.POSITION,
		UserID:    pos.UserID,
		X:         pos.X,
		Y:         pos.Y,
		Z:         pos.Z,
		RotY:      pos.RotY,
	}, pos.UserID)

	// Send RTT response
	s.sendRTTResponse(player.GetListenAddress(), pos.TimestampRTT)

	fmt.Printf("PositionRTT update: UserID=%d, X=%.2f, Y=%.2f, Z=%.2f, RotY=%.2f, RTT=%d\n",
		pos.UserID, pos.X, pos.Y, pos.Z, pos.RotY, pos.TimestampRTT)
}

// handleMovement handles movement commands
func (s *Server) handleMovement(mov message.MoveData) {
	fmt.Printf("Movement: UserID=%d, Direction=%s, Speed=%.2f\n",
		mov.UserID, mov.DirectionID, mov.Speed)
}

// handleMovementRTT handles movement commands with RTT
func (s *Server) handleMovementRTT(_ *net.UDPAddr, mov message.MoveDataRTT) {
	player, exists := s.clientManager.GetPlayer(mov.UserID)
	if !exists {
		return
	}

	// Send RTT response
	s.sendRTTResponse(player.GetListenAddress(), mov.TimestampRTT)

	fmt.Printf("MovementRTT: UserID=%d, Direction=%s, Speed=%.2f, RTT=%d\n",
		mov.UserID, mov.DirectionID, mov.Speed, mov.TimestampRTT)
}

// broadcastPosition sends position updates to all other players
func (s *Server) broadcastPosition(pos message.PositionData, excludeUserID uint8) {
	data, err := s.serializer.SerializePositionData(pos)
	if err != nil {
		log.Printf("Failed to serialize position for broadcast: %v", err)
		return
	}

	players := s.clientManager.GetAllPlayers(excludeUserID)
	for _, player := range players {
		s.conn.WriteToUDP(data, player.GetListenAddress())
	}
}

// sendRTTResponse sends an RTT response back to the client
func (s *Server) sendRTTResponse(addr *net.UDPAddr, timestamp uint32) {
	response := message.DefaultRTT{
		CommandID:    command.DEFAULT_RTT,
		TimestampRTT: timestamp,
	}

	data, err := s.serializer.SerializeDefaultRTT(response)
	if err != nil {
		log.Printf("Failed to serialize RTT response: %v", err)
		return
	}

	s.conn.WriteToUDP(data, addr)
}

// cleanupRoutine periodically removes inactive players
func (s *Server) cleanupRoutine() {
	ticker := time.NewTicker(30 * time.Second)
	defer ticker.Stop()

	for range ticker.C {
		s.clientManager.CleanupInactivePlayers(60 * time.Second)

		playerCount, availablePorts := s.clientManager.GetStats()
		log.Printf("Active players: %d, Available ports: %d", playerCount, availablePorts)
	}
}
