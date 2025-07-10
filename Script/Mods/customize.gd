extends Button

var on = false
func _on_pressed() -> void:
	on = not on
	if not on:
		text = "Customize"
	else:
		text = "Close Customize"
