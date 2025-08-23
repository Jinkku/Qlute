extends ColorRect

func _ready() -> void:
	pivot_offset = Vector2(size.x/2, size.y/2)
# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	if rotation > 360:
		rotation = 0
	else:
		rotation += 5 * delta
