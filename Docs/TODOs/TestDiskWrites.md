# Tests writing to disk

**Exists:** tests touch no web and write no files (`.claude/CLAUDE.md`). `SkillTests.Load_SkipsBrokenAndMismatchedFiles` (`Source/Veronica.Tests/SkillTests.cs`) still writes skill files into a folder beside the test assembly, because `SkillFiles.Load` reads a directory.

**Missing:** `SkillFiles.Load` (and the wiki's loader alike) reading through a source the test can give in memory, and the test moved onto it.
