extends HTTPRequest


# Called when the node enters the scene tree for the first time.
func _ready() -> void:	
	request("https://qlute.jinkku.moe/api/menunotice")

# Called every frame. 'delta' is the elapsed time since the previous frame.
func _process(delta: float) -> void:
	pass


func _on_request_completed(result: int, response_code: int, headers: PackedStringArray, body: PackedByteArray) -> void:
	$"../NoticeText".set_text(body.get_string_from_utf8())
	$"..".visible = true


func _on_timer_timeout() -> void:
	request("https://qlute.jinkku.moe/api/menunotice")
