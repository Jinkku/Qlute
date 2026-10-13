extends Control


@export var center: Vector2 = Vector2(200, 200)
@export var radius: float = 100.0
@export var width: float = 3.0
@export var point_count: int = 100

func _draw():
	# draw_arc draws the outline of an arc / circle
	# Parameters: center, radius, start_angle, end_angle, point_count, color, width
	center = Vector2(position.x - 20, position.y - 20)
	draw_arc(pivot_offset, size.x / 2, 0, TAU, point_count, Color.WHITE, width, true)

func _ready() -> void:
	pivot_offset = size / 2

func _process(delta):
	# Forces Godot to update and redraw the node every frame if you change variables
	queue_redraw()
