@tool
class_name RankBox extends PanelContainer

@onready var ValueNode = $RankLabel
var Colors = [Color(1,0,0),Color(0,1,0),Color(1,1,0),Color(0,0,0)]
@export var Rankid : int:
	set(value):
		Rankid = value
		if ValueNode:
			change_label(value)

func change_label(value):
	var textrank = "N/A"
	self_modulate = Colors[3]
	if value == 0:
		textrank = "Unranked"
		self_modulate = Colors[0]
	elif value == 1:
		textrank = "Ranked"
		self_modulate = Colors[1]
	elif value == 2:
		textrank = "Special"
		self_modulate = Colors[2]
	ValueNode.text = textrank

func _ready() -> void:
	change_label(Rankid)
