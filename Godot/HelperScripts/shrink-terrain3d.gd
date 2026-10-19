@tool
extends EditorScript
## Godot 4 / Terrain3D 1.0.2 region API.
## Select exactly one Terrain3D node, open this script, then File > Run.
## Writes a NEW terrain scene + data. Never modifies the source terrain.
## Verify on a backup: this script has not been run against your project.

# Use the ORIGINAL, unconverted terrain data, even if its spacing is now 0.25.
# Original reference spacing is 1.0. Output is 20% of that original width/depth.
# One output vertex samples every fifth original map pixel.
# Height values must also be original (not already scaled by an earlier conversion).
const REDUCTION: int = 5
const OUTPUT_VERTEX_SPACING: float = 1.0
const SCALE_HEIGHTS: bool = true
# Vertical pivot: 0.0 scales heights around sea level.
const HEIGHT_PIVOT: float = 0.0
# Must not already exist. Change this before rerunning.
const OUTPUT_DIRECTORY: String = "res://terrain_shrunk_20_percent_v3"
# False stops if painted mesh instances exist. True explicitly omits them.
const OMIT_PAINTED_INSTANCES: bool = false


func _run() -> void:
	var selected: Array[Node] = get_editor_interface().get_selection().get_selected_nodes()
	if selected.size() != 1 or not selected[0].is_class("Terrain3D"):
		push_error("Select exactly one Terrain3D node in the scene tree.")
		return
	var source: Node = selected[0]
	var data: Object = source.get("data")
	if data == null or not data.has_method("get_regions_active"):
		push_error("This script requires Terrain3D's 1.x Terrain3DRegion API.")
		return
	# Terrain3D does not support arbitrary Node3D transforms.
	if not (source as Node3D).global_transform.is_equal_approx(Transform3D.IDENTITY):
		push_error("Source Terrain3D and its parents must have identity transforms.")
		return
	var size: int = int(source.get("region_size"))
	var source_spacing: float = float(source.get("vertex_spacing"))
	if REDUCTION < 2:
		push_error("REDUCTION must be >= 2.")
		return
	if not OUTPUT_DIRECTORY.begins_with("res://") or OUTPUT_DIRECTORY == "res://":
		push_error("Choose a new output directory inside res://.")
		return
	var output: String = OUTPUT_DIRECTORY.simplify_path().trim_suffix("/")
	if DirAccess.dir_exists_absolute(output) or FileAccess.file_exists(output):
		push_error("Output already exists. Choose another OUTPUT_DIRECTORY.")
		return
	var regions: Array = data.call("get_regions_active")
	if regions.is_empty():
		push_error("The selected terrain has no active regions.")
		return

	# Group patches into destination regions before allocating any image buffers.
	var groups: Dictionary = {}
	var instance_regions: int = 0
	for region in regions:
		if not region.has_method("get_maps") or not region.has_method("save"):
			push_error("Unsupported Terrain3DRegion API.")
			return
		var instances: Dictionary = region.get("instances")
		if not instances.is_empty():
			instance_regions += 1
		var maps: Array = region.call("get_maps")
		if maps.size() != 3:
			push_error("Region does not contain height, control and color maps.")
			return
		for index in range(3):
			var image: Image = maps[index]
			var expected: int = Image.FORMAT_RGBA8 if index == 2 else Image.FORMAT_RF
			if image == null or image.is_compressed() or image.get_format() != expected:
				push_error("Unsupported map format. Expected RF / RF / RGBA8. No conversion written.")
				return
			if image.get_size() != Vector2i(size, size):
				push_error("Region map size does not match terrain region_size.")
				return
		var location: Vector2i = region.get("location")
		# A factor of 5 does not divide normal region sizes. Compute sample extents
		# globally, so every region stays aligned, including negative coordinates.
		var rect: Rect2i = _destination_rect(location, size)
		if not rect.has_area():
			continue
		var first := Vector2i(floori(float(rect.position.x) / size), floori(float(rect.position.y) / size))
		var last := Vector2i(floori(float(rect.end.x - 1) / size), floori(float(rect.end.y - 1) / size))
		for ty in range(first.y, last.y + 1):
			for tx in range(first.x, last.x + 1):
				var target := Vector2i(tx, ty)
				if not groups.has(target):
					groups[target] = []
				groups[target].append(region)
	if instance_regions > 0 and not OMIT_PAINTED_INSTANCES:
		push_error("Painted instances found. This converter handles terrain maps only. " +
			"Handle foliage separately, or explicitly set OMIT_PAINTED_INSTANCES = true.")
		return

	var data_directory: String = output.path_join("data")
	var err: Error = DirAccess.make_dir_recursive_absolute(data_directory)
	if err != OK:
		push_error("Cannot create output: " + error_string(err))
		return
	var height_scale: float = 1.0 / float(REDUCTION) if SCALE_HEIGHTS else 1.0
	var completed: int = 0
	print("Converting %d regions. The editor may pause until this finishes." % groups.size())
	for target_key in groups:
		var target: Vector2i = target_key
		var heights := PackedByteArray()
		heights.resize(size * size * 4)
		heights.fill(0)
		var controls := PackedByteArray()
		controls.resize(size * size * 4)
		# Unoccupied patches in partially filled regions must be holes, not flat land.
		for pixel in range(size * size):
			controls.encode_u32(pixel * 4, 4) # Terrain3D hole bit (1 << 2).
		var blank_color := Image.create(size, size, false, Image.FORMAT_RGBA8)
		blank_color.fill(Color(1.0, 1.0, 1.0, 0.5))
		var colors: PackedByteArray = blank_color.get_data()
		for region in groups[target]:
			var location: Vector2i = region.get("location")
			var source_origin: Vector2i = location * size
			var target_origin: Vector2i = target * size
			var rect: Rect2i = _destination_rect(location, size).intersection(Rect2i(target_origin, Vector2i(size, size)))
			var maps: Array = region.call("get_maps")
			var height_image: Image = maps[0]
			var control_image: Image = maps[1]
			var color_image: Image = maps[2]
			var source_h: PackedByteArray = height_image.get_data()
			var source_c: PackedByteArray = control_image.get_data()
			var source_color: PackedByteArray = color_image.get_data()
			# Aligned nearest-neighbor decimation: destination vertex d samples d*REDUCTION.
			# All three maps use exactly the same sample positions.
			for y in range(rect.position.y, rect.end.y):
				for x in range(rect.position.x, rect.end.x):
					var src: Vector2i = Vector2i(x, y) * REDUCTION - source_origin
					var dst: Vector2i = Vector2i(x, y) - target_origin
					var src_offset: int = (src.y * size + src.x) * 4
					var dst_offset: int = (dst.y * size + dst.x) * 4
					var h: float = source_h.decode_float(src_offset)
					heights.encode_float(dst_offset, HEIGHT_PIVOT + (h - HEIGHT_PIVOT) * height_scale)
					# Reinterpret as uint32: never pass packed control values through Color/float.
					controls.encode_u32(dst_offset, source_c.decode_u32(src_offset))
					colors.encode_u32(dst_offset, source_color.decode_u32(src_offset))
			# Run synchronously: yielding here can free the EditorScript before saving.
		print("Saving converted region ", target)
		var result := Terrain3DRegion.new()
		result.region_size = size
		result.location = target
		var result_maps: Array[Image] = [
			Image.create_from_data(size, size, false, Image.FORMAT_RF, heights),
			Image.create_from_data(size, size, false, Image.FORMAT_RF, controls),
			Image.create_from_data(size, size, false, Image.FORMAT_RGBA8, colors),
		]
		result.set_maps(result_maps)
		result.calc_height_range()
		result.modified = true
		var filename: String = Terrain3DUtil.location_to_filename(target)
		err = result.save(data_directory.path_join(filename), false)
		if err != OK:
			push_error("Region save failed: " + error_string(err) + ". Partial output: " + output)
			return
		completed += 1
		print("Shrinking terrain: %d / %d regions" % [completed, groups.size()])

	# Create a separate scene that references the converted data and original assets.
	print("All region files saved. Creating shrunk_terrain.tscn...")
	var root := Node3D.new()
	root.name = "ConvertedTerrain"
	var terrain := Terrain3D.new()
	terrain.name = "Terrain3D"
	terrain.region_size = size
	terrain.vertex_spacing = OUTPUT_VERTEX_SPACING
	terrain.assets = source.get("assets")
	var source_material: Resource = source.get("material")
	if source_material != null:
		terrain.material = source_material.duplicate(true)
	for property in ["mesh_size", "mesh_lods", "collision_layer", "collision_mask"]:
		terrain.set(property, source.get(property))
	terrain.data_directory = data_directory
	root.add_child(terrain)
	# PackedScene includes child nodes only when owned by the scene root.
	terrain.owner = root
	var scene := PackedScene.new()
	err = scene.pack(root)
	if err == OK:
		err = ResourceSaver.save(scene, output.path_join("shrunk_terrain.tscn"))
	root.free()
	if err != OK:
		push_error("Scene save failed: " + error_string(err) + ". Region files remain at " + output)
		return
	get_editor_interface().get_resource_filesystem().scan()
	print("DONE: Open %s/shrunk_terrain.tscn" % output)
	print("Output vertex spacing: %s. Map pixel dimensions reduced by %d." % [OUTPUT_VERTEX_SPACING, REDUCTION])
	print("Width/depth: 20%% of original terrain at spacing 1.0; %.3f of current terrain." % (OUTPUT_VERTEX_SPACING / (REDUCTION * source_spacing)))
	print("Original scene and terrain data unchanged. Detail below the new spacing is discarded.")
	print("Separate objects, foliage, navigation meshes, texture tiling and material height thresholds " +
		"may need adjustment. Shrinking is around world X/Z origin and HEIGHT_PIVOT on Y.")


# Destination vertices d satisfy: source_start <= d * REDUCTION < source_end.
# Ceil bounds avoid duplicated/missing samples when region_size is not divisible by 5.
func _destination_rect(location: Vector2i, size: int) -> Rect2i:
	var start: Vector2i = location * size
	var finish: Vector2i = start + Vector2i(size, size)
	var first := Vector2i(ceili(float(start.x) / REDUCTION), ceili(float(start.y) / REDUCTION))
	var end_exclusive := Vector2i(ceili(float(finish.x) / REDUCTION), ceili(float(finish.y) / REDUCTION))
	return Rect2i(first, end_exclusive - first)
