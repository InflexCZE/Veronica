---
name: overrides-and-copies
title: Partial definitions: overrides and copies
source: Veronica's create_partial tool (Docs/Tech/EditorTools.md)
description: How a mod changes a definition it does not own: an override versus a copy, what values follow the original, and an override that already exists.
---
Vanilla and other read-only projects are never written. A mod changes their definitions through a partial definition, made with create_partial(def, kind, folder):
- override: changes the original everywhere it is used. Use it when the user wants the existing thing changed.
- copy: a new thing next to the original. It changes nothing until something references it. Use it when the user wants something new that starts from the original.

In both, values left alone keep following the original. Write only the values that change.

A mod holds at most one override of a definition. When one exists, create_partial's error names its file; write to that file instead.
