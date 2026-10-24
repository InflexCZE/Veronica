# VRAM sharing on other hardware

**Exists:** the behaviour and costs in `Docs/Tech/VramSharing.md` were measured on one machine: NVIDIA RTX 3070 8 GB, driver 591.44, Windows 10 22H2 (19045), hardware-accelerated GPU scheduling off.

**Missing:** repeat on an AMD 8 GB card, with hardware-accelerated GPU scheduling on, and on Windows 11. The demotion behaviour and the per-GB swap cost are driver and VidMm dependent.
