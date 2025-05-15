using Godot;

public partial class PlayerController : Node
{
	[Signal]
	public delegate void PlayerPositionUpdatedEventHandler(Vector3 position, float rotationY);

	[Export]
	public float MovementSpeed { get; set; } = 10.0f;

	private CharacterBody3D _parent;

	public override void _Ready()
	{
		_parent = GetParent<CharacterBody3D>();
		if (_parent == null)
		{
			GD.PrintErr("PlayerController must be attached to a CharacterBody3D!");
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_parent == null) return;

		Vector2 input = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");

		// Calculate movement
		Vector3 movementDirection = new Vector3(
			x: input.X,
			y: 0,
			z: input.Y
		).Normalized();

		// Rotate direction to match player orientation
		movementDirection = movementDirection.Rotated(Vector3.Up, _parent.Rotation.Y);

		// Apply velocity
		_parent.Velocity = movementDirection * MovementSpeed;
		_parent.MoveAndSlide();

		// Emit position update signal for networking
		EmitSignal(SignalName.PlayerPositionUpdated, _parent.Position, _parent.Rotation.Y);
	}
}