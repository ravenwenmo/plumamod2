extends SpineSprite


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	var animation_state = get_animation_state()
	animation_state.set_animation("Idle", true, 0)


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass
