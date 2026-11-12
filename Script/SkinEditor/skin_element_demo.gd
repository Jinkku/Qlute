extends Button

# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	if has_meta("ScenePath") and get_meta("ScenePath") != null and get_meta("ScenePath") != "":
		print(get_meta("ScenePath"))
		var Scene = load(get_meta("ScenePath")).instantiate()
		$SubViewport.add_child(Scene)
		$SubViewport.size = Scene.size
		$SkinText.text = Scene.name


func _pressed() -> void:
	var path = get_meta("ScenePath", "")
	if path == "":
		return
	var node = get_parent()
	while node != null and not node.has_method("AddComponent"):
		node = node.get_parent()
	if node != null:
		node.AddComponent(path)
