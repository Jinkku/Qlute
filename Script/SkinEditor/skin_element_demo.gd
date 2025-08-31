extends Button

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	if has_meta("ScenePath") and get_meta("ScenePath") != null and get_meta("ScenePath") != "":
		print(get_meta("ScenePath"))
		var Scene = load(get_meta("ScenePath")).instantiate()
		$SubViewport.add_child(Scene)
		$SubViewport.size = Scene.size
		$SkinText.text = Scene.name
