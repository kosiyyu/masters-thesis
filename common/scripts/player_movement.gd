extends CharacterBody3D

@export var SPEED = 4

func _physics_process(delta: float) -> void:
	var direction = Vector3.ZERO
	
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
		
	velocity.x = direction.x * SPEED
	velocity.z = direction.z * SPEED
	
	move_and_slide()
