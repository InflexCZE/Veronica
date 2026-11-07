# Listing headers copied into read targets

**Exists:** `list_folder` and `find_content` replies start with a header naming the project and folder, such as `Vanilla (read-only): Assets\...`.

**Missing:** the model copies that header into a read target, and the read is refused:
- Bonsai MyTimer `.tmp/eval/runs/20261107-111315-MyTimer` l17; recovered through the nearest-name hint.

**Options:** read accepts a known project header and strips it; or the header is written so it does not look like part of a path.

Observe how often it recurs before acting: one occurrence in the 2026-11-07 Bonsai sweep, recovered on the next call.
