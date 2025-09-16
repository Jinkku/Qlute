extends VBoxContainer

var path = "res://Panels/GameplayElements/Customizable/"
var scenename = ""
# Called when the node enters the scene tree for the first time.
func _process(delta: float) -> void:
	if get_tree().current_scene != null and visible and scenename != get_tree().current_scene.name:
		scenename = get_tree().current_scene.name
		var dir := DirAccess.open(path)
		if dir == null: printerr("Could not open folder"); return
		dir.list_dir_begin()
		for file: String in dir.get_files():
			if file.ends_with(".tscn") and not file.begins_with("Playfield"):
				var node = load("res://Panels/SkinEditorElements/SkinElementDemo.tscn").instantiate().get_node(".")
				node.set_meta("ScenePath", path + file)
				add_child(node)
	elif get_tree().current_scene != null and scenename != get_tree().current_scene.name:
		for scene in get_children():
			scene.queue_free()
	
