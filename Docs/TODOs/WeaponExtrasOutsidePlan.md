# Weapon extras are done outside the plan

`block-weapon`'s plan has no item for the optional muzzle flash, sound and aiming. A model that adds them does so after the plans are through, and the proof it gives when `create-block` resumes can claim them before they exist (MyGatling run 20261104-225624, l156 says "extras" were done; they were added at l166-173).

Missing: a plan item for the extras the user asked for (owner decision). Where to start: `Source/Veronica/Skills/block-weapon.md`.
