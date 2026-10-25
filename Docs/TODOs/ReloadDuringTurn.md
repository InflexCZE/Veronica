# Editor: project reload during a frozen turn

**Exists:** a local turn stops the Editor's engine update loop and disables Render12's frame commit job (`Docs/Tech/VramSharing.md`, Editor residency). The Editor starts that loop again when a scene is created, which includes a project reload.

**Missing:** not live-tested. A reload during a turn restarts the updates while the render thread is parked. With the commit job disabled they should commit no frames and so not block the UI thread. That holds only if the reload keeps the engine scene; a rebuilt engine scene has the job enabled again, and after 30 frames the update would block the UI thread until the turn ends.
