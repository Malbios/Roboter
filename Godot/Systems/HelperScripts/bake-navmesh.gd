extends SceneTree
## Bakes the navigation mesh of a map and saves it as a resource file.
## Walkable ground = the Terrain3D (if any) + every node in the "navigation_source" group (with children).
## Terrain lower than LOWEST_WALKABLE_GROUND is river bed and not walkable, so rivers can only be crossed on bridges.
##
## Usage (from the repo root):
##   Godot_console.exe --headless --path Godot --script res://Systems/HelperScripts/bake-navmesh.gd -- res://Maps/1v1.tscn res://Maps/MapData/1v1/navmesh.res

const SOURCE_GROUP := "navigation_source"
const FRAMES_TO_WAIT_FOR_TERRAIN := 3
const MAX_DISTANCE_FROM_NAVMESH := 1.0
const LOWEST_WALKABLE_GROUND := -1.0

var map: Node
var output_path: String
var frames_waited := 0
var is_baked := false
var navigation_iteration_at_bake := 0


func _init() -> void:
	var arguments := OS.get_cmdline_user_args()
	if arguments.size() != 2:
		push_error("Expected two arguments: <map scene> <output navmesh path>")
		quit(1)
		return

	output_path = arguments[1]
	map = load(arguments[0]).instantiate()
	var match_node := map.get_node_or_null("Match")
	if match_node:
		map.remove_child(match_node)
		match_node.free()
	root.add_child(map)


func _process(_delta: float) -> bool:
	frames_waited += 1
	if frames_waited == FRAMES_TO_WAIT_FOR_TERRAIN:
		bake()
	elif is_baked and NavigationServer3D.map_get_iteration_id(navigation_map()) > navigation_iteration_at_bake:
		warn_about_unreachable_objects()
		quit()
	return false


func bake() -> void:
	var nav_mesh := create_nav_mesh()
	var source := NavigationMeshSourceGeometryData3D.new()
	NavigationServer3D.parse_source_geometry_data(nav_mesh, source, map)
	for terrain in map.find_children("*", "Terrain3D", true, false):
		var terrain_faces: PackedVector3Array = terrain.generate_nav_mesh_source_geometry(terrain_bounds(terrain), false)
		source.add_faces(without_river_beds(terrain_faces), Transform3D.IDENTITY)

	var start := Time.get_ticks_msec()
	NavigationServer3D.bake_from_source_geometry_data(nav_mesh, source)
	remove_broken_polygons(nav_mesh)
	print("Baked %d polygons in %d ms" % [nav_mesh.get_polygon_count(), Time.get_ticks_msec() - start])

	var error := ResourceSaver.save(nav_mesh, output_path, ResourceSaver.FLAG_COMPRESS)
	print("Saved %s: %s" % [output_path, error_string(error)])
	if error != OK:
		quit(1)
		return

	var region := map.get_node_or_null("NavigationRegion3D")
	if region == null:
		region = NavigationRegion3D.new()
		map.add_child(region)
	navigation_iteration_at_bake = NavigationServer3D.map_get_iteration_id(navigation_map())
	region.navigation_mesh = nav_mesh
	is_baked = true


func navigation_map() -> RID:
	return map.get_world_3d().navigation_map


func warn_about_unreachable_objects() -> void:
	var nav_map := navigation_map()
	var objects: Array[Node] = []
	objects.append_array(get_nodes_in_group("ScrapPiles"))
	objects.append_array(get_nodes_in_group("GasVolcanoes"))
	var start_positions := map.get_node_or_null("StartPositions")
	if start_positions:
		objects.append_array(start_positions.get_children())

	var unreachable_count := 0
	for object in objects:
		var position: Vector3 = object.global_position
		var closest := NavigationServer3D.map_get_closest_point(nav_map, position)
		var radius: float = object.get("Radius") if "Radius" in object else 0.0
		var distance := Vector2(closest.x - position.x, closest.z - position.z).length() - radius
		if distance > MAX_DISTANCE_FROM_NAVMESH:
			unreachable_count += 1
			push_warning("%s at %s is %.1f m (from its edge) away from walkable ground" % [object.name, position, distance])
	print("Unreachable scrap piles, gas volcanoes and start positions: %d" % unreachable_count)


func without_river_beds(faces: PackedVector3Array) -> PackedVector3Array:
	var dry_faces := PackedVector3Array()
	for i in range(0, faces.size(), 3):
		var lowest := minf(faces[i].y, minf(faces[i + 1].y, faces[i + 2].y))
		if lowest >= LOWEST_WALKABLE_GROUND:
			dry_faces.append_array([faces[i], faces[i + 1], faces[i + 2]])
	return dry_faces


func create_nav_mesh() -> NavigationMesh:
	var nav_mesh := NavigationMesh.new()
	nav_mesh.cell_size = 0.5
	nav_mesh.cell_height = 0.25
	nav_mesh.agent_radius = 0.25
	nav_mesh.agent_height = 1.5
	nav_mesh.agent_max_climb = 1.0
	nav_mesh.agent_max_slope = 50.0
	nav_mesh.geometry_parsed_geometry_type = NavigationMesh.PARSED_GEOMETRY_BOTH
	nav_mesh.geometry_source_geometry_mode = NavigationMesh.SOURCE_GEOMETRY_GROUPS_WITH_CHILDREN
	nav_mesh.geometry_source_group_name = SOURCE_GROUP
	return nav_mesh


func terrain_bounds(terrain: Node) -> AABB:
	var region_length: float = terrain.get_region_size() * terrain.get_vertex_spacing()
	var bounds := AABB()
	var first := true
	for location in terrain.data.get_region_locations():
		var region := AABB(Vector3(location.x * region_length, -1000, location.y * region_length), Vector3(region_length, 2000, region_length))
		bounds = region if first else bounds.merge(region)
		first = false
	return bounds


# Works around Godot issue #85548 (overlapping polygons that break the navigation map).
# Copied from Terrain3D's baker (addons/terrain_3d/menu/baker.gd, MIT license).
func remove_broken_polygons(nav_mesh: NavigationMesh) -> void:
	var vertices := round_vertices(nav_mesh)
	var polygons := remove_empty_polygons(nav_mesh, vertices)
	remove_overlapping_polygons(nav_mesh, vertices, polygons)
	nav_mesh.clear_polygons()
	nav_mesh.set_vertices(vertices)
	for polygon in polygons:
		nav_mesh.add_polygon(polygon)


func round_vertices(nav_mesh: NavigationMesh) -> PackedVector3Array:
	var round_factor := Vector3(nav_mesh.cell_size, nav_mesh.cell_height, nav_mesh.cell_size) * 1.001
	var vertices := nav_mesh.get_vertices()
	for i in range(vertices.size()):
		vertices[i] = (vertices[i] / round_factor).floor() * round_factor
	return vertices


func remove_empty_polygons(nav_mesh: NavigationMesh, vertices: PackedVector3Array) -> Array[PackedInt32Array]:
	var polygons: Array[PackedInt32Array] = []
	for i in range(nav_mesh.get_polygon_count()):
		var new_polygon := PackedInt32Array()
		var polygon_vertices := PackedVector3Array()
		for index in nav_mesh.get_polygon(i):
			if polygon_vertices.has(vertices[index]):
				continue
			polygon_vertices.push_back(vertices[index])
			new_polygon.push_back(index)
		if new_polygon.size() > 2:
			polygons.push_back(new_polygon)
	return polygons


func remove_overlapping_polygons(nav_mesh: NavigationMesh, vertices: PackedVector3Array, polygons: Array[PackedInt32Array]) -> void:
	var cell_size := Vector3(nav_mesh.cell_size, nav_mesh.cell_height, nav_mesh.cell_size)
	var polygons_by_edge := {}
	for polygon_index in range(polygons.size()):
		var polygon := polygons[polygon_index]
		for j in range(polygon.size()):
			var edge_key := [Vector3i(vertices[polygon[j]] / cell_size), Vector3i(vertices[polygon[(j + 1) % polygon.size()]] / cell_size)]
			edge_key.sort()
			if not polygons_by_edge.has(edge_key):
				polygons_by_edge[edge_key] = []
			polygons_by_edge[edge_key].push_back(polygon_index)

	var overlap_count := {}
	for polygon_indices in polygons_by_edge.values():
		if polygon_indices.size() <= 2:
			continue
		for polygon_index in polygon_indices:
			overlap_count[polygon_index] = overlap_count.get(polygon_index, 0) + 1

	var bad_polygons := overlap_count.keys().filter(func(index): return overlap_count[index] >= 2)
	bad_polygons.sort()
	for i in range(bad_polygons.size() - 1, -1, -1):
		polygons.remove_at(bad_polygons[i])
