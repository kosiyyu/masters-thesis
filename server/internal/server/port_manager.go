package server

import (
	"errors"
)

// PortManager manages the allocation and release of UDP ports
type PortManager struct {
	portPool chan int
	minPort  int
	maxPort  int
}

// NewPortManager creates a new port manager with the specified port range
func NewPortManager(minPort, maxPort int) *PortManager {
	pm := &PortManager{
		portPool: make(chan int, maxPort-minPort+1),
		minPort:  minPort,
		maxPort:  maxPort,
	}

	// Initialize the port pool
	for port := minPort; port <= maxPort; port++ {
		pm.portPool <- port
	}

	return pm
}

// AllocatePort returns an available port from the pool
func (pm *PortManager) AllocatePort() (int, error) {
	select {
	case port := <-pm.portPool:
		return port, nil
	default:
		return 0, errors.New("no ports available")
	}
}

// ReleasePort returns a port to the pool for reuse
func (pm *PortManager) ReleasePort(port int) {
	if port >= pm.minPort && port <= pm.maxPort {
		select {
		case pm.portPool <- port:
		default:
			// Pool is full, ignore
		}
	}
}

// AvailablePorts returns the number of available ports
func (pm *PortManager) AvailablePorts() int {
	return len(pm.portPool)
}
