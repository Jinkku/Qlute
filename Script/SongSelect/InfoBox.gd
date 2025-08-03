@tool
class_name InfoBox extends PanelContainer

@onready var IconNode = $Columns/Icon
@onready var ValueNode = $Columns/Label

var _icon : Texture2D
@export var Icon : Texture2D:
	set(value):
		_icon = value
		if IconNode:
			change_texture(value)

var _text := ""
@export var Text : String:
	set(value):
		_text = value
		if ValueNode:
			change_label(value)

func change_label(value):
	ValueNode.text = value

func change_texture(value):
	IconNode.texture = value

func _ready() -> void:
	change_label(_text)
	change_texture(_icon)
