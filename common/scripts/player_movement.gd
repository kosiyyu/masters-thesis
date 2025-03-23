extends CharacterBody3D

@export var speed = 200
@export var is_auto_mode = true
@export var time_interval = 2.0

var rng = RandomNumberGenerator.new()
var current_time = 0.0
var current_direction = Vector3(0, 0, -1)

func _ready() -> void:
	rng.randomize()

func _detect_wall_collision() -> Dictionary:
	var result = {"collided": false, "normal": Vector3.ZERO}
	
	var n: int = get_slide_collision_count()
	for i in range(n):
		var collision = get_slide_collision(i)
		var collider = collision.get_collider()
		
		if collider is CSGCombiner3D:
			result.collided = true
			result.normal = collision.get_normal()
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

	var collision_info = _detect_wall_collision()
	
	if collision_info.collided:
		_bounce(collision_info.normal)

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
