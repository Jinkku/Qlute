extends PanelContainer

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	if get_tree().current_scene == null:
		show()
	elif get_tree().current_scene.name != "Gameplay":
		show()
	else:
		hide()
