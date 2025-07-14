extends Control


# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	$Updater.download_file = "user://update.pck"
	$Updater.request("https://github.com/Jinkkuu/Qlute/releases/latest/download/update.pck") # this can work for now, i'll need to make this more better afterwards


# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	$ProgressBar.max_value = $Updater.get_body_size()
	$ProgressBar.value = $Updater.get_downloaded_bytes()


func _reload_game(result: int, response_code: int, headers: PackedStringArray, body: PackedByteArray) -> void:
	if response_code == 200:
		print("Update game file downloaded")
		ProjectSettings.load_resource_pack("user://update.pck",true)
		get_tree().change_scene_to_file("res://Panels/Screens/bootstrap.tscn")
