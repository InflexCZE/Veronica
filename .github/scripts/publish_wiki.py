import re
import shutil
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
HUMAN = ROOT / "Wiki"
SKILLS = ROOT / "Source" / "Veronica" / "Skills"
KNOWLEDGE = ROOT / "Source" / "Veronica" / "Wiki"
EVAL = ROOT / "Eval" / "Tasks"


def read(path):
    return path.read_text(encoding="utf-8").replace("\r\n", "\n")


def split_frontmatter(path):
    text = read(path)
    if not text.startswith("---\n"):
        raise SystemExit(f"{path}: no frontmatter")
    head, _, body = text[4:].partition("\n---\n")
    fields = {}
    for line in head.splitlines():
        key, sep, value = line.partition(": ")
        if not sep:
            raise SystemExit(f"{path}: bad frontmatter line: {line}")
        fields[key] = value
    return fields, body.lstrip("\n")


def page(title, description, body):
    head = f"# {title}\n\n" if title else ""
    return f"{head}*{description}*\n\n{body.rstrip()}\n"


def eval_prompt(text, path):
    match = re.search(r"^## Prompt\n+(.+?)(?:\n\n|\n#|\Z)", text, re.S | re.M)
    if not match:
        raise SystemExit(f"{path}: no '## Prompt' section")
    return " ".join(match.group(1).split())


def eval_title(text, path):
    match = re.match(r"# (.+)", text)
    if not match:
        raise SystemExit(f"{path}: no '# Title' first line")
    return match.group(1).strip()


def index(intro, entries):
    lines = [intro, ""]
    lines += [f"- [{name}]({link}): {desc}" for name, link, desc in entries]
    return "\n".join(lines) + "\n"


def link_text(page_name):
    return page_name.replace("-", " ")


def build(out):
    files = {}
    seen = {}

    def add(rel, content):
        key = Path(rel).stem.lower() if rel.endswith(".md") else rel.lower()
        if key in seen:
            raise SystemExit(f"Page name clash: {rel} and {seen[key]}")
        seen[key] = rel
        files[rel] = content

    if not (HUMAN / "Home.md").is_file():
        raise SystemExit(f"{HUMAN / 'Home.md'} is missing")
    human_pages = []
    for path in sorted(HUMAN.rglob("*")):
        if path.is_dir():
            continue
        rel = path.relative_to(HUMAN).as_posix()
        if path.name == "_Sidebar.md":
            raise SystemExit("The sidebar is generated; remove Wiki/_Sidebar.md")
        add(rel, path.read_bytes())
        if path.suffix == ".md" and path.stem not in ("Home",) and not path.stem.startswith("_"):
            human_pages.append(path.stem)

    skills = []
    for path in sorted(SKILLS.glob("*.md")):
        fields, body = split_frontmatter(path)
        name = f"Skill-{path.stem}"
        add(f"Skills/{name}.md", page(None, fields["description"], body))
        skills.append((fields["name"], name, fields["description"]))
    add("Skills.md", index(
        "Guided modding tasks Veronica follows. Each skill lists what it needs from the modder and the plan it works through.",
        skills))

    knowledge = []
    for path in sorted(KNOWLEDGE.glob("*.md")):
        fields, body = split_frontmatter(path)
        name = f"Knowledge-{path.stem}"
        title = fields.get("title", fields["name"])
        add(f"Game-knowledge/{name}.md", page(title, fields["description"], body))
        knowledge.append((title, name, fields["description"]))
    add("Game-knowledge.md", index(
        "What Veronica knows about Space Engineers 2 modding, shipped with it and read by the model when it answers.",
        knowledge))

    tasks = []
    for path in sorted(EVAL.glob("*.md")):
        text = read(path)
        name = f"Eval-{path.stem}"
        add(f"Eval/{name}.md", text)
        tasks.append((eval_title(text, path), name, eval_prompt(text, path)))
    add("Eval-tasks.md", index(
        "The tasks Veronica is tested on in the Editor. Each gives the modder's request, how the simulated modder answers, and what a passing result looks like.",
        tasks))

    sidebar = ["**[Home](Home)**", ""]
    sidebar += [f"- [{link_text(p)}]({p})" for p in human_pages]
    sidebar += ["", f"**[Skills](Skills)** ({len(skills)})", "",
                f"**[Game knowledge](Game-knowledge)** ({len(knowledge)})", "",
                f"**[Eval tasks](Eval-tasks)** ({len(tasks)})"]
    add("_Sidebar.md", "\n".join(sidebar) + "\n")

    if out.exists():
        shutil.rmtree(out)
    for rel, content in files.items():
        target = out / rel
        target.parent.mkdir(parents=True, exist_ok=True)
        if isinstance(content, bytes):
            target.write_bytes(content)
        else:
            target.write_text(content, encoding="utf-8", newline="\n")
    print(f"{len(files)} files -> {out}")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        raise SystemExit("usage: publish_wiki.py <output dir>")
    build(Path(sys.argv[1]).resolve())
