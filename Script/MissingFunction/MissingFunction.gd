extends PanelContainer


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	pivot_offset = Vector2(size.x/2, size.y/2)
	var animation = create_tween()
	scale = Vector2(0.8,0.8)
	modulate = Color(0,0,0,0)
	animation.set_parallel(true)
	animation.tween_property(self, "scale", Vector2(1,1),0.5).set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)
	animation.tween_property(self, "modulate", Color(1,1,1,1),0.5).set_ease(Tween.EASE_OUT).set_trans(Tween.TRANS_CUBIC)


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass
