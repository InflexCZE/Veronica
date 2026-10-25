# Editor: project reload during a frozen turn

**Exists:** a local turn stops the Editor's engine update loop (`Docs/Tech/VramSharing.md`, Editor residency). The Editor starts that loop again when a scene is created, which includes a project reload.

**Missing:** a reload during a turn restarts the updates while the render thread is parked. After 30 frames the update blocks the UI thread until the turn ends; the Editor recovers afterwards. The owner accepted this as rare. A fix would stop the loop again while frozen, or refuse the reload.
