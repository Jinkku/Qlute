extends PanelContainer


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	if has_meta("ModName"):
		$Customizations/Name.text = str(get_meta("ModName"))
	else:
		$Customizations/Name.text = "??"
