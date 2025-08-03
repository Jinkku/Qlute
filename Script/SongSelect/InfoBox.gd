@tool
class_name InfoBox extends PanelContainer

@onready var IconNode = $Columns/Icon
@onready var ValueNode = $Columns/Label

@export var Icon : Texture2D:
	set(value):
		Icon = value
		if IconNode:
			change_texture(value)

@export var Text : String:
	set(value):
		Text = value
		if ValueNode:
			change_label(value)

func change_label(value):
	ValueNode.text = value

func change_texture(value):
	IconNode.texture = value

func _ready() -> void:
	change_label(Text)
	change_texture(Icon)
