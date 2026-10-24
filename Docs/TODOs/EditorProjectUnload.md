# Project unload

**Exists:** `VeronicaPlugin` adds the panel and the `Veronica` menu on project load and keeps them for the life of the process.

**Missing:** on Debug > Unload Project the Editor disposes plugins that implement `IDisposable`; Veronica does not, so its panel and menu stay live with no project behind them. Harmless while the panel uses no project data. Once it does, implement `IDisposable` on `VeronicaPlugin` and detach there (remove the panel from the dock manager, drop project state); the next project load adds it again.
