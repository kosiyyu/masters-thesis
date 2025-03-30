package main

import "server/udp"

func main() {
	udpServer := udp.NewUdp("127.0.0.1:9000")
	udpServer.Run()
}
