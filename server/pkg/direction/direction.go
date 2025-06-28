package direction

type Direction uint8

const (
	North Direction = iota
	NorthEast
	East
	SouthEast
	South
	SouthWest
	West
	NorthWest
)

func (d Direction) String() string {
	directions := []string{"North", "NorthEast", "East", "SouthEast", "South", "SouthWest", "West", "NorthWest"}
	if int(d) < len(directions) {
		return directions[d]
	}
	return "Unknown"
}
