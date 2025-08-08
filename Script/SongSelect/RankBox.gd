@tool
class_name RankBox extends PanelContainer

@onready var ValueNode = $RankLabel

@export var Text : String:
	set(value):
		Text = value
		if ValueNode:
			change_label(value)

func change_label(value):
	ValueNode.text = value

func _ready() -> void:
	change_label(Text)
