DB_NAME=idle-survivor
MODULE_NAME=./spacetimedb
GODOT_BINDINGS_PATH=./godot-client/spacetime_bindings

generate-godot-bindings:
	spacetime generate --lang csharp --out-dir $(GODOT_BINDINGS_PATH) --module-path $(MODULE_NAME)

publish-local:
	spacetime publish $(DB_NAME) --module-path $(MODULE_NAME) --server local

publish-local-clean:
	spacetime publish $(DB_NAME) --module-path $(MODULE_NAME) --server local --clear-database -y

publish-maincloud:
	spacetime publish $(DB_NAME) --module-path $(MODULE_NAME)

publish-maincloud-clean:
	spacetime publish $(DB_NAME) --module-path $(MODULE_NAME) --clear-database -y