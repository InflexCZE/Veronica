# Synthetic templates

**Exists:** `find_template` offers the templates on disk, plus synthetic templates that are a vanilla template's generator followed by a recipe (Atmospheric Thrusters, `Docs/Tech/Tools_Creators.md`). A door leaf, a bound child-model prefab, has no template of its own: `block-door` builds it on Empty Prefab with two definitions and six `component_add` calls in a set order.

**Missing:** templates that exist only in Veronica's tools, created on demand with the right base shape of the data (e.g. a bound door leaf or child model: binding, client and server prefabs, ChildTransform, Hierarchy, ParentRenderAdapter, ModelComponent, ModelRender). In MyDoor D1-B1 run 3 (`.tmp/eval/runs/20261104-043338-MyDoor`) the model gave up on the Empty Prefab leaf build and rebuilt the leaves from the single-sided Subpart template; the direct cause was `HierarchyComponent` not addable by name.

**Owner decision:** recorded because the model failed on Empty Prefab; the design (which templates, where they live, how find_template lists them) is to be settled with the owner.
