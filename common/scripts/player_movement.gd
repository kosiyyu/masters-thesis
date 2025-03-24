extends CharacterBody3D

@export var speed = 200
@export var is_auto_mode = true
@export var time_interval = 2.0
@export var color = Color(0.0, 0.0, 1.0, 1.0)

var rng = RandomNumberGenerator.new()
var current_time = 0.0
var current_direction = Vector3(0, 0, -1)
var material = StandardMaterial3D.new()

enum ColliderType {
	CSG_COMBINER_3D,
	CHARACTER_BODY_3D,
	NULL
}

func _ready() -> void:
	rng.randomize()

	material.albedo_color = color
	material.metallic = 0.5
	material.roughness = 0.2

	$MeshInstance3D.set_surface_override_material(0, material)

func _detect_collision() -> Dictionary:
	var result: Dictionary = {
		collided = false,
		normal = Vector3.ZERO,
		colider_type = ColliderType.NULL
	}
	
	var n: int = get_slide_collision_count()
	for i in range(n):
		var collision = get_slide_collision(i)
		var collider = collision.get_collider()
		
		if collider is CSGCombiner3D:
			result.collided = true
			result.normal = collision.get_normal()
			result.colider_type = ColliderType.CSG_COMBINER_3D
			break
		elif collider is CharacterBody3D:
			result.collided = true
			result.normal = collision.get_normal()
			result.colider_type = ColliderType.CHARACTER_BODY_3D
			break

	return result

func _bounce(wall_normal: Vector3) -> void:
	current_direction = current_direction.reflect(wall_normal)
	
	# Make sure we're not heading back into the wall
	if current_direction.dot(wall_normal) < 0:
		current_direction = - current_direction
	
	# Thats equivalent of 15 deg
	var angle_rad = 0.2518
	current_direction = current_direction.rotated(Vector3.UP, rng.randf_range(-angle_rad, angle_rad))
	
	current_direction = current_direction.normalized()

func _auto_input(delta: float) -> void:
	velocity.x = current_direction.x * speed * delta
	velocity.z = current_direction.z * speed * delta
	
	move_and_slide()

	var collision_info = _detect_collision()
	if collision_info.collided:
		_bounce(collision_info.normal)

		if (collision_info.colider_type == ColliderType.CSG_COMBINER_3D):
			print("Player collided with a wall")
		elif (collision_info.colider_type == ColliderType.CHARACTER_BODY_3D):
			print("Player collided with another player")


func _keyboard_input(delta: float) -> void:
	var direction: Vector3 = Vector3.ZERO

	if Input.is_action_pressed("move_right"):
		direction.x += 1
		print("right")
	if Input.is_action_pressed("move_left"):
		direction.x -= 1
		print("left")
	if Input.is_action_pressed("move_back"):
		direction.z += 1
		print("down")
	if Input.is_action_pressed("move_forward"):
		direction.z -= 1
		print("up")
		
	if direction != Vector3.ZERO:
		direction = direction.normalized()
		
	velocity.x = direction.x * speed * delta
	velocity.z = direction.z * speed * delta
	
	move_and_slide()

func _physics_process(delta: float) -> void:
	if !is_auto_mode:
		_keyboard_input(delta)
	else:
		_auto_input(delta)
