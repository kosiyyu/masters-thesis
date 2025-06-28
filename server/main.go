package main

import (
	"log"
	"os"
	"os/signal"
	"server/internal/server"
	"syscall"
)

func main() {
	// Default server address
	address := ":8080"

	// Override with command line argument if provided
	if len(os.Args) > 1 {
		address = os.Args[1]
	}

	// Create server with port range 22222-22321
	gameServer := server.NewServer(address, 22222, 22321)

	// Setup graceful shutdown
	sigChan := make(chan os.Signal, 1)
	signal.Notify(sigChan, syscall.SIGINT, syscall.SIGTERM)

	// Start server in a goroutine
	go func() {
		if err := gameServer.Start(); err != nil {
			log.Fatalf("Server failed to start: %v", err)
		}
	}()

	// Wait for shutdown signal
	<-sigChan
	log.Println("Shutting down server...")

	if err := gameServer.Stop(); err != nil {
		log.Printf("Error during shutdown: %v", err)
	} else {
		log.Println("Server stopped gracefully")
	}
}
