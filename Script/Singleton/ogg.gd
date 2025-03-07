extends Node


func load_audio(file_name: String) -> AudioStream:
	var file_path = file_name
	var audio_stream = load(file_path)  # Load the OGG file
	
	if audio_stream is AudioStream:
		return audio_stream
	else:
		print("Failed to load audio file: ", file_path)
		return null
