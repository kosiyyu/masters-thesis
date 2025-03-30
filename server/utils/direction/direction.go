package direction

type Direction uint8

const (
	North     Direction = iota // 0
	NorthEast                  // 1
	East                       // 2
	SouthEast                  // 3
	South                      // 4
	SouthWest                  // 5
	West                       // 6
	NorthWest                  // 7
)
