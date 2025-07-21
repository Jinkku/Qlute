extends TextureRect

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	position = Vector2($"../SpectatorMenu".position.x - size.x - 10, $"../SpectatorMenu".position.y + 10)
