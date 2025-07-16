extends Node

var KikoApi = HTTPRequest.new()
var UsingExternalPCK = false
# Called when the node enters the scene tree for the first time.
func _ready() -> void:
	if FileAccess.file_exists(OS.get_user_data_dir() + "/update.pck"):
		print("Update file found. Using that instead.")
		ProjectSettings.load_resource_pack(OS.get_user_data_dir() + "/update.pck")
		UsingExternalPCK = true
	else:
		print("No update file found. Continuing")
	add_child(KikoApi)
	KikoApi.connect("request_completed",_KikoApiDone)
	KikoApi.Request("https://github.com/Jinkkuu/Qlute/releases/latest/download/RELEASE")

func _KikoApiDone(result, response_code, headers, body):
	var Notify = load("res://Script/Singleton/NotificationListener.cs")
	Notify = Notify.pos
	
	var VersionCode = body.get_string_from_utf8().strip_edges(true, false)
	if VersionCode != ProjectSettings.get_setting("application/config/version"):
		pass
	#	Notify.Post("New update is avaliable!\n" + VersionCode + " is available, click to view.",uri: "https://jinkku.itch.io/qlute")

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass
