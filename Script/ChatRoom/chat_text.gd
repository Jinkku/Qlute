extends Control
func _ready() -> void:
	if has_meta("username"):
		$ChatText.text = str(get_meta("username"))
	else:
		$ChatText.text = "N/A"
	if has_meta("text"):
		$ChatText/Text.text = str(get_meta("text"))
	else:
		$ChatText/Text.text = "..."
	$ChatText/Text.position = Vector2(150,$ChatText/Text.position.y)
