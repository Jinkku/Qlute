extends Label


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	var version = str(ProjectSettings.get_setting("application/config/version"))
	if version == '0.0.0':
		set_text('Development Build (Nanashi)')
	else:
		set_text('Nanashi/stable ('+ version +')')
