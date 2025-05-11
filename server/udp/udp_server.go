package udp

import (
	"bytes"
	"errors"
	"fmt"
	"net"
	"os"
	b "server/utils/binary"
	"server/utils/command"
	"server/utils/data"
	"sync"
	"time"
)

type PlayerState struct {
	Data       data.PositionDataRTT
	Addr       *net.UDPAddr
	ListenPort int
	LastSeen   time.Time
}

var (
	players    = make(map[uint8]PlayerState)
	clients    = make(map[string]uint8) // IP:Port -> UserID
	portPool   = make(chan int, 100)    // 22222-22321
	mu         sync.RWMutex
	nextUserID uint8 = 1
)

func init() {
	for port := 22222; port <= 22321; port++ {
		portPool <- port
	}
}

func allocatePort() (int, error) {
	select {
	case port := <-portPool:
		return port, nil
	default:
		return 0, errors.New("no ports available in pool")
	}
}

func releasePort(port int) {
	if port >= 22222 && port <= 22321 {
		portPool <- port
	}
}

func handlePortRequest(conn *net.UDPConn, addr *net.UDPAddr) {
	mu.Lock()
	defer mu.Unlock()

	// Check if client already exists
	key := addr.String()
	if _, exists := clients[key]; exists {
		// Client already registered, do nothing
		return
	}

	port, err := allocatePort()
	if err != nil {
		fmt.Println("Failed to allocate port:", err)
		return
	}

	userID := nextUserID
	nextUserID++

	clients[key] = userID
	players[userID] = PlayerState{
		Addr:       addr,
		ListenPort: port,
		LastSeen:   time.Now(),
		Data: data.PositionDataRTT{
			UserID: userID,
		},
	}

	// Send both port assignment and user assignment
	sendPortAssignment(conn, addr, userID, port)
	sendUserAssignment(conn, addr, userID, port)
}

func sendPortAssignment(conn *net.UDPConn, addr *net.UDPAddr, userID uint8, port int) {
	response := data.PortAssignment{
		CommandID: command.PORT_ASSIGNMENT,
		UserID:    userID,
		Port:      uint16(port),
	}

	data.PortAssignment.Print(response)

	data, err := b.SerializePortAssignment(response)
	if err != nil {
		fmt.Println("Failed to serialize port assignment:", err)
		return
	}
	conn.WriteToUDP(data, addr)
}

func sendUserAssignment(conn *net.UDPConn, addr *net.UDPAddr, userID uint8, port int) {
	assignment := data.UserAssignment{
		CommandID: command.USER_ASSIGNMENT,
		UserID:    userID,
	}

	data, err := b.SerializeUserAssignment(assignment)
	if err != nil {
		fmt.Println("Failed to serialize user assignment:", err)
		return
	}

	conn.WriteToUDP(data, &net.UDPAddr{
		IP:   addr.IP,
		Port: port,
	})
}

func broadcastUpdate(conn *net.UDPConn, update data.PositionData) {
	mu.RLock()
	defer mu.RUnlock()

	for _, player := range players {
		if player.Data.UserID != update.UserID {
			sendPlayerState(conn, &net.UDPAddr{
				IP:   player.Addr.IP,
				Port: player.ListenPort,
			}, update)
		}
	}
}

func sendPlayerState(conn *net.UDPConn, addr *net.UDPAddr, data data.PositionData) {
	byteArray, err := b.SerializePositionData(data)
	if err != nil {
		fmt.Println("Serialization error:", err)
		return
	}

	conn.WriteToUDP(byteArray, addr)
}

func cleanupInactivePlayers() {
	mu.Lock()
	defer mu.Unlock()

	now := time.Now()
	timeout := 60 * time.Second

	for userID, player := range players {
		if now.Sub(player.LastSeen) > timeout {
			// Release port back to pool
			releasePort(player.ListenPort)

			// Remove from clients map
			for key, id := range clients {
				if id == userID {
					delete(clients, key)
					break
				}
			}

			// Remove from players map
			delete(players, userID)
		}
	}
}

type Udp struct {
	conn *net.UDPConn
	addr string
	tick time.Duration
}

const TICK_CONSTANT_MS = 1

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
		tick: TICK_CONSTANT_MS * time.Millisecond,
	}
}

func (u *Udp) Run() {
	buffer := make([]byte, 1024)

	fmt.Printf("UDP server listening on %s\n", u.addr)

	// Start cleanup goroutine
	ticker := time.NewTicker(30 * time.Second)
	go func() {
		for range ticker.C {
			cleanupInactivePlayers()
		}
	}()

	for {
		n, clientAddr, err := u.conn.ReadFromUDP(buffer)
		if err != nil {
			fmt.Println("Error reading from UDP:", err)
			continue
		}

		handleData(u.conn, clientAddr, n, buffer[:n])
		time.Sleep(u.tick)
	}
}

func getCientID(clientAddr *net.UDPAddr) uint8 {
	key := clientAddr.String()
	clientID, exists := clients[key]
	if !exists {
		fmt.Printf("Client not found for address %s\n", clientAddr)
	}
	return clientID
}

func handleData(conn *net.UDPConn, clientAddr *net.UDPAddr, n int, byteArray []byte) {
	// Handle empty packets
	if n == 0 {
		return
	}

	// Check if first byte is a command
	commandID := command.Command(byteArray[0])

	// Handle special commands that don't need full deserialization
	if commandID == command.PORT_REQUEST {
		handlePortRequest(conn, clientAddr)
		return
	}

	// For other commands, deserialize fully
	value, commandID, err := deserializeData(byteArray)
	if err != nil {
		fmt.Println("Deserialization error:", err)
		return
	}

	switch commandID {
	case command.POSITION:
		positionData, ok := value.(data.PositionData)
		if !ok {
			fmt.Printf("Error during receiving %v data. Received %d bytes from %s\n", commandID, n, clientAddr)
			return
		}
		data.PositionData.Print(positionData)

	case command.POSITION_RTT:
		positionDataRTT, ok := value.(data.PositionDataRTT)
		if !ok {
			return
		}

		positionData := data.PositionData{
			CommandID: command.POSITION,
			UserID:    positionDataRTT.UserID,
			X:         positionDataRTT.X,
			Y:         positionDataRTT.Y,
			Z:         positionDataRTT.Z,
			RotY:      positionDataRTT.RotY,
		}

		mu.Lock()
		key := clientAddr.String()
		userID, exists := clients[key]

		if !exists {
			// Auto-register if new
			mu.Unlock()
			handlePortRequest(conn, clientAddr)
			return
		}

		// Update player data
		if state, ok := players[userID]; ok {
			state.Data = positionDataRTT
			state.LastSeen = time.Now()
			players[userID] = state
		}
		mu.Unlock()

		// Broadcast to all clients via their assigned ports
		broadcastUpdate(conn, positionData)

		// Send RTT response for POSITION_RTT packets (add this part)
		mu.RLock()
		player, exists := players[positionDataRTT.UserID]
		listenPort := 0
		if exists {
			listenPort = player.ListenPort
		}
		mu.RUnlock()

		if listenPort > 0 {
			sendRTTResponse(conn, clientAddr, positionDataRTT.TimestampRTT, listenPort)
		}

		fmt.Printf("ClientID: %v | PositionDataRTT{CommandID: %v, UserID: %v, X: %v, Y: %v, Z: %v, RotY: %v, TimestampRTT: %v}\n",
			getCientID(clientAddr), positionDataRTT.CommandID, positionDataRTT.UserID, positionDataRTT.X, positionDataRTT.Y, positionDataRTT.Z, positionDataRTT.RotY, positionDataRTT.TimestampRTT)

	case command.MOVE:
		moveData, ok := value.(data.MoveData)
		if !ok {
			fmt.Printf("Error during receiving %v data.", commandID)
			return
		}
		data.MoveData.Print(moveData)

	case command.MOVE_RTT:
		moveDataRTT, ok := value.(data.MoveDataRTT)
		if !ok {
			fmt.Printf("Error during receiving %v data.", commandID)
			return
		}
		data.MoveDataRTT.Print(moveDataRTT)

		// Get player's listen port
		mu.RLock()
		player, exists := players[moveDataRTT.UserID]
		listenPort := 0
		if exists {
			listenPort = player.ListenPort
		}
		mu.RUnlock()

		if listenPort > 0 {
			sendRTTResponse(conn, clientAddr, moveDataRTT.TimestampRTT, listenPort)
		}
	}
}

func sendRTTResponse(conn *net.UDPConn, clientAddr *net.UDPAddr, timestampRTT uint32, port int) {
	responseAddr := &net.UDPAddr{
		IP:   clientAddr.IP,
		Port: port,
	}

	defaultRTT := data.DefaultRTT{
		CommandID:    command.DEFAULT_RTT,
		TimestampRTT: timestampRTT,
	}

	byteArray, err := b.SerializeDefaultRTT(defaultRTT)
	if err != nil {
		fmt.Println("Serialization error:", err)
		return
	}

	_, err = conn.WriteToUDP(byteArray, responseAddr)
	if err != nil {
		fmt.Println("Send error:", err)
		return
	}
}

func deserializeData(byteArray []byte) (interface{}, command.Command, error) {
	if len(byteArray) == 0 {
		return nil, 0, errors.New("empty byte array")
	}

	commandID := command.Command(byteArray[0])

	reader := bytes.NewReader(byteArray)

	switch command.Command(commandID) {
	case command.POSITION:
		value, err := b.DeserializePostitionData(reader)
		return value, commandID, err
	case command.MOVE:
		value, err := b.DeserializeMoveData(reader)
		return value, commandID, err
	case command.POSITION_RTT:
		value, err := b.DeserializePostitionDataRTT(reader)
		return value, commandID, err
	case command.MOVE_RTT:
		value, err := b.DeserializeMoveDataRTT(reader)

		return value, commandID, err
	case command.USER_ASSIGNMENT:
		value, err := b.DeserializeUserAssignment(reader)
		return value, commandID, err
	default:
		return nil, commandID, fmt.Errorf("unknown command")
	}
}
